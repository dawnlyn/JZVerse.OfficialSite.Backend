using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Configuration;

/// <summary>
/// 服务通信配置
/// </summary>
public sealed record ServiceCommunicationOptions
{
    /// <summary>
    /// 默认超时时间
    /// </summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 默认负载均衡策略
    /// </summary>
    public LoadBalancerStrategy DefaultLoadBalancer { get; init; } = LoadBalancerStrategy.RoundRobin;

    /// <summary>
    /// 默认重试策略
    /// </summary>
    public RetryPolicyOptions DefaultRetry { get; init; } = new();

    /// <summary>
    /// 默认熔断器配置
    /// </summary>
    public CircuitBreakerOptions DefaultCircuitBreaker { get; init; } = new();

    /// <summary>
    /// 是否启用服务发现
    /// </summary>
    public bool EnableServiceDiscovery { get; init; } = true;

    /// <summary>
    /// 服务发现刷新间隔
    /// </summary>
    public TimeSpan ServiceDiscoveryRefreshInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 是否启用链路追踪
    /// </summary>
    public bool EnableTracing { get; init; } = true;

    /// <summary>
    /// 是否启用指标收集
    /// </summary>
    public bool EnableMetrics { get; init; } = true;

    /// <summary>
    /// 服务特定配置
    /// </summary>
    public Dictionary<string, ServiceEndpointOptions> ServiceEndpoints { get; init; } = [];
}

/// <summary>
/// 服务端点配置
/// </summary>
public sealed record ServiceEndpointOptions
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// 直连地址（不使用服务发现时）
    /// </summary>
    public List<string> Addresses { get; init; } = [];

    /// <summary>
    /// 超时时间
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// 负载均衡策略
    /// </summary>
    public LoadBalancerStrategy? LoadBalancer { get; init; }

    /// <summary>
    /// 重试策略
    /// </summary>
    public RetryPolicyOptions? Retry { get; init; }

    /// <summary>
    /// 熔断器配置
    /// </summary>
    public CircuitBreakerOptions? CircuitBreaker { get; init; }

    /// <summary>
    /// 是否启用 TLS
    /// </summary>
    public bool UseTls { get; init; }

    /// <summary>
    /// 传输协议
    /// </summary>
    public ServiceProtocol Protocol { get; init; } = ServiceProtocol.Http;
}

/// <summary>
/// 服务通信协议
/// </summary>
public enum ServiceProtocol
{
    /// <summary>
    /// HTTP/HTTPS
    /// </summary>
    Http,

    /// <summary>
    /// gRPC
    /// </summary>
    Grpc
}
