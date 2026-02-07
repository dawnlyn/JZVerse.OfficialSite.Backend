using Grpc.Core;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using JZVerse.MicroHuaxia.Protos.ConfigCenter;
using Microsoft.Extensions.Logging;
using ProtoConfigItem = JZVerse.MicroHuaxia.Protos.ConfigCenter.ConfigItem;
using DomainConfigItem = JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models.ConfigItem;
using DomainClientInfo = JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models.ClientInfo;
using DomainGrayRelease = JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models.GrayRelease;

namespace JZVerse.MicroHuaxia.ConfigCenter.Grpc.Services;

/// <summary>
/// 配置中心 gRPC 服务实现
/// </summary>
public sealed class ConfigCenterGrpcService(
    IConfigRegistry configRegistry,
    IConfigDiscovery configDiscovery,
    IGrayReleaseManager grayReleaseManager,
    ILogger<ConfigCenterGrpcService> logger) : ConfigCenterService.ConfigCenterServiceBase
{
    public override async Task<GetConfigResponse> GetConfig(GetConfigRequest request, ServerCallContext context)
    {
        logger.LogInformation("gRPC GetConfig: {ApplicationId}/{EnvironmentId}", request.ApplicationId, request.EnvironmentId);

        var query = new ConfigQuery
        {
            ApplicationId = request.ApplicationId,
            EnvironmentId = request.EnvironmentId,
            NamespaceId = request.HasNamespaceId ? request.NamespaceId : null,
        };

        var config = await configDiscovery.GetConfigAsync(query, context.CancellationToken);

        var response = new GetConfigResponse();
        foreach (var kv in config)
        {
            response.Config.Add(kv.Key, kv.Value);
        }

        return response;
    }

    public override async Task<GetValueResponse> GetValue(GetValueRequest request, ServerCallContext context)
    {
        var value = await configDiscovery.GetValueAsync(
            request.ApplicationId,
            request.EnvironmentId,
            request.Key,
            context.CancellationToken);

        return new GetValueResponse
        {
            Value = value ?? "",
            Found = value is not null,
        };
    }

    public override async Task<GetNamespaceConfigResponse> GetNamespaceConfig(GetNamespaceConfigRequest request, ServerCallContext context)
    {
        DomainClientInfo? clientInfo = null;
        if (request.ClientInfo is not null)
        {
            clientInfo = new DomainClientInfo
            {
                ClientId = request.ClientInfo.ClientId,
                IpAddress = request.ClientInfo.IpAddress,
                Tags = [.. request.ClientInfo.Tags],
            };
        }

        var config = await configDiscovery.GetNamespaceConfigAsync(
            request.NamespaceId,
            request.EnvironmentId,
            clientInfo,
            context.CancellationToken);

        var response = new GetNamespaceConfigResponse();
        foreach (var kv in config)
        {
            response.Config.Add(kv.Key, kv.Value);
        }

        return response;
    }

    public override async Task<QueryConfigResponse> QueryConfig(QueryConfigRequest request, ServerCallContext context)
    {
        var query = new ConfigQuery
        {
            ApplicationId = request.ApplicationId,
            EnvironmentId = request.EnvironmentId,
            NamespaceId = request.HasNamespaceId ? request.NamespaceId : null,
            Keys = request.Keys.Count > 0 ? [.. request.Keys] : null,
            IncludeSecrets = request.IncludeSecrets,
        };

        var items = await configDiscovery.QueryAsync(query, context.CancellationToken);

        var response = new QueryConfigResponse();
        response.Items.AddRange(items.Select(MapToProto));

        return response;
    }

    public override async Task<UpsertConfigResponse> UpsertConfig(UpsertConfigRequest request, ServerCallContext context)
    {
        var item = MapToDomain(request.Item);
        var result = await configRegistry.SetAsync(item, context.CancellationToken);

        return new UpsertConfigResponse
        {
            Success = true,
            Item = MapToProto(result),
        };
    }

    public override async Task<DeleteConfigResponse> DeleteConfig(DeleteConfigRequest request, ServerCallContext context)
    {
        var success = await configRegistry.DeleteAsync(request.ItemId, context.CancellationToken);

        return new DeleteConfigResponse { Success = success };
    }

    public override async Task<BatchSetConfigResponse> BatchSetConfig(BatchSetConfigRequest request, ServerCallContext context)
    {
        var items = request.Items.Select(MapToDomain).ToList();
        var success = await configRegistry.BatchSetAsync(items, context.CancellationToken);

        return new BatchSetConfigResponse
        {
            Success = success,
            Count = items.Count,
        };
    }

    public override async Task<CreateGrayReleaseResponse> CreateGrayRelease(CreateGrayReleaseRequest request, ServerCallContext context)
    {
        var release = new DomainGrayRelease
        {
            ReleaseId = Guid.NewGuid().ToString("N"),
            ReleaseName = request.ReleaseName,
            NamespaceId = request.NamespaceId,
            EnvironmentId = request.EnvironmentId,
            Strategy = Enum.TryParse<GrayReleaseStrategy>(request.Strategy, out var strategy)
                ? strategy
                : GrayReleaseStrategy.Manual,
            TargetRules = request.Rules.Select(r => new GrayReleaseRule
            {
                RuleType = r.Type switch
                {
                    GrayRule.Types.RuleType.Ip => GrayRuleType.IP,
                    GrayRule.Types.RuleType.Tag => GrayRuleType.Tag,
                    GrayRule.Types.RuleType.ClientId => GrayRuleType.ClientId,
                    GrayRule.Types.RuleType.Percentage => GrayRuleType.Percentage,
                    _ => GrayRuleType.IP,
                },
                MatchPattern = r.MatchPattern,
                Priority = r.Priority,
            }).ToList(),
            Status = GrayReleaseStatus.Draft,
        };

        var created = await grayReleaseManager.CreateReleaseAsync(release, context.CancellationToken);

        return new CreateGrayReleaseResponse
        {
            Release = MapToProto(created),
        };
    }

    public override async Task<StartGrayReleaseResponse> StartGrayRelease(StartGrayReleaseRequest request, ServerCallContext context)
    {
        var success = await grayReleaseManager.StartReleaseAsync(request.ReleaseId, context.CancellationToken);

        return new StartGrayReleaseResponse { Success = success };
    }

    public override async Task<CompleteGrayReleaseResponse> CompleteGrayRelease(CompleteGrayReleaseRequest request, ServerCallContext context)
    {
        var success = await grayReleaseManager.CompleteReleaseAsync(request.ReleaseId, context.CancellationToken);

        return new CompleteGrayReleaseResponse { Success = success };
    }

    public override async Task<RollbackGrayReleaseResponse> RollbackGrayRelease(RollbackGrayReleaseRequest request, ServerCallContext context)
    {
        var success = await grayReleaseManager.RollbackReleaseAsync(request.ReleaseId, context.CancellationToken);

        return new RollbackGrayReleaseResponse { Success = success };
    }

    public override async Task<MatchGrayReleaseResponse> MatchGrayRelease(MatchGrayReleaseRequest request, ServerCallContext context)
    {
        var clientInfo = new DomainClientInfo
        {
            ClientId = request.ClientInfo.ClientId,
            IpAddress = request.ClientInfo.IpAddress,
            Tags = [.. request.ClientInfo.Tags],
        };

        var matches = await grayReleaseManager.MatchesGrayRuleAsync(request.ReleaseId, clientInfo, context.CancellationToken);

        return new MatchGrayReleaseResponse
        {
            Matches = matches,
            ReleaseId = request.ReleaseId,
        };
    }

    private static ProtoConfigItem MapToProto(DomainConfigItem item) => new()
    {
        ItemId = item.ItemId,
        Key = item.Key,
        Value = item.Value,
        ValueType = item.ValueType.ToString(),
        NamespaceId = item.NamespaceId,
        EnvironmentId = item.EnvironmentId,
        Version = item.Version,
        Comment = item.Comment ?? "",
        IsSecret = item.IsSecret,
        IsRequired = item.IsRequired,
        CreatedAt = item.CreatedAt.ToUnixTimeMilliseconds(),
        UpdatedAt = item.UpdatedAt.ToUnixTimeMilliseconds(),
        CreatedBy = item.CreatedBy ?? "",
        UpdatedBy = item.UpdatedBy ?? "",
    };

    private static DomainConfigItem MapToDomain(ProtoConfigItem item) => new()
    {
        ItemId = item.ItemId,
        Key = item.Key,
        Value = item.Value,
        ValueType = Enum.TryParse<ConfigValueType>(item.ValueType, out var vt) ? vt : ConfigValueType.String,
        NamespaceId = item.NamespaceId,
        EnvironmentId = item.EnvironmentId,
        Version = item.Version,
        Comment = string.IsNullOrEmpty(item.Comment) ? null : item.Comment,
        IsSecret = item.IsSecret,
        IsRequired = item.IsRequired,
    };

    private static Protos.ConfigCenter.GrayRelease MapToProto(DomainGrayRelease release)
    {
        var proto = new Protos.ConfigCenter.GrayRelease
        {
            ReleaseId = release.ReleaseId,
            ReleaseName = release.ReleaseName,
            NamespaceId = release.NamespaceId,
            EnvironmentId = release.EnvironmentId,
            Strategy = release.Strategy.ToString(),
            Status = release.Status.ToString(),
            RolloutPercentage = release.RolloutPercentage,
            CreatedAt = release.CreatedAt.ToUnixTimeMilliseconds(),
        };

        if (release.StartedAt.HasValue)
        {
            proto.StartedAt = release.StartedAt.Value.ToUnixTimeMilliseconds();
        }

        if (release.CompletedAt.HasValue)
        {
            proto.CompletedAt = release.CompletedAt.Value.ToUnixTimeMilliseconds();
        }

        proto.Rules.AddRange(release.TargetRules.Select(r => new GrayRule
        {
            Type = r.RuleType switch
            {
                GrayRuleType.IP => GrayRule.Types.RuleType.Ip,
                GrayRuleType.Tag => GrayRule.Types.RuleType.Tag,
                GrayRuleType.ClientId => GrayRule.Types.RuleType.ClientId,
                GrayRuleType.Percentage => GrayRule.Types.RuleType.Percentage,
                _ => GrayRule.Types.RuleType.Unknown,
            },
            MatchPattern = r.MatchPattern,
            Priority = r.Priority,
        }));

        foreach (var kv in release.ConfigSnapshot)
        {
            proto.ConfigSnapshot.Add(kv.Key, kv.Value);
        }

        return proto;
    }
}
