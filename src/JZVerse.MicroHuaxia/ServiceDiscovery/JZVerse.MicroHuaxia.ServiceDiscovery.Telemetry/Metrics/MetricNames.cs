namespace JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Metrics;

/// <summary>
/// 指标名称常量
/// </summary>
public static class MetricNames
{
    public const string MeterName = "JZVerse.MicroHuaxia.ServiceDiscovery";
    public const string Version = "1.0.0";

    // 计数器指标
    public const string Registrations = "sd.registrations";
    public const string Deregistrations = "sd.deregistrations";
    public const string Heartbeats = "sd.heartbeats";
    public const string DiscoveryRequests = "sd.discovery.requests";
    public const string HealthChecks = "sd.health_checks";
    public const string HealthCheckFailures = "sd.health_check_failures";

    // 直方图指标
    public const string DiscoveryDuration = "sd.discovery.duration";
    public const string HealthCheckDuration = "sd.health_check.duration";
    public const string RegistrationDuration = "sd.registration.duration";

    // 计量指标
    public const string TotalServices = "sd.services.total";
    public const string TotalInstances = "sd.instances.total";
    public const string HealthyInstances = "sd.instances.healthy";
    public const string UnhealthyInstances = "sd.instances.unhealthy";
}
