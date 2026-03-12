using JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Server;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.JsonRpc.Handlers;

/// <summary>
/// 注册服务请求参数
/// </summary>
public sealed record RegisterParams
{
    public string? InstanceId { get; init; }
    public required string ServiceName { get; init; }
    public string Version { get; init; } = "1.0.0";
    public required string Host { get; init; }
    public int? Port { get; init; }
    public string Scheme { get; init; } = "http";
    public string? BasePath { get; init; }
    public List<string>? Tags { get; init; }
    public int Weight { get; init; } = 100;
    public int HeartbeatIntervalSeconds { get; init; } = 30;
    public Dictionary<string, string>? Metadata { get; init; }
    public HealthCheckParams? HealthCheck { get; init; }
}

/// <summary>
/// 健康检查参数
/// </summary>
public sealed record HealthCheckParams
{
    public bool EnableActiveCheck { get; init; } = true;
    public int ActiveCheckIntervalSeconds { get; init; } = 10;
    public string Endpoint { get; init; } = "/health";
    public int TimeoutSeconds { get; init; } = 5;
    public int FailureThreshold { get; init; } = 3;
    public int SuccessThreshold { get; init; } = 2;
}

/// <summary>
/// 服务发现 - 注册服务处理器
/// </summary>
public sealed class RegisterHandler(IServiceRegistry serviceRegistry) : JsonRpcHandler<RegisterParams, ServiceInstance>
{
    public override string Method => "ServiceDiscovery.Register";

    protected override async Task<ServiceInstance?> ExecuteAsync(RegisterParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var registration = new ServiceRegistration
        {
            InstanceId = string.IsNullOrEmpty(parameters.InstanceId) ? null : parameters.InstanceId,
            ServiceName = parameters.ServiceName,
            Version = parameters.Version,
            Host = parameters.Host,
            Port = parameters.Port,
            Scheme = parameters.Scheme,
            BasePath = parameters.BasePath,
            Tags = parameters.Tags ?? [],
            Weight = parameters.Weight,
            HeartbeatIntervalSeconds = parameters.HeartbeatIntervalSeconds,
            Metadata = new ServiceMetadata
            {
                Properties = parameters.Metadata ?? []
            },
            HealthCheck = parameters.HealthCheck is not null ? new HealthCheckConfiguration
            {
                EnableActiveCheck = parameters.HealthCheck.EnableActiveCheck,
                ActiveCheckIntervalSeconds = parameters.HealthCheck.ActiveCheckIntervalSeconds,
                Endpoint = parameters.HealthCheck.Endpoint,
                TimeoutSeconds = parameters.HealthCheck.TimeoutSeconds,
                FailureThreshold = parameters.HealthCheck.FailureThreshold,
                SuccessThreshold = parameters.HealthCheck.SuccessThreshold,
            } : null,
        };

        return await serviceRegistry.RegisterAsync(registration, cancellationToken);
    }
}

/// <summary>
/// 注销服务请求参数
/// </summary>
public sealed record DeregisterParams
{
    public required string InstanceId { get; init; }
}

/// <summary>
/// 服务发现 - 注销服务处理器
/// </summary>
public sealed class DeregisterHandler(IServiceRegistry serviceRegistry) : JsonRpcHandler<DeregisterParams, bool>
{
    public override string Method => "ServiceDiscovery.Deregister";

    protected override async Task<bool> ExecuteAsync(DeregisterParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return await serviceRegistry.DeregisterAsync(parameters.InstanceId, cancellationToken);
    }
}

/// <summary>
/// 心跳请求参数
/// </summary>
public sealed record HeartbeatParams
{
    public required string InstanceId { get; init; }
}

/// <summary>
/// 服务发现 - 心跳处理器
/// </summary>
public sealed class HeartbeatHandler(IServiceRegistry serviceRegistry) : JsonRpcHandler<HeartbeatParams, bool>
{
    public override string Method => "ServiceDiscovery.Heartbeat";

    protected override async Task<bool> ExecuteAsync(HeartbeatParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return await serviceRegistry.HeartbeatAsync(parameters.InstanceId, cancellationToken);
    }
}

