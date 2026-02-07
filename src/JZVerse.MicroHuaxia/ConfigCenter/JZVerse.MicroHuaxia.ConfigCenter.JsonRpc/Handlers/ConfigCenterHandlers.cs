using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Server;

namespace JZVerse.MicroHuaxia.ConfigCenter.JsonRpc.Handlers;

/// <summary>
/// 获取配置请求参数
/// </summary>
public sealed record GetConfigParams
{
    public required string ApplicationId { get; init; }
    public required string EnvironmentId { get; init; }
    public string? NamespaceId { get; init; }
}

/// <summary>
/// 配置中心 - 获取配置处理器
/// </summary>
public sealed class GetConfigHandler(IConfigDiscovery configDiscovery) : JsonRpcHandler<GetConfigParams, Dictionary<string, string>>
{
    public override string Method => "ConfigCenter.GetConfig";

    protected override async Task<Dictionary<string, string>?> ExecuteAsync(GetConfigParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var query = new ConfigQuery
        {
            ApplicationId = parameters.ApplicationId,
            EnvironmentId = parameters.EnvironmentId,
            NamespaceId = parameters.NamespaceId,
        };

        return await configDiscovery.GetConfigAsync(query, cancellationToken);
    }
}

/// <summary>
/// 获取单个值请求参数
/// </summary>
public sealed record GetValueParams
{
    public required string ApplicationId { get; init; }
    public required string EnvironmentId { get; init; }
    public required string Key { get; init; }
}

/// <summary>
/// 配置中心 - 获取单个值处理器
/// </summary>
public sealed class GetValueHandler(IConfigDiscovery configDiscovery) : JsonRpcHandler<GetValueParams, string?>
{
    public override string Method => "ConfigCenter.GetValue";

    protected override async Task<string?> ExecuteAsync(GetValueParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return await configDiscovery.GetValueAsync(
            parameters.ApplicationId,
            parameters.EnvironmentId,
            parameters.Key,
            cancellationToken);
    }
}

/// <summary>
/// 获取命名空间配置请求参数
/// </summary>
public sealed record GetNamespaceConfigParams
{
    public required string NamespaceId { get; init; }
    public required string EnvironmentId { get; init; }
    public string? ClientId { get; init; }
    public string? IpAddress { get; init; }
    public List<string>? Tags { get; init; }
}

/// <summary>
/// 配置中心 - 获取命名空间配置处理器
/// </summary>
public sealed class GetNamespaceConfigHandler(IConfigDiscovery configDiscovery) : JsonRpcHandler<GetNamespaceConfigParams, Dictionary<string, string>>
{
    public override string Method => "ConfigCenter.GetNamespaceConfig";

    protected override async Task<Dictionary<string, string>?> ExecuteAsync(GetNamespaceConfigParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        ClientInfo? clientInfo = null;
        if (!string.IsNullOrEmpty(parameters.ClientId))
        {
            clientInfo = new ClientInfo
            {
                ClientId = parameters.ClientId,
                IpAddress = parameters.IpAddress,
                Tags = parameters.Tags ?? [],
            };
        }

        return await configDiscovery.GetNamespaceConfigAsync(
            parameters.NamespaceId,
            parameters.EnvironmentId,
            clientInfo,
            cancellationToken);
    }
}

/// <summary>
/// 设置配置请求参数
/// </summary>
public sealed record SetConfigParams
{
    public string? ItemId { get; init; }
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required string NamespaceId { get; init; }
    public required string EnvironmentId { get; init; }
    public string? Comment { get; init; }
    public string ValueType { get; init; } = "String";
}

/// <summary>
/// 配置中心 - 设置配置处理器
/// </summary>
public sealed class SetConfigHandler(IConfigRegistry configRegistry) : JsonRpcHandler<SetConfigParams, ConfigItem>
{
    public override string Method => "ConfigCenter.SetConfig";

    protected override async Task<ConfigItem?> ExecuteAsync(SetConfigParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var item = new ConfigItem
        {
            ItemId = parameters.ItemId ?? Guid.NewGuid().ToString("N"),
            Key = parameters.Key,
            Value = parameters.Value,
            NamespaceId = parameters.NamespaceId,
            EnvironmentId = parameters.EnvironmentId,
            Comment = parameters.Comment,
            ValueType = Enum.TryParse<ConfigValueType>(parameters.ValueType, out var vt) ? vt : ConfigValueType.String,
        };

        return await configRegistry.SetAsync(item, cancellationToken);
    }
}

/// <summary>
/// 删除配置请求参数
/// </summary>
public sealed record DeleteConfigParams
{
    public required string ItemId { get; init; }
}

/// <summary>
/// 配置中心 - 删除配置处理器
/// </summary>
public sealed class DeleteConfigHandler(IConfigRegistry configRegistry) : JsonRpcHandler<DeleteConfigParams, bool>
{
    public override string Method => "ConfigCenter.DeleteConfig";

    protected override async Task<bool> ExecuteAsync(DeleteConfigParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return await configRegistry.DeleteAsync(parameters.ItemId, cancellationToken);
    }
}

/// <summary>
/// 查询配置请求参数
/// </summary>
public sealed record QueryConfigParams
{
    public required string ApplicationId { get; init; }
    public required string EnvironmentId { get; init; }
    public string? NamespaceId { get; init; }
    public List<string>? Keys { get; init; }
}

/// <summary>
/// 配置中心 - 查询配置处理器
/// </summary>
public sealed class QueryConfigHandler(IConfigDiscovery configDiscovery) : JsonRpcHandler<QueryConfigParams, IReadOnlyList<ConfigItem>>
{
    public override string Method => "ConfigCenter.QueryConfig";

    protected override async Task<IReadOnlyList<ConfigItem>?> ExecuteAsync(QueryConfigParams? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var query = new ConfigQuery
        {
            ApplicationId = parameters.ApplicationId,
            EnvironmentId = parameters.EnvironmentId,
            NamespaceId = parameters.NamespaceId,
            Keys = parameters.Keys,
        };

        return await configDiscovery.QueryAsync(query, cancellationToken);
    }
}
