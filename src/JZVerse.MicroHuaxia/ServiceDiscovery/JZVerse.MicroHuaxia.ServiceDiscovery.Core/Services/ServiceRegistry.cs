using System.Diagnostics;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Core.Services;

/// <summary>
/// 服务注册实现
/// </summary>
public class ServiceRegistry(
    IServiceInstanceRepository _repository,
    IServiceEventPublisher _eventPublisher,
    ILogger<ServiceRegistry> _logger
) : IServiceRegistry
{
    // 可选的追踪活动源
    private static readonly ActivitySource? _activitySource;

    static ServiceRegistry()
    {
        // 尝试加载追踪活动源（如果 Telemetry 模块可用）
        try
        {
            _activitySource = new("JZVerse.MicroHuaxia.ServiceDiscovery", "1.0.0");
        }
        catch
        {
            // 追踪不可用，忽略
        }
    }

    /// <inheritdoc />
    public async Task<ServiceInstance> RegisterAsync(
        ServiceRegistration registration,
        CancellationToken cancellationToken = default
    )
    {
        // 生成实例 ID
        var instanceId = registration.InstanceId ?? GenerateInstanceId(registration);

        // 开始追踪
        using var activity = _activitySource?.StartActivity(
            $"{nameof(ServiceDiscovery)}.Register",
            ActivityKind.Server
        );
        activity?.SetTag("service.name", registration.ServiceName);
        activity?.SetTag("service.instance.id", instanceId);
        activity?.SetTag("sd.operation", "Register");

        var startTime = Stopwatch.GetTimestamp();

        var instance = new ServiceInstance
        {
            InstanceId = instanceId,
            ServiceName = registration.ServiceName,
            Version = registration.Version,
            Host = registration.Host,
            Port = registration.Port,
            Scheme = registration.Scheme,
            BasePath = registration.BasePath,
            Tags = [.. registration.Tags],
            Metadata = registration.Metadata,
            Weight = registration.Weight,
            Health = HealthStatus.Starting,
            RegisteredAt = DateTimeOffset.UtcNow,
            LastHeartbeatAt = DateTimeOffset.UtcNow,
            LastHealthCheckAt = DateTimeOffset.UtcNow,
        };

        try
        {
            await _repository.AddAsync(instance, cancellationToken);

            // 记录追踪信息
            activity?.SetTag("sd.registration.host", instance.Host);
            activity?.SetTag("sd.registration.port", instance.Port);
            activity?.SetTag("sd.registration.scheme", instance.Scheme);
            activity?.SetStatus(ActivityStatusCode.Ok);

            var elapsed = Stopwatch.GetElapsedTime(startTime);
            activity?.SetTag("sd.registration.duration_ms", elapsed.TotalMilliseconds);

            _logger.LogInformation(
                "Service instance registered: {ServiceName}:{InstanceId} at {Address} in {ElapsedMs:F2}ms",
                instance.ServiceName,
                instance.InstanceId,
                instance.Address,
                elapsed.TotalMilliseconds
            );

            // 发布事件
            await _eventPublisher.PublishAsync(
                new() { EventType = ServiceEventType.Registered, Instance = instance },
                cancellationToken
            );

            return instance;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeregisterAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource?.StartActivity(
            $"{nameof(ServiceDiscovery)}.Deregister",
            ActivityKind.Server
        );
        activity?.SetTag("service.instance.id", instanceId);
        activity?.SetTag("sd.operation", "Deregister");

        var instance = await _repository.GetByIdAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            _logger.LogWarning("Attempted to deregister non-existent instance: {InstanceId}", instanceId);
            activity?.SetStatus(ActivityStatusCode.Error, "Instance not found");
            return false;
        }

        activity?.SetTag("service.name", instance.ServiceName);

        var result = await _repository.RemoveAsync(instanceId, cancellationToken);

        if (result)
        {
            _logger.LogInformation(
                "Service instance deregistered: {ServiceName}:{InstanceId}",
                instance.ServiceName,
                instance.InstanceId
            );

            activity?.SetStatus(ActivityStatusCode.Ok);

            await _eventPublisher.PublishAsync(
                new() { EventType = ServiceEventType.Deregistered, Instance = instance },
                cancellationToken
            );
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> HeartbeatAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource?.StartActivity(
            $"{nameof(ServiceDiscovery)}.Heartbeat",
            ActivityKind.Server
        );
        activity?.SetTag("service.instance.id", instanceId);
        activity?.SetTag("sd.operation", "Heartbeat");

        var instance = await _repository.GetByIdAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            _logger.LogWarning("Heartbeat received for non-existent instance: {InstanceId}", instanceId);
            activity?.SetStatus(ActivityStatusCode.Error, "Instance not found");
            return false;
        }

        activity?.SetTag("service.name", instance.ServiceName);

        var oldHealth = instance.Health;
        activity?.SetTag("sd.heartbeat.previous_status", oldHealth.ToString());

        // 更新心跳时间
        instance.LastHeartbeatAt = DateTimeOffset.UtcNow;

        // 如果之前是不健康或未知状态，恢复为健康
        if (instance.Health is HealthStatus.Unhealthy or HealthStatus.Unknown or HealthStatus.Starting)
        {
            instance.FailureCount = 0;
            instance.Health = HealthStatus.Healthy;
        }

        activity?.SetTag("sd.heartbeat.new_status", instance.Health.ToString());

        var result = await _repository.UpdateAsync(instance, cancellationToken);

        if (result && oldHealth != instance.Health)
        {
            _logger.LogInformation(
                "Service instance health restored via heartbeat: {ServiceName}:{InstanceId}",
                instance.ServiceName,
                instance.InstanceId
            );

            await _eventPublisher.PublishAsync(
                new()
                {
                    EventType = ServiceEventType.HealthChanged,
                    Instance = instance,
                    AdditionalData = new() { ["OldHealth"] = oldHealth, ["NewHealth"] = instance.Health },
                },
                cancellationToken
            );
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateHealthStatusAsync(
        string instanceId,
        HealthStatus status,
        CancellationToken cancellationToken = default
    )
    {
        var instance = await _repository.GetByIdAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            return false;
        }

        var oldStatus = instance.Health;
        instance.Health = status;
        instance.LastHealthCheckAt = DateTimeOffset.UtcNow;

        if (status == HealthStatus.Unhealthy)
        {
            instance.FailureCount++;
        }
        else if (status == HealthStatus.Healthy)
        {
            instance.FailureCount = 0;
        }

        var result = await _repository.UpdateAsync(instance, cancellationToken);

        if (result && oldStatus != status)
        {
            _logger.LogInformation(
                "Service instance health changed: {ServiceName}:{InstanceId} from {OldStatus} to {NewStatus}",
                instance.ServiceName,
                instance.InstanceId,
                oldStatus,
                status
            );

            await _eventPublisher.PublishAsync(
                new()
                {
                    EventType = ServiceEventType.HealthChanged,
                    Instance = instance,
                    AdditionalData = new() { ["OldHealth"] = oldStatus, ["NewHealth"] = status },
                },
                cancellationToken
            );
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateMetadataAsync(
        string instanceId,
        ServiceMetadata metadata,
        CancellationToken cancellationToken = default
    )
    {
        var instance = await _repository.GetByIdAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            return false;
        }

        // 创建更新后的实例
        var updatedInstance = new ServiceInstance
        {
            InstanceId = instance.InstanceId,
            ServiceName = instance.ServiceName,
            Version = instance.Version,
            Host = instance.Host,
            Port = instance.Port,
            Scheme = instance.Scheme,
            BasePath = instance.BasePath,
            Tags = instance.Tags,
            Metadata = metadata,
            Health = instance.Health,
            RegisteredAt = instance.RegisteredAt,
            LastHeartbeatAt = instance.LastHeartbeatAt,
            LastHealthCheckAt = instance.LastHealthCheckAt,
            FailureCount = instance.FailureCount,
            Weight = instance.Weight,
            Enabled = instance.Enabled,
        };

        var result = await _repository.UpdateAsync(updatedInstance, cancellationToken);

        if (result)
        {
            _logger.LogInformation(
                "Service instance metadata updated: {ServiceName}:{InstanceId}",
                instance.ServiceName,
                instance.InstanceId
            );

            await _eventPublisher.PublishAsync(
                new() { EventType = ServiceEventType.MetadataUpdated, Instance = updatedInstance },
                cancellationToken
            );
        }

        return result;
    }

    private static string GenerateInstanceId(ServiceRegistration registration) =>
        $"{registration.ServiceName}-{registration.Host}-{registration.Port}-{Guid.NewGuid().ToString("N")[..8]}";
}
