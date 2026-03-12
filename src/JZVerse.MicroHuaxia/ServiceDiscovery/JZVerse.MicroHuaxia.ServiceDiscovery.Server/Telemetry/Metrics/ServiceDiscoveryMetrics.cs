using System.Diagnostics.Metrics;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Metrics;

/// <summary>
/// 服务发现指标收集器
/// </summary>
public sealed class ServiceDiscoveryMetrics
{
    private readonly Meter _meter;

    // 计数器
    private readonly Counter<long> _registrationsCounter;
    private readonly Counter<long> _deregistrationsCounter;
    private readonly Counter<long> _heartbeatsCounter;
    private readonly Counter<long> _discoveryRequestsCounter;
    private readonly Counter<long> _healthChecksCounter;
    private readonly Counter<long> _healthCheckFailuresCounter;

    // 直方图
    private readonly Histogram<double> _discoveryDuration;
    private readonly Histogram<double> _healthCheckDuration;
    private readonly Histogram<double> _registrationDuration;

    // 可观察计数器的数据源
    private Func<int> _getTotalServices = () => 0;
    private Func<int> _getTotalInstances = () => 0;
    private Func<int> _getHealthyInstances = () => 0;
    private Func<int> _getUnhealthyInstances = () => 0;

    public ServiceDiscoveryMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MetricNames.MeterName, MetricNames.Version);

        // 初始化计数器
        _registrationsCounter = _meter.CreateCounter<long>(MetricNames.Registrations, "{registration}", "服务注册总数");

        _deregistrationsCounter = _meter.CreateCounter<long>(
            MetricNames.Deregistrations,
            "{deregistration}",
            "服务注销总数"
        );

        _heartbeatsCounter = _meter.CreateCounter<long>(MetricNames.Heartbeats, "{heartbeat}", "心跳总数");

        _discoveryRequestsCounter = _meter.CreateCounter<long>(
            MetricNames.DiscoveryRequests,
            "{request}",
            "服务发现请求总数"
        );

        _healthChecksCounter = _meter.CreateCounter<long>(MetricNames.HealthChecks, "{check}", "健康检查总数");

        _healthCheckFailuresCounter = _meter.CreateCounter<long>(
            MetricNames.HealthCheckFailures,
            "{failure}",
            "健康检查失败总数"
        );

        // 初始化直方图
        _discoveryDuration = _meter.CreateHistogram<double>(MetricNames.DiscoveryDuration, "ms", "服务发现请求耗时");

        _healthCheckDuration = _meter.CreateHistogram<double>(MetricNames.HealthCheckDuration, "ms", "健康检查耗时");

        _registrationDuration = _meter.CreateHistogram<double>(MetricNames.RegistrationDuration, "ms", "服务注册耗时");

        // 初始化可观察计数器
        _meter.CreateObservableGauge(MetricNames.TotalServices, () => _getTotalServices(), "{service}", "服务总数");

        _meter.CreateObservableGauge(MetricNames.TotalInstances, () => _getTotalInstances(), "{instance}", "实例总数");

        _meter.CreateObservableGauge(
            MetricNames.HealthyInstances,
            () => _getHealthyInstances(),
            "{instance}",
            "健康实例数"
        );

        _meter.CreateObservableGauge(
            MetricNames.UnhealthyInstances,
            () => _getUnhealthyInstances(),
            "{instance}",
            "不健康实例数"
        );
    }

    /// <summary>
    /// 设置实例统计数据提供者
    /// </summary>
    public void SetInstanceStatsProvider(
        Func<int> getTotalServices,
        Func<int> getTotalInstances,
        Func<int> getHealthyInstances,
        Func<int> getUnhealthyInstances
    )
    {
        _getTotalServices = getTotalServices;
        _getTotalInstances = getTotalInstances;
        _getHealthyInstances = getHealthyInstances;
        _getUnhealthyInstances = getUnhealthyInstances;
    }

    /// <summary>
    /// 记录服务注册
    /// </summary>
    public void RecordRegistration(string serviceName, string instanceId) =>
        _registrationsCounter.Add(1, new("service.name", serviceName), new("instance.id", instanceId));

    /// <summary>
    /// 记录服务注销
    /// </summary>
    public void RecordDeregistration(string serviceName, string instanceId) =>
        _deregistrationsCounter.Add(1, new("service.name", serviceName), new("instance.id", instanceId));

    /// <summary>
    /// 记录心跳
    /// </summary>
    public void RecordHeartbeat(string serviceName, string instanceId) =>
        _heartbeatsCounter.Add(1, new("service.name", serviceName), new("instance.id", instanceId));

    /// <summary>
    /// 记录服务发现请求
    /// </summary>
    public void RecordDiscoveryRequest(string serviceName, int instanceCount, double durationMs)
    {
        _discoveryRequestsCounter.Add(1, new KeyValuePair<string, object?>("service.name", serviceName));

        _discoveryDuration.Record(durationMs, new KeyValuePair<string, object?>("service.name", serviceName));
    }

    /// <summary>
    /// 记录健康检查
    /// </summary>
    public void RecordHealthCheck(string serviceName, string instanceId, bool success, double durationMs)
    {
        _healthChecksCounter.Add(
            1,
            new("service.name", serviceName),
            new("instance.id", instanceId),
            new("success", success)
        );

        if (!success)
        {
            _healthCheckFailuresCounter.Add(1, new("service.name", serviceName), new("instance.id", instanceId));
        }

        _healthCheckDuration.Record(durationMs, new("service.name", serviceName), new("success", success));
    }

    /// <summary>
    /// 记录服务注册耗时
    /// </summary>
    public void RecordRegistrationDuration(string serviceName, double durationMs) =>
        _registrationDuration.Record(durationMs, new KeyValuePair<string, object?>("service.name", serviceName));
}
