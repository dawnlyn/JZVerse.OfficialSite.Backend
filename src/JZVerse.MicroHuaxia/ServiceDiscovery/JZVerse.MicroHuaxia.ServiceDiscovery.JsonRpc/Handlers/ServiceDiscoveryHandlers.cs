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
            InstanceId = parameters.InstanceId,
            ServiceName = parameters.ServiceName,
            Version = parameters.Version,
            Host = parameters.Host,
            Port = parameters.Port,
            Scheme = parameters.Scheme,
            BasePath = parameters.BasePath,
            Tags = parameters.Tags ?? [],
            Weight = parameters.Weight,
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
            Tags = parameters?.Tags,
            OnlyHealthy = parameters?.OnlyHealthy ?? true,
            OnlyEnabled = parameters?.OnlyEnabled ?? true,
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
