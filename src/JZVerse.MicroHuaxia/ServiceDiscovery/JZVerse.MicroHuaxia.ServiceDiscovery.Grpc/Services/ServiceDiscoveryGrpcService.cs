using Grpc.Core;
using JZVerse.MicroHuaxia.Protos.ServiceDiscovery;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using Microsoft.Extensions.Logging;
using ProtoHealthStatus = JZVerse.MicroHuaxia.Protos.Common.HealthStatus;
using DomainHealthStatus = JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models.HealthStatus;
using DomainServiceEvent = JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models.ServiceEvent;
using ProtoServiceEvent = JZVerse.MicroHuaxia.Protos.ServiceDiscovery.ServiceEvent;
using DomainHealthCheckConfiguration = JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models.HealthCheckConfiguration;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Grpc.Services;

/// <summary>
/// 服务发现 gRPC 服务实现
/// </summary>
public sealed class ServiceDiscoveryGrpcService(
    IServiceRegistry serviceRegistry,
    IServiceDiscovery serviceDiscovery,
    IServiceEventPublisher eventPublisher,
    ILogger<ServiceDiscoveryGrpcService> logger) : ServiceDiscoveryService.ServiceDiscoveryServiceBase
{
    public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        logger.LogInformation("gRPC Register: {ServiceName}", request.ServiceName);

        var registration = new ServiceRegistration
        {
            InstanceId = string.IsNullOrEmpty(request.InstanceId) ? null : request.InstanceId,
            ServiceName = request.ServiceName,
            Version = request.Version,
            Host = request.Host,
            Port = request.Port,
            Scheme = request.Scheme,
            BasePath = request.BasePath,
            Tags = [.. request.Tags],
            Weight = request.Weight,
            HeartbeatIntervalSeconds = request.HeartbeatIntervalSeconds,
            Metadata = new ServiceMetadata
            {
                Properties = request.Metadata?.Properties?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? [],
            },
            HealthCheck = request.HealthCheck is not null ? new DomainHealthCheckConfiguration
            {
                EnableActiveCheck = request.HealthCheck.EnableActiveCheck,
                ActiveCheckIntervalSeconds = request.HealthCheck.ActiveCheckIntervalSeconds,
                Endpoint = request.HealthCheck.Endpoint,
                TimeoutSeconds = request.HealthCheck.TimeoutSeconds,
                FailureThreshold = request.HealthCheck.FailureThreshold,
                SuccessThreshold = request.HealthCheck.SuccessThreshold,
            } : null,
        };

        var instance = await serviceRegistry.RegisterAsync(registration, context.CancellationToken);

        return new RegisterResponse
        {
            Instance = MapToProto(instance),
        };
    }

    public override async Task<DeregisterResponse> Deregister(DeregisterRequest request, ServerCallContext context)
    {
        logger.LogInformation("gRPC Deregister: {InstanceId}", request.InstanceId);

        var success = await serviceRegistry.DeregisterAsync(request.InstanceId, context.CancellationToken);

        return new DeregisterResponse { Success = success };
    }

    public override async Task<HeartbeatResponse> Heartbeat(HeartbeatRequest request, ServerCallContext context)
    {
        var success = await serviceRegistry.HeartbeatAsync(request.InstanceId, context.CancellationToken);

        return new HeartbeatResponse { Success = success };
    }

    public override async Task<GetInstancesResponse> GetInstances(GetInstancesRequest request, ServerCallContext context)
    {
        var instances = await serviceDiscovery.GetInstancesAsync(request.ServiceName, context.CancellationToken);

        var response = new GetInstancesResponse();
        response.Instances.AddRange(instances.Select(MapToProto));

        return response;
    }

    public override async Task<GetInstanceResponse> GetInstance(GetInstanceRequest request, ServerCallContext context)
    {
        var instance = await serviceDiscovery.GetInstanceAsync(request.ServiceName, context.CancellationToken);

        return new GetInstanceResponse
        {
            Instance = instance is not null ? MapToProto(instance) : null,
            Found = instance is not null,
        };
    }

    public override async Task<DiscoverResponse> Discover(DiscoverRequest request, ServerCallContext context)
    {
        var query = new ServiceQuery
        {
            ServiceName = request.ServiceName,
            Version = request.HasVersion ? request.Version : null,
            Tags = request.Tags.Count > 0 ? [.. request.Tags] : null,
            OnlyHealthy = request.OnlyHealthy,
            OnlyEnabled = request.OnlyEnabled,
            Environment = request.HasEnvironment ? request.Environment : null,
            Region = request.HasRegion ? request.Region : null,
        };

        var instances = await serviceDiscovery.DiscoverAsync(query, context.CancellationToken);

        var response = new DiscoverResponse();
        response.Instances.AddRange(instances.Select(MapToProto));

        return response;
    }

    public override async Task<GetServiceNamesResponse> GetServiceNames(GetServiceNamesRequest request, ServerCallContext context)
    {
        var names = await serviceDiscovery.GetServiceNamesAsync(context.CancellationToken);

        var response = new GetServiceNamesResponse();
        response.ServiceNames.AddRange(names);

        return response;
    }

    public override async Task<UpdateHealthStatusResponse> UpdateHealthStatus(UpdateHealthStatusRequest request, ServerCallContext context)
    {
        var status = MapToDomainHealthStatus(request.Status);
        var success = await serviceRegistry.UpdateHealthStatusAsync(request.InstanceId, status, context.CancellationToken);

        return new UpdateHealthStatusResponse { Success = success };
    }

    public override async Task WatchService(WatchServiceRequest request, IServerStreamWriter<ProtoServiceEvent> responseStream, ServerCallContext context)
    {
        logger.LogInformation("Client started watching service: {ServiceName}", request.ServiceName);

        // 创建事件监听器
        var listener = new StreamingServiceEventListener(request.ServiceName, responseStream, logger);

        // 订阅事件
        eventPublisher.Subscribe(listener);

        try
        {
            // 保持连接直到客户端断开
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 客户端断开连接
        }
        finally
        {
            eventPublisher.Unsubscribe(listener);
            logger.LogInformation("Client stopped watching service: {ServiceName}", request.ServiceName);
        }
    }

    /// <summary>
    /// 流式服务事件监听器
    /// </summary>
    private sealed class StreamingServiceEventListener(
        string serviceName,
        IServerStreamWriter<ProtoServiceEvent> responseStream,
        ILogger logger) : IServiceEventListener
    {
        public async Task OnEventAsync(DomainServiceEvent @event, CancellationToken cancellationToken = default)
        {
            if (@event.Instance.ServiceName != serviceName)
                return;

            var protoEvent = new ProtoServiceEvent
            {
                EventType = @event.EventType switch
                {
                    ServiceEventType.Registered => ProtoServiceEvent.Types.EventType.Registered,
                    ServiceEventType.Deregistered => ProtoServiceEvent.Types.EventType.Deregistered,
                    ServiceEventType.HealthChanged => ProtoServiceEvent.Types.EventType.HealthChanged,
                    ServiceEventType.MetadataUpdated => ProtoServiceEvent.Types.EventType.MetadataUpdated,
                    _ => ProtoServiceEvent.Types.EventType.Unknown,
                },
                Instance = MapToProto(@event.Instance),
                Timestamp = @event.Timestamp.ToUnixTimeMilliseconds(),
            };

            try
            {
                await responseStream.WriteAsync(protoEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to write service event to stream");
            }
        }

        private static Protos.ServiceDiscovery.ServiceInstance MapToProto(Abstractions.Models.ServiceInstance instance)
        {
            var proto = new Protos.ServiceDiscovery.ServiceInstance
            {
                InstanceId = instance.InstanceId,
                ServiceName = instance.ServiceName,
                Version = instance.Version,
                Host = instance.Host,
                Port = instance.Port ?? 0,
                Scheme = instance.Scheme,
                BasePath = instance.BasePath ?? "",
                Health = instance.Health switch
                {
                    DomainHealthStatus.Healthy => ProtoHealthStatus.Healthy,
                    DomainHealthStatus.Unhealthy => ProtoHealthStatus.Unhealthy,
                    DomainHealthStatus.Degraded => ProtoHealthStatus.Degraded,
                    DomainHealthStatus.Starting => ProtoHealthStatus.Starting,
                    DomainHealthStatus.Stopping => ProtoHealthStatus.Stopping,
                    _ => ProtoHealthStatus.Unknown,
                },
                RegisteredAt = instance.RegisteredAt.ToUnixTimeMilliseconds(),
                LastHeartbeatAt = instance.LastHeartbeatAt.ToUnixTimeMilliseconds(),
                Weight = instance.Weight,
                Enabled = instance.Enabled,
            };

            proto.Tags.AddRange(instance.Tags);

            if (instance.Metadata?.Properties is { Count: > 0 })
            {
                proto.Metadata = new Protos.Common.Metadata();
                foreach (var kv in instance.Metadata.Properties)
                {
                    proto.Metadata.Properties.Add(kv.Key, kv.Value);
                }
            }

            return proto;
        }
    }

    private static Protos.ServiceDiscovery.ServiceInstance MapToProto(Abstractions.Models.ServiceInstance instance)
    {
        var proto = new Protos.ServiceDiscovery.ServiceInstance
        {
            InstanceId = instance.InstanceId,
            ServiceName = instance.ServiceName,
            Version = instance.Version,
            Host = instance.Host,
            Port = instance.Port ?? 0,
            Scheme = instance.Scheme,
            BasePath = instance.BasePath ?? "",
            Health = MapToProtoHealthStatus(instance.Health),
            RegisteredAt = instance.RegisteredAt.ToUnixTimeMilliseconds(),
            LastHeartbeatAt = instance.LastHeartbeatAt.ToUnixTimeMilliseconds(),
            Weight = instance.Weight,
            Enabled = instance.Enabled,
        };

        proto.Tags.AddRange(instance.Tags);

        if (instance.Metadata?.Properties is { Count: > 0 })
        {
            proto.Metadata = new Protos.Common.Metadata();
            foreach (var kv in instance.Metadata.Properties)
            {
                proto.Metadata.Properties.Add(kv.Key, kv.Value);
            }
        }

        return proto;
    }

    private static ProtoHealthStatus MapToProtoHealthStatus(DomainHealthStatus status) => status switch
    {
        DomainHealthStatus.Healthy => ProtoHealthStatus.Healthy,
        DomainHealthStatus.Unhealthy => ProtoHealthStatus.Unhealthy,
        DomainHealthStatus.Degraded => ProtoHealthStatus.Degraded,
        DomainHealthStatus.Starting => ProtoHealthStatus.Starting,
        DomainHealthStatus.Stopping => ProtoHealthStatus.Stopping,
        _ => ProtoHealthStatus.Unknown,
    };

    private static DomainHealthStatus MapToDomainHealthStatus(ProtoHealthStatus status) => status switch
    {
        ProtoHealthStatus.Healthy => DomainHealthStatus.Healthy,
        ProtoHealthStatus.Unhealthy => DomainHealthStatus.Unhealthy,
        ProtoHealthStatus.Degraded => DomainHealthStatus.Degraded,
        ProtoHealthStatus.Starting => DomainHealthStatus.Starting,
        ProtoHealthStatus.Stopping => DomainHealthStatus.Stopping,
        _ => DomainHealthStatus.Unknown,
    };
}
