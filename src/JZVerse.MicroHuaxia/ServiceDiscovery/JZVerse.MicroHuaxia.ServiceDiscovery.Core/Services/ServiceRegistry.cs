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

        try
        {
            // 检查是否存在已注销的同 ID 实例，支持重新注册
            var existing = await _repository.GetByIdAsync(instanceId, cancellationToken);
            if (existing != null && existing.IsDeregistered)
            {
                existing.DeregisteredAt = null;
                existing.Enabled = true;
                existing.Health = HealthStatus.Starting;
                existing.LastHeartbeatAt = DateTimeOffset.UtcNow;
                existing.LastHealthCheckAt = DateTimeOffset.UtcNow;
                existing.FailureCount = 0;
                await _repository.UpdateAsync(existing, cancellationToken);

                activity?.SetStatus(ActivityStatusCode.Ok);
                var elapsed = Stopwatch.GetElapsedTime(startTime);
                activity?.SetTag("sd.registration.duration_ms", elapsed.TotalMilliseconds);
                activity?.SetTag("sd.registration.reactivated", true);

                _logger.LogInformation(
                    "已注销服务实例重新注册: {ServiceName}:{InstanceId} 地址: {Address} 耗时: {ElapsedMs:F2}ms",
                    existing.ServiceName,
                    existing.InstanceId,
                    existing.Address,
                    elapsed.TotalMilliseconds
                );

                await _eventPublisher.PublishAsync(
                    new() { EventType = ServiceEventType.Registered, Instance = existing },
                    cancellationToken
                );

                return existing;
            }

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

            await _repository.AddAsync(instance, cancellationToken);

            // 记录追踪信息
            activity?.SetTag("sd.registration.host", instance.Host);
            activity?.SetTag("sd.registration.port", instance.Port);
            activity?.SetTag("sd.registration.scheme", instance.Scheme);
            activity?.SetStatus(ActivityStatusCode.Ok);

            var elapsedNew = Stopwatch.GetElapsedTime(startTime);
            activity?.SetTag("sd.registration.duration_ms", elapsedNew.TotalMilliseconds);

            _logger.LogInformation(
                "服务实例已注册: {ServiceName}:{InstanceId} 地址: {Address} 耗时: {ElapsedMs:F2}ms",
                instance.ServiceName,
                instance.InstanceId,
                instance.Address,
                elapsedNew.TotalMilliseconds
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
            _logger.LogWarning("尝试注销不存在的实例: {InstanceId}", instanceId);
            activity?.SetStatus(ActivityStatusCode.Error, "Instance not found");
            return false;
        }

        activity?.SetTag("service.name", instance.ServiceName);

        // 软删除：标记为已注销而非物理删除
        instance.DeregisteredAt = DateTimeOffset.UtcNow;
        instance.Enabled = false;
        var result = await _repository.UpdateAsync(instance, cancellationToken);

        if (result)
        {
            _logger.LogInformation(
                "服务实例已注销: {ServiceName}:{InstanceId}",
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
            _logger.LogWarning("收到不存在实例的心跳: {InstanceId}", instanceId);
            activity?.SetStatus(ActivityStatusCode.Error, "Instance not found");
            return false;
        }

        // 拒绝已注销实例的心跳
        if (instance.IsDeregistered)
        {
            _logger.LogWarning("收到已注销实例的心跳: {InstanceId}", instanceId);
            activity?.SetStatus(ActivityStatusCode.Error, "Instance deregistered");
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
                "通过心跳恢复服务实例健康状态: {ServiceName}:{InstanceId}",
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
                "服务实例健康状态变更: {ServiceName}:{InstanceId} 从 {OldStatus} 变更为 {NewStatus}",
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
                "服务实例元数据已更新: {ServiceName}:{InstanceId}",
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

    /// <inheritdoc />
    public async Task<bool> PurgeAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _repository.GetByIdAsync(instanceId, cancellationToken);
        if (instance == null || !instance.IsDeregistered)
        {
            _logger.LogWarning("无法清除实例: {InstanceId}（不存在或未注销）", instanceId);
            return false;
        }

        var result = await _repository.RemoveAsync(instanceId, cancellationToken);
        if (result)
        {
            _logger.LogInformation(
                "已永久删除注销实例: {ServiceName}:{InstanceId}",
                instance.ServiceName,
                instance.InstanceId
            );
        }

        return result;
    }

    private static string GenerateInstanceId(ServiceRegistration registration) =>
        $"{registration.ServiceName}-{registration.Host}-{registration.Port}-{Guid.NewGuid().ToString("N")[..8]}";
}
