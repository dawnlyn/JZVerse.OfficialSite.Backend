using JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.DataAccess.Caching.AspNetCore;

/// <summary>
/// 缓存健康检查
/// </summary>
public sealed class CachingHealthCheck : IHealthCheck
{
    private readonly IEnumerable<ICacheProvider> _providers;
    private readonly CacheOptions _options;
    private readonly ILogger<CachingHealthCheck> _logger;

    public CachingHealthCheck(
        IEnumerable<ICacheProvider> providers,
        IOptions<CacheOptions> options,
        ILogger<CachingHealthCheck> logger)
    {
        _providers = providers;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy("Caching is disabled");
        }

        var data = new Dictionary<string, object>();
        var unhealthyProviders = new List<string>();

        foreach (var provider in _providers)
        {
            var levelName = provider.Level.ToString();

            try
            {
                var isAvailable = provider.IsAvailable;
                data[$"{levelName}.Available"] = isAvailable;

                if (!isAvailable)
                {
                    // 检查是否是必需的缓存级别
                    var isRequired = provider.Level switch
                    {
                        CacheLevel.Memory => _options.Memory.Enabled,
                        CacheLevel.Redis => _options.Redis.Enabled,
                        _ => false
                    };

                    if (isRequired)
                    {
                        unhealthyProviders.Add(levelName);
                    }
                }
                else
                {
                    // 尝试进行简单的读写测试
                    var testKey = $"health_check_{Guid.NewGuid():N}";
                    var testValue = DateTime.UtcNow.Ticks;

                    await provider.SetAsync(testKey, testValue, TimeSpan.FromSeconds(10), cancellationToken);
                    var result = await provider.GetAsync<long>(testKey, cancellationToken);
                    await provider.RemoveAsync(testKey, cancellationToken);

                    data[$"{levelName}.ReadWriteTest"] = result == testValue ? "Passed" : "Failed";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check failed for cache provider {Level}", levelName);
                data[$"{levelName}.Error"] = ex.Message;
                unhealthyProviders.Add(levelName);
            }
        }

        if (unhealthyProviders.Count > 0)
        {
            return HealthCheckResult.Unhealthy(
                $"Cache providers unhealthy: {string.Join(", ", unhealthyProviders)}",
                data: data);
        }

        return HealthCheckResult.Healthy("All cache providers are healthy", data);
    }
}
