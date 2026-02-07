using System.Diagnostics;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Tracing;

/// <summary>
/// 服务发现活动源
/// </summary>
public static class ServiceDiscoveryActivitySource
{
    public const string Name = "JZVerse.MicroHuaxia.ServiceDiscovery";
    public const string Version = "1.0.0";

    public static ActivitySource Instance { get; } = new(Name, Version);

    /// <summary>
    /// 开始服务注册追踪
    /// </summary>
    public static Activity? StartRegisterActivity(string serviceName, string instanceId)
    {
        var activity = Instance.StartActivity($"{nameof(ServiceDiscovery)}.Register", ActivityKind.Server);

        activity?.SetTag(SpanAttributes.ServiceName, serviceName);
        activity?.SetTag(SpanAttributes.InstanceId, instanceId);
        activity?.SetTag(SpanAttributes.Operation, "Register");

        return activity;
    }

    /// <summary>
    /// 开始服务注销追踪
    /// </summary>
    public static Activity? StartDeregisterActivity(string instanceId, string? serviceName = null)
    {
        var activity = Instance.StartActivity($"{nameof(ServiceDiscovery)}.Deregister", ActivityKind.Server);

        activity?.SetTag(SpanAttributes.InstanceId, instanceId);
        activity?.SetTag(SpanAttributes.Operation, "Deregister");
        if (serviceName != null)
        {
            activity?.SetTag(SpanAttributes.ServiceName, serviceName);
        }

        return activity;
    }

    /// <summary>
    /// 开始心跳追踪
    /// </summary>
    public static Activity? StartHeartbeatActivity(string instanceId, string? serviceName = null)
    {
        var activity = Instance.StartActivity($"{nameof(ServiceDiscovery)}.Heartbeat", ActivityKind.Server);

        activity?.SetTag(SpanAttributes.InstanceId, instanceId);
        activity?.SetTag(SpanAttributes.Operation, "Heartbeat");
        if (serviceName != null)
        {
            activity?.SetTag(SpanAttributes.ServiceName, serviceName);
        }

        return activity;
    }

    /// <summary>
    /// 开始服务发现追踪
    /// </summary>
    public static Activity? StartDiscoverActivity(string serviceName)
    {
        var activity = Instance.StartActivity($"{nameof(ServiceDiscovery)}.Discover", ActivityKind.Client);

        activity?.SetTag(SpanAttributes.ServiceName, serviceName);
        activity?.SetTag(SpanAttributes.Operation, "Discover");

        return activity;
    }

    /// <summary>
    /// 开始健康检查追踪
    /// </summary>
    public static Activity? StartHealthCheckActivity(string serviceName, string instanceId, string? checkType = null)
    {
        var activity = Instance.StartActivity($"{nameof(ServiceDiscovery)}.HealthCheck");

        activity?.SetTag(SpanAttributes.ServiceName, serviceName);
        activity?.SetTag(SpanAttributes.InstanceId, instanceId);
        activity?.SetTag(SpanAttributes.Operation, "HealthCheck");
        if (checkType != null)
        {
            activity?.SetTag(SpanAttributes.HealthCheckType, checkType);
        }

        return activity;
    }

    /// <summary>
    /// 开始获取实例追踪
    /// </summary>
    public static Activity? StartGetInstancesActivity(string serviceName)
    {
        var activity = Instance.StartActivity($"{nameof(ServiceDiscovery)}.GetInstances", ActivityKind.Client);

        activity?.SetTag(SpanAttributes.ServiceName, serviceName);
        activity?.SetTag(SpanAttributes.Operation, "GetInstances");

        return activity;
    }

    /// <summary>
    /// 开始获取单个实例追踪 (负载均衡)
    /// </summary>
    public static Activity? StartGetInstanceActivity(string serviceName, string? loadBalancer = null)
    {
        var activity = Instance.StartActivity($"{nameof(ServiceDiscovery)}.GetInstance", ActivityKind.Client);

        activity?.SetTag(SpanAttributes.ServiceName, serviceName);
        activity?.SetTag(SpanAttributes.Operation, "GetInstance");
        if (loadBalancer != null)
        {
            activity?.SetTag(SpanAttributes.DiscoveryLoadBalancer, loadBalancer);
        }

        return activity;
    }

    /// <summary>
    /// 记录异常
    /// </summary>
    public static void RecordException(this Activity? activity, Exception exception)
    {
        if (activity == null)
            return;

        activity.SetTag(SpanAttributes.ErrorType, exception.GetType().Name);
        activity.SetTag(SpanAttributes.ErrorMessage, exception.Message);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);

        // 添加事件记录异常详情
        activity.AddEvent(
            new(
                "exception",
                tags: new()
                {
                    { "exception.type", exception.GetType().FullName },
                    { "exception.message", exception.Message },
                    { "exception.stacktrace", exception.StackTrace },
                }
            )
        );
    }

    /// <summary>
    /// 设置成功状态
    /// </summary>
    public static void SetSuccess(this Activity? activity, string? message = null) =>
        activity?.SetStatus(ActivityStatusCode.Ok, message);

    /// <summary>
    /// 记录缓存命中
    /// </summary>
    public static void RecordCacheHit(this Activity? activity, bool hit)
    {
        activity?.SetTag(SpanAttributes.CacheHit, hit);
        activity?.SetTag(SpanAttributes.DiscoveryCacheHit, hit);
    }

    /// <summary>
    /// 记录发现结果
    /// </summary>
    public static void RecordDiscoveryResult(this Activity? activity, int totalCount, int healthyCount)
    {
        activity?.SetTag(SpanAttributes.DiscoveryInstanceCount, totalCount);
        activity?.SetTag(SpanAttributes.DiscoveryHealthyCount, healthyCount);
    }

    /// <summary>
    /// 记录健康检查结果
    /// </summary>
    public static void RecordHealthCheckResult(this Activity? activity, string status, double durationMs)
    {
        activity?.SetTag(SpanAttributes.HealthStatus, status);
        activity?.SetTag(SpanAttributes.HealthCheckDuration, durationMs);
    }

    /// <summary>
    /// 记录注册信息
    /// </summary>
    public static void RecordRegistrationInfo(
        this Activity? activity,
        string host,
        int port,
        string scheme,
        IEnumerable<string>? tags = null
    )
    {
        activity?.SetTag(SpanAttributes.RegistrationHost, host);
        activity?.SetTag(SpanAttributes.RegistrationPort, port);
        activity?.SetTag(SpanAttributes.RegistrationScheme, scheme);
        if (tags != null)
        {
            activity?.SetTag(SpanAttributes.RegistrationTags, string.Join(",", tags));
        }
    }
}
