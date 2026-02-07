using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Gateway.Abstractions.RateLimiting;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Core.RateLimiting;

/// <summary>
/// 限流器工厂实现
/// 根据算法类型创建对应的限流器实例
/// </summary>
public sealed class RateLimiterFactory : IRateLimiterFactory
{
    private readonly ConcurrentDictionary<RateLimitAlgorithm, IRateLimiter> _limiters = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly IRateLimiterStore _store;
    private readonly ILoggerFactory _loggerFactory;

    public RateLimiterFactory(
        IServiceProvider serviceProvider,
        IRateLimiterStore store,
        ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _store = store;
        _loggerFactory = loggerFactory;
    }

    public IRateLimiter GetOrCreate(RateLimitAlgorithm algorithm)
    {
        return _limiters.GetOrAdd(algorithm, CreateLimiter);
    }

    private IRateLimiter CreateLimiter(RateLimitAlgorithm algorithm)
    {
        return algorithm switch
        {
            RateLimitAlgorithm.SlidingWindow => new SlidingWindowRateLimiter(
                _loggerFactory.CreateLogger<SlidingWindowRateLimiter>(),
                _store),

            RateLimitAlgorithm.TokenBucket => new TokenBucketRateLimiter(
                _loggerFactory.CreateLogger<TokenBucketRateLimiter>(),
                _store),

            RateLimitAlgorithm.LeakyBucket => new LeakyBucketRateLimiter(
                _loggerFactory.CreateLogger<LeakyBucketRateLimiter>(),
                _store),

            _ => new SlidingWindowRateLimiter(
                _loggerFactory.CreateLogger<SlidingWindowRateLimiter>(),
                _store)
        };
    }
}
