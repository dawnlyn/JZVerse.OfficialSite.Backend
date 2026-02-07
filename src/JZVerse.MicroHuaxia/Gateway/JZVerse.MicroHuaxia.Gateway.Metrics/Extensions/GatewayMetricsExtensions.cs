using JZVerse.MicroHuaxia.Gateway.Metrics.Api;
using JZVerse.MicroHuaxia.Gateway.Metrics.Configuration;
using JZVerse.MicroHuaxia.Gateway.Metrics.Middleware;
using JZVerse.MicroHuaxia.Gateway.Metrics.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;

namespace JZVerse.MicroHuaxia.Gateway.Metrics.Extensions;

/// <summary>
/// 网关指标 DI 扩展方法
/// </summary>
public static class GatewayMetricsExtensions
{
    /// <summary>
    /// 添加网关指标服务（从配置加载）
    /// </summary>
    public static IServiceCollection AddGatewayMetrics(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var options = new GatewayMetricsOptions();
        configuration.GetSection(GatewayMetricsOptions.SectionName).Bind(options);

        return services.AddGatewayMetrics(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.CollectRequestMetrics = options.CollectRequestMetrics;
            opt.CollectAuthMetrics = options.CollectAuthMetrics;
            opt.CollectRateLimitMetrics = options.CollectRateLimitMetrics;
            opt.CollectCacheMetrics = options.CollectCacheMetrics;
            opt.CollectForwardMetrics = options.CollectForwardMetrics;
            opt.CollectResilienceMetrics = options.CollectResilienceMetrics;
            opt.InMemoryStore = options.InMemoryStore;
            opt.EnableOtlpExporter = options.EnableOtlpExporter;
            opt.OtlpEndpoint = options.OtlpEndpoint;
            opt.OtlpProtocol = options.OtlpProtocol;
            opt.EnablePrometheusExporter = options.EnablePrometheusExporter;
            opt.PrometheusEndpoint = options.PrometheusEndpoint;
            opt.EnableConsoleExporter = options.EnableConsoleExporter;
            opt.EnableHttpApi = options.EnableHttpApi;
            opt.IgnorePaths = options.IgnorePaths;
            opt.IncludeAspNetCoreMetrics = options.IncludeAspNetCoreMetrics;
            opt.IncludeHttpClientMetrics = options.IncludeHttpClientMetrics;
            opt.IncludeRuntimeMetrics = options.IncludeRuntimeMetrics;
            opt.ExportIntervalSeconds = options.ExportIntervalSeconds;
        });
    }

    /// <summary>
    /// 添加网关指标服务（通过委托配置）
    /// </summary>
    public static IServiceCollection AddGatewayMetrics(
        this IServiceCollection services,
        Action<GatewayMetricsOptions>? configure = null
    )
    {
        var options = new GatewayMetricsOptions();
        configure?.Invoke(options);

        if (!options.Enabled)
        {
            return services;
        }

        // 注册配置选项
        services.Configure<GatewayMetricsOptions>(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.CollectRequestMetrics = options.CollectRequestMetrics;
            opt.CollectAuthMetrics = options.CollectAuthMetrics;
            opt.CollectRateLimitMetrics = options.CollectRateLimitMetrics;
            opt.CollectCacheMetrics = options.CollectCacheMetrics;
            opt.CollectForwardMetrics = options.CollectForwardMetrics;
            opt.CollectResilienceMetrics = options.CollectResilienceMetrics;
            opt.InMemoryStore = options.InMemoryStore;
            opt.EnableOtlpExporter = options.EnableOtlpExporter;
            opt.OtlpEndpoint = options.OtlpEndpoint;
            opt.OtlpProtocol = options.OtlpProtocol;
            opt.EnablePrometheusExporter = options.EnablePrometheusExporter;
            opt.PrometheusEndpoint = options.PrometheusEndpoint;
            opt.EnableConsoleExporter = options.EnableConsoleExporter;
            opt.EnableHttpApi = options.EnableHttpApi;
            opt.IgnorePaths = options.IgnorePaths;
            opt.IncludeAspNetCoreMetrics = options.IncludeAspNetCoreMetrics;
            opt.IncludeHttpClientMetrics = options.IncludeHttpClientMetrics;
            opt.IncludeRuntimeMetrics = options.IncludeRuntimeMetrics;
            opt.ExportIntervalSeconds = options.ExportIntervalSeconds;
        });

        services.Configure<InMemoryMetricsStoreOptions>(opt =>
        {
            opt.Enabled = options.InMemoryStore.Enabled;
            opt.MaxDataPoints = options.InMemoryStore.MaxDataPoints;
            opt.RetentionMinutes = options.InMemoryStore.RetentionMinutes;
            opt.BucketSizeSeconds = options.InMemoryStore.BucketSizeSeconds;
            opt.CleanupIntervalMinutes = options.InMemoryStore.CleanupIntervalMinutes;
            opt.EnableAggregation = options.InMemoryStore.EnableAggregation;
            opt.AggregationWindowSeconds = options.InMemoryStore.AggregationWindowSeconds;
        });

        // 注册指标收集器
        services.TryAddSingleton<GatewayMetrics>();

        // 注册内存存储
        if (options.InMemoryStore.Enabled)
        {
            services.TryAddSingleton<IMetricsStore, InMemoryMetricsStore>();
        }

        // 注册 API 数据提供者
        if (options.EnableHttpApi)
        {
            services.TryAddSingleton<IGatewayMetricsProvider, GatewayMetricsProvider>();
        }

        // 配置 OpenTelemetry Metrics
        services
            .AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                // 添加网关指标 Meter
                metrics.AddMeter(GatewayMetricNames.MeterName);

                // 添加 ASP.NET Core 仪表化
                if (options.IncludeAspNetCoreMetrics)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                // 添加 HTTP 客户端仪表化
                if (options.IncludeHttpClientMetrics)
                {
                    metrics.AddHttpClientInstrumentation();
                }

                // 添加运行时仪表化
                if (options.IncludeRuntimeMetrics)
                {
                    metrics.AddRuntimeInstrumentation();
                }

                // 配置 OTLP 导出器
                if (options.EnableOtlpExporter && !string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(
                        (otlp, reader) =>
                        {
                            otlp.Endpoint = new Uri(options.OtlpEndpoint);
                            otlp.Protocol =
                                options.OtlpProtocol == "HttpProtobuf"
                                    ? OtlpExportProtocol.HttpProtobuf
                                    : OtlpExportProtocol.Grpc;

                            reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                                options.ExportIntervalSeconds * 1000;
                        }
                    );
                }

                // 配置 Prometheus 导出器
                if (options.EnablePrometheusExporter)
                {
                    metrics.AddPrometheusExporter();
                }

                // 配置控制台导出器（仅开发调试）
                if (options.EnableConsoleExporter)
                {
                    metrics.AddConsoleExporter(
                        (_, reader) =>
                        {
                            reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                                options.ExportIntervalSeconds * 1000;
                        }
                    );
                }
            });

        return services;
    }

    /// <summary>
    /// 添加 OTLP 指标导出器
    /// </summary>
    public static IServiceCollection AddGatewayMetricsOtlpExporter(
        this IServiceCollection services,
        string endpoint,
        Action<OtlpExporterOptions>? configure = null
    )
    {
        services
            .AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(endpoint);
                    configure?.Invoke(otlp);
                });
            });

        return services;
    }

    /// <summary>
    /// 添加 Prometheus 指标导出器
    /// </summary>
    public static IServiceCollection AddGatewayMetricsPrometheusExporter(this IServiceCollection services)
    {
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddPrometheusExporter());

        return services;
    }

    /// <summary>
    /// 添加控制台指标导出器（仅用于开发调试）
    /// </summary>
    public static IServiceCollection AddGatewayMetricsConsoleExporter(
        this IServiceCollection services,
        int exportIntervalSeconds = 10
    )
    {
        services
            .AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddConsoleExporter(
                    (_, reader) =>
                    {
                        reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                            exportIntervalSeconds * 1000;
                    }
                );
            });

        return services;
    }

    /// <summary>
    /// 使用网关指标中间件
    /// </summary>
    public static IApplicationBuilder UseGatewayMetrics(this IApplicationBuilder app)
    {
        // 注册指标收集中间件（应放在管道最外层）
        app.UseMiddleware<GatewayMetricsMiddleware>();

        // 启用 Prometheus 端点
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        return app;
    }
}
