using System.Diagnostics;
using JZVerse.MicroHuaxia.Gateway.Tracing.Configuration;
using JZVerse.MicroHuaxia.Gateway.Tracing.Middleware;
using JZVerse.MicroHuaxia.Gateway.Tracing.Propagation;
using JZVerse.MicroHuaxia.Gateway.Tracing.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace JZVerse.MicroHuaxia.Gateway.Tracing.Extensions;

/// <summary>
/// 网关追踪 DI 扩展方法
/// </summary>
public static class GatewayTracingExtensions
{
    /// <summary>
    /// 添加网关追踪服务（从配置加载）
    /// </summary>
    public static IServiceCollection AddGatewayTracing(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GatewayTracingOptions>(configuration.GetSection(GatewayTracingOptions.SectionName));

        var options =
            configuration.GetSection(GatewayTracingOptions.SectionName).Get<GatewayTracingOptions>()
            ?? new GatewayTracingOptions();

        return services.AddGatewayTracingCore(options);
    }

    /// <summary>
    /// 添加网关追踪服务（通过委托配置）
    /// </summary>
    public static IServiceCollection AddGatewayTracing(
        this IServiceCollection services,
        Action<GatewayTracingOptions>? configure = null
    )
    {
        var options = new GatewayTracingOptions();
        configure?.Invoke(options);

        services.Configure<GatewayTracingOptions>(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.SamplingRatio = options.SamplingRatio;
            opt.PropagationFormat = options.PropagationFormat;
            opt.CustomHeaders = options.CustomHeaders;
            opt.Record = options.Record;
            opt.InMemoryStore = options.InMemoryStore;
            opt.IgnorePaths = options.IgnorePaths;
            opt.AlwaysSampleErrors = options.AlwaysSampleErrors;
            opt.AlwaysSampleSlowRequests = options.AlwaysSampleSlowRequests;
            opt.SlowRequestThresholdMs = options.SlowRequestThresholdMs;
        });

        return services.AddGatewayTracingCore(options);
    }

    private static IServiceCollection AddGatewayTracingCore(
        this IServiceCollection services,
        GatewayTracingOptions options
    )
    {
        if (!options.Enabled)
        {
            return services;
        }

        // 注册传播器
        services.TryAddSingleton<ITracePropagator>(sp => CreatePropagator(options));

        // 注册内存存储（如果启用）
        if (options.InMemoryStore.Enabled)
        {
            services.TryAddSingleton<ITraceStore, InMemoryTraceStore>();
        }

        // 配置 OpenTelemetry 追踪
        services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: GatewayActivitySource.Name,
                    serviceVersion: GatewayActivitySource.Version
                );
            })
            .WithTracing(tracing =>
            {
                // 添加 Gateway ActivitySource
                tracing.AddSource(GatewayActivitySource.Name);

                // 配置采样器
                if (options.SamplingRatio < 1.0)
                {
                    tracing.SetSampler(new TraceIdRatioBasedSampler(options.SamplingRatio));
                }

                // 添加 ASP.NET Core 仪表化
                tracing.AddAspNetCoreInstrumentation(opt =>
                {
                    opt.RecordException = true;
                    opt.Filter = httpContext =>
                    {
                        // 过滤忽略的路径
                        var path = httpContext.Request.Path.Value;
                        if (string.IsNullOrEmpty(path))
                            return true;

                        foreach (var ignorePath in options.IgnorePaths)
                        {
                            if (ignorePath.EndsWith('*'))
                            {
                                var prefix = ignorePath[..^1];
                                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                    return false;
                            }
                            else if (path.Equals(ignorePath, StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }
                        }

                        return true;
                    };
                });

                // 添加 HTTP 客户端仪表化
                tracing.AddHttpClientInstrumentation(opt =>
                {
                    opt.RecordException = true;
                });
            });

        return services;
    }

    /// <summary>
    /// 添加 OTLP 追踪导出器
    /// </summary>
    public static IServiceCollection AddGatewayTracingOtlpExporter(
        this IServiceCollection services,
        string endpoint,
        Action<OpenTelemetry.Exporter.OtlpExporterOptions>? configure = null
    )
    {
        services
            .AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri(endpoint);
                    configure?.Invoke(opt);
                });
            });

        return services;
    }

    /// <summary>
    /// 添加控制台追踪导出器（开发调试用）
    /// </summary>
    public static IServiceCollection AddGatewayTracingConsoleExporter(this IServiceCollection services)
    {
        services.AddOpenTelemetry().WithTracing(tracing => tracing.AddConsoleExporter());

        return services;
    }

    /// <summary>
    /// 使用网关追踪中间件
    /// </summary>
    public static IApplicationBuilder UseGatewayTracing(this IApplicationBuilder app) =>
        app.UseMiddleware<GatewayTracingMiddleware>();

    private static ITracePropagator CreatePropagator(GatewayTracingOptions options) =>
        options.PropagationFormat switch
        {
            TracePropagationFormat.W3C => new W3CTracePropagator(),
            TracePropagationFormat.B3Single => new B3TracePropagator(B3Format.Single),
            TracePropagationFormat.B3Multi => new B3TracePropagator(B3Format.Multi),
            TracePropagationFormat.Custom => new CustomTracePropagator(options.CustomHeaders),
            TracePropagationFormat.Composite => CompositeTracePropagator.CreateDefault(),
            _ => new W3CTracePropagator(),
        };
}
