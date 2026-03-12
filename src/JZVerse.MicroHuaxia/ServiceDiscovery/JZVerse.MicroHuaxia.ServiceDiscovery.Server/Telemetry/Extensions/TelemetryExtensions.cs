using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Configuration;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Dashboard;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Logging;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Metrics;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Extensions;

/// <summary>
/// Telemetry 扩展方法
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// 添加服务发现可观测性
    /// </summary>
    public static IServiceCollection AddServiceDiscoveryTelemetry(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // 绑定配置
        services.Configure<TelemetryOptions>(configuration.GetSection(TelemetryOptions.SectionName));
        var options =
            configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();

        return services.AddServiceDiscoveryTelemetry(options);
    }

    /// <summary>
    /// 添加服务发现可观测性
    /// </summary>
    public static IServiceCollection AddServiceDiscoveryTelemetry(
        this IServiceCollection services,
        Action<TelemetryOptions>? configure = null
    )
    {
        var options = new TelemetryOptions();
        configure?.Invoke(options);
        return services.AddServiceDiscoveryTelemetry(options);
    }

    /// <summary>
    /// 添加服务发现可观测性
    /// </summary>
    public static IServiceCollection AddServiceDiscoveryTelemetry(
        this IServiceCollection services,
        TelemetryOptions options
    )
    {
        if (!options.Enabled)
        {
            return services;
        }

        // 注册内存日志存储
        services.AddSingleton(new InMemoryLogStore(options.Logging.MaxInMemoryLogs));

        // 注册指标收集器
        services.AddSingleton<ServiceDiscoveryMetrics>();

        // 注册 Dashboard 数据提供者
        services.AddSingleton<ITelemetryDataProvider, TelemetryDataProvider>();

        // 配置资源信息
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(options.ServiceName, serviceVersion: options.ServiceVersion)
            .AddAttributes(
                new Dictionary<string, object>
                {
                    ["deployment.environment"] =
                        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    ["host.name"] = Environment.MachineName,
                }
            );

        // 配置日志
        if (options.Logging.Enabled)
        {
            services.AddLogging(logging =>
            {
                // 添加自定义日志提供者 (用于 Dashboard)
                logging.Services.AddSingleton<ILoggerProvider, TelemetryLoggerProvider>();

                // 配置 OpenTelemetry 日志
                logging.AddOpenTelemetry(otlp =>
                {
                    otlp.SetResourceBuilder(resourceBuilder);
                    otlp.IncludeScopes = options.Logging.IncludeScopes;
                    otlp.IncludeFormattedMessage = true;

                    ConfigureOtlpExporter(otlp, options);
                    ConfigureConsoleExporter(otlp, options);
                });
            });
        }

        // 配置追踪
        if (options.Tracing.Enabled)
        {
            services
                .AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing.SetResourceBuilder(resourceBuilder);

                    // 添加服务发现活动源
                    tracing.AddSource(ServiceDiscoveryActivitySource.Name);

                    // 添加 ASP.NET Core 追踪
                    if (options.Tracing.RecordHttpRequests)
                    {
                        tracing.AddAspNetCoreInstrumentation(opts =>
                        {
                            opts.RecordException = true;
                            opts.Filter = context =>
                                !context.Request.Path.StartsWithSegments("/health")
                                && !context.Request.Path.StartsWithSegments("/api/v1/health")
                                && !context.Request.Path.StartsWithSegments("/api/v1/alive");
                        });

                        tracing.AddHttpClientInstrumentation(opts =>
                        {
                            opts.RecordException = true;
                        });
                    }

                    // 配置采样
                    if (options.Tracing.SamplingRatio < 1.0)
                    {
                        tracing.SetSampler(new TraceIdRatioBasedSampler(options.Tracing.SamplingRatio));
                    }

                    ConfigureOtlpExporter(tracing, options);
                    ConfigureConsoleExporter(tracing, options);
                });
        }

        // 配置指标
        if (options.Metrics.Enabled)
        {
            services
                .AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.SetResourceBuilder(resourceBuilder);

                    // 添加服务发现指标收集器
                    metrics.AddMeter(MetricNames.MeterName);

                    // 添加 ASP.NET Core 指标
                    if (options.Metrics.IncludeAspNetCoreMetrics)
                    {
                        metrics.AddAspNetCoreInstrumentation();
                        metrics.AddHttpClientInstrumentation();
                    }

                    // 添加运行时指标
                    if (options.Metrics.IncludeRuntimeMetrics)
                    {
                        metrics.AddRuntimeInstrumentation();
                    }

                    ConfigureOtlpExporter(metrics, options);
                    ConfigureConsoleExporter(metrics, options);
                });
        }

        return services;
    }

    private static void ConfigureOtlpExporter(OpenTelemetryLoggerOptions loggerOptions, TelemetryOptions options)
    {
        if (options.OtlpExporter != null)
        {
            loggerOptions.AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new(options.OtlpExporter.Endpoint);
                otlp.Protocol =
                    options.OtlpExporter.Protocol == "HttpProtobuf"
                        ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
                        : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;

                if (options.OtlpExporter.Headers.Count > 0)
                {
                    otlp.Headers = string.Join(",", options.OtlpExporter.Headers.Select(h => $"{h.Key}={h.Value}"));
                }
            });
        }
    }

    private static void ConfigureOtlpExporter(TracerProviderBuilder builder, TelemetryOptions options)
    {
        if (options.OtlpExporter != null)
        {
            builder.AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new(options.OtlpExporter.Endpoint);
                otlp.Protocol =
                    options.OtlpExporter.Protocol == "HttpProtobuf"
                        ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
                        : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;

                if (options.OtlpExporter.Headers.Count > 0)
                {
                    otlp.Headers = string.Join(",", options.OtlpExporter.Headers.Select(h => $"{h.Key}={h.Value}"));
                }
            });
        }
    }

    private static void ConfigureOtlpExporter(MeterProviderBuilder builder, TelemetryOptions options)
    {
        if (options.OtlpExporter != null)
        {
            builder.AddOtlpExporter(
                (otlp, reader) =>
                {
                    otlp.Endpoint = new(options.OtlpExporter.Endpoint);
                    otlp.Protocol =
                        options.OtlpExporter.Protocol == "HttpProtobuf"
                            ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
                            : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;

                    if (options.OtlpExporter.Headers.Count > 0)
                    {
                        otlp.Headers = string.Join(",", options.OtlpExporter.Headers.Select(h => $"{h.Key}={h.Value}"));
                    }

                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                        options.Metrics.CollectionIntervalSeconds * 1000;
                }
            );
        }
    }

    private static void ConfigureConsoleExporter(OpenTelemetryLoggerOptions loggerOptions, TelemetryOptions options)
    {
        if (options.Console is { Enabled: true, Logs: true })
        {
            loggerOptions.AddConsoleExporter();
        }
    }

    private static void ConfigureConsoleExporter(TracerProviderBuilder builder, TelemetryOptions options)
    {
        if (options.Console is { Enabled: true, Traces: true })
        {
            builder.AddConsoleExporter();
        }
    }

    private static void ConfigureConsoleExporter(MeterProviderBuilder builder, TelemetryOptions options)
    {
        if (options.Console is { Enabled: true, Metrics: true })
        {
            builder.AddConsoleExporter();
        }
    }
}
