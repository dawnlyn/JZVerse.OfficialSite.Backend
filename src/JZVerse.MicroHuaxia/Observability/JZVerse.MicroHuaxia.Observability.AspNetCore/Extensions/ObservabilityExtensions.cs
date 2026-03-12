using JZVerse.MicroHuaxia.Observability.AspNetCore.Http;
using JZVerse.MicroHuaxia.Observability.AspNetCore.Startup;
using JZVerse.MicroHuaxia.Observability.Core.Configuration;
using JZVerse.MicroHuaxia.Observability.Core.FileLogging;
using JZVerse.MicroHuaxia.Observability.Core.Formatting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace JZVerse.MicroHuaxia.Observability.AspNetCore.Extensions;

/// <summary>
/// 可观测性一站式扩展方法
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// 添加统一可观测性：控制台日志、文件日志、OpenTelemetry（日志/追踪/指标）
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        var section = configuration.GetSection(ObservabilityOptions.SectionName);
        services.Configure<ObservabilityOptions>(section);
        services.Configure<ConsoleOptions>(section.GetSection("Console"));

        var options = section.Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        // 注册格式化器 & 序列化器
        services.TryAddSingleton<SafeJsonSerializer>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ConsoleOptions>>().Value;
            return new SafeJsonSerializer(opts.MaxPayloadLength, opts.SensitiveFields);
        });
        services.TryAddSingleton<ConsoleLogFormatter>();
        services.TryAddSingleton<JsonLogFormatter>();

        // 注册 HTTP 诊断 DelegatingHandler
        services.TryAddTransient<DiagnosticsLoggingHandler>();

        // 将 DiagnosticsLoggingHandler 添加到所有 HttpClient
        services.ConfigureHttpClientDefaults(builder =>
        {
            builder.AddHttpMessageHandler<DiagnosticsLoggingHandler>();
        });

        // 文件日志
        if (options.File.Enabled)
        {
            services.AddSingleton<FileLogWriter>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
                return new FileLogWriter(opts.File, opts.ServiceName);
            });

            services.AddSingleton<FileLogRotator>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
                return new FileLogRotator(opts.File, opts.ServiceName);
            });
        }

        // 启动信息输出
        services.AddHostedService<StartupLogger>();

        // 抑制 Microsoft / System 基础组件日志噪音
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);

            logging.AddFilter("Microsoft", LogLevel.Warning);
            logging.AddFilter("System", LogLevel.Warning);

            // OpenTelemetry 日志导出
            if (options.OpenTelemetry.Enabled && options.Logging.Enabled)
            {
                var resourceBuilder = BuildResource(options);

                logging.AddOpenTelemetry(otlp =>
                {
                    otlp.SetResourceBuilder(resourceBuilder);
                    otlp.IncludeScopes = true;
                    otlp.IncludeFormattedMessage = true;

                    ConfigureOtlpExporter(otlp, options);
                });
            }
        });

        // OpenTelemetry 追踪
        if (options.OpenTelemetry.Enabled && options.Tracing.Enabled)
        {
            var resourceBuilder = BuildResource(options);

            services
                .AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing.SetResourceBuilder(resourceBuilder);

                    if (options.Tracing.RecordHttpRequests)
                    {
                        tracing.AddAspNetCoreInstrumentation(opts =>
                        {
                            opts.RecordException = true;
                            opts.Filter = context =>
                                !context.Request.Path.StartsWithSegments("/health")
                                && !context.Request.Path.StartsWithSegments("/healthz");
                        });

                        tracing.AddHttpClientInstrumentation(opts =>
                        {
                            opts.RecordException = true;
                        });
                    }

                    if (options.Tracing.SamplingRatio < 1.0)
                    {
                        tracing.SetSampler(new TraceIdRatioBasedSampler(options.Tracing.SamplingRatio));
                    }

                    ConfigureOtlpExporter(tracing, options);
                });
        }

        // OpenTelemetry 指标
        if (options.OpenTelemetry.Enabled && options.Metrics.Enabled)
        {
            var resourceBuilder = BuildResource(options);

            services
                .AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.SetResourceBuilder(resourceBuilder);

                    if (options.Metrics.IncludeAspNetCoreMetrics)
                    {
                        metrics.AddAspNetCoreInstrumentation();
                        metrics.AddHttpClientInstrumentation();
                    }

                    if (options.Metrics.IncludeRuntimeMetrics)
                    {
                        metrics.AddRuntimeInstrumentation();
                    }

                    ConfigureOtlpExporter(metrics, options);
                });
        }

        return services;
    }

    /// <summary>
    /// 启用可观测性中间件（入站 HTTP 诊断日志）
    /// </summary>
    public static IApplicationBuilder UseObservability(this IApplicationBuilder app)
    {
        app.UseMiddleware<DiagnosticsLoggingMiddleware>();
        return app;
    }

    // ────── OpenTelemetry helpers ──────

    private static ResourceBuilder BuildResource(ObservabilityOptions options)
    {
        return ResourceBuilder
            .CreateDefault()
            .AddService(options.ServiceName, serviceVersion: options.ServiceVersion)
            .AddAttributes(
                new Dictionary<string, object>
                {
                    ["deployment.environment"] =
                        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    ["host.name"] = Environment.MachineName,
                });
    }

    private static void ConfigureOtlpExporter(OpenTelemetryLoggerOptions loggerOptions, ObservabilityOptions options)
    {
        var otel = options.OpenTelemetry;
        loggerOptions.AddOtlpExporter(otlp =>
        {
            otlp.Endpoint = new Uri(otel.Endpoint);
            otlp.Protocol = otel.Protocol == "HttpProtobuf"
                ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
                : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;

            if (otel.Headers.Count > 0)
            {
                otlp.Headers = string.Join(",", otel.Headers.Select(h => $"{h.Key}={h.Value}"));
            }
        });
    }

    private static void ConfigureOtlpExporter(TracerProviderBuilder builder, ObservabilityOptions options)
    {
        var otel = options.OpenTelemetry;
        builder.AddOtlpExporter(otlp =>
        {
            otlp.Endpoint = new Uri(otel.Endpoint);
            otlp.Protocol = otel.Protocol == "HttpProtobuf"
                ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
                : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;

            if (otel.Headers.Count > 0)
            {
                otlp.Headers = string.Join(",", otel.Headers.Select(h => $"{h.Key}={h.Value}"));
            }
        });
    }

    private static void ConfigureOtlpExporter(MeterProviderBuilder builder, ObservabilityOptions options)
    {
        var otel = options.OpenTelemetry;
        builder.AddOtlpExporter(
            (otlp, reader) =>
            {
                otlp.Endpoint = new Uri(otel.Endpoint);
                otlp.Protocol = otel.Protocol == "HttpProtobuf"
                    ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
                    : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;

                if (otel.Headers.Count > 0)
                {
                    otlp.Headers = string.Join(",", otel.Headers.Select(h => $"{h.Key}={h.Value}"));
                }

                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                    options.Metrics.CollectionIntervalSeconds * 1000;
            });
    }
}