/// <summary>
/// 获取实例列表请求参数
/// </summary>
public sealed record GetInstancesParams
{
    public required string ServiceName { get; init; }
}

/// <summary>
/// 服务发现 - 获取实例列表处理器
/// </summary>
public sealed class GetInstancesHandler(IServiceDiscovery serviceDiscovery) : JsonRpcHandler<GetInstancesParams, IReadOnlyList<ServiceInstance>>
{
    public override string Method => "ServiceDiscovery.GetInstances";

    protected override async Task<IReadOnlyList<ServiceInstance>?> ExecuteAsync(GetInstancesParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return await serviceDiscovery.GetInstancesAsync(parameters.ServiceName, cancellationToken);
    }
}

/// <summary>
/// 获取单个实例请求参数
/// </summary>
public sealed record GetInstanceParams
{
    public required string ServiceName { get; init; }
}

/// <summary>
/// 服务发现 - 获取单个实例处理器
/// </summary>
public sealed class GetInstanceHandler(IServiceDiscovery serviceDiscovery) : JsonRpcHandler<GetInstanceParams, ServiceInstance?>
{
    public override string Method => "ServiceDiscovery.GetInstance";

    protected override async Task<ServiceInstance?> ExecuteAsync(GetInstanceParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return await serviceDiscovery.GetInstanceAsync(parameters.ServiceName, cancellationToken);
    }
}

/// <summary>
/// 发现服务请求参数
/// </summary>
public sealed record DiscoverParams
{
    public string? ServiceName { get; init; }
    public string? Version { get; init; }
    public List<string>? Tags { get; init; }
    public bool OnlyHealthy { get; init; } = true;
    public bool OnlyEnabled { get; init; } = true;
    public string? Environment { get; init; }
    public string? Region { get; init; }
}

/// <summary>
/// 服务发现 - 条件查询处理器
/// </summary>
public sealed class DiscoverHandler(IServiceDiscovery serviceDiscovery) : JsonRpcHandler<DiscoverParams, IReadOnlyList<ServiceInstance>>
{
    public override string Method => "ServiceDiscovery.Discover";

    protected override async Task<IReadOnlyList<ServiceInstance>?> ExecuteAsync(DiscoverParams? parameters, CancellationToken cancellationToken)
    {
        var query = new ServiceQuery
        {
            ServiceName = parameters?.ServiceName,
            Version = parameters?.Version,
            Tags = parameters?.Tags is { Count: > 0 } ? parameters.Tags : null,
            OnlyHealthy = parameters?.OnlyHealthy ?? true,
            OnlyEnabled = parameters?.OnlyEnabled ?? true,
            Environment = parameters?.Environment,
            Region = parameters?.Region,
        };

        return await serviceDiscovery.DiscoverAsync(query, cancellationToken);
    }
}

/// <summary>
/// 服务发现 - 获取服务名称列表处理器
/// </summary>
public sealed class GetServiceNamesHandler(IServiceDiscovery serviceDiscovery) : JsonRpcHandler<IReadOnlyList<string>>
{
    public override string Method => "ServiceDiscovery.GetServiceNames";

    protected override async Task<IReadOnlyList<string>?> ExecuteAsync(CancellationToken cancellationToken)
    {
        return await serviceDiscovery.GetServiceNamesAsync(cancellationToken);
    }
}

/// <summary>
/// 更新健康状态请求参数
/// </summary>
public sealed record UpdateHealthStatusParams
{
    public required string InstanceId { get; init; }
    public required HealthStatus Status { get; init; }
}

/// <summary>
/// 服务发现 - 更新健康状态处理器
/// </summary>
public sealed class UpdateHealthStatusHandler(IServiceRegistry serviceRegistry) : JsonRpcHandler<UpdateHealthStatusParams, bool>
{
    public override string Method => "ServiceDiscovery.UpdateHealthStatus";

    protected override async Task<bool> ExecuteAsync(UpdateHealthStatusParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return await serviceRegistry.UpdateHealthStatusAsync(parameters.InstanceId, parameters.Status, cancellationToken);
    }
}
