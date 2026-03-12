using JZVerse.DataAccess.Abstractions.Caching;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.DataAccess.Caching.AspNetCore;

/// <summary>
/// 缓存预热后台服务
/// </summary>
/// <remarks>
/// 在应用启动时自动执行缓存预热
/// </remarks>
public sealed class CacheWarmupBackgroundService : BackgroundService
{
    private readonly ICacheWarmer _cacheWarmer;
    private readonly CacheOptions _options;
    private readonly ILogger<CacheWarmupBackgroundService> _logger;

    public CacheWarmupBackgroundService(
        ICacheWarmer cacheWarmer,
        IOptions<CacheOptions> options,
        ILogger<CacheWarmupBackgroundService> logger)
    {
        _cacheWarmer = cacheWarmer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.Warmup.EnableOnStartup)
        {
            _logger.LogInformation("Cache warmup is disabled");
            return;
        }

        // 延迟启动，等待应用完全启动
        var delaySeconds = _options.Warmup.DelayStartSeconds;
        if (delaySeconds > 0)
        {
            _logger.LogInformation("Cache warmup will start in {Delay} seconds", delaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }

        try
        {
            _logger.LogInformation("Starting cache warmup...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            await _cacheWarmer.WarmupAsync(stoppingToken);

            stopwatch.Stop();
            _logger.LogInformation("Cache warmup completed in {Elapsed}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Cache warmup was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache warmup failed");

            if (_options.Warmup.FailOnError)
            {
                throw;
            }
        }
    }
}
