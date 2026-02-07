using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.RateLimiting;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Core.RateLimiting;
using JZVerse.MicroHuaxia.Gateway.Core.RateLimiting.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.RateLimiting;

[TestFixture]
public class TokenBucketRateLimiterTests
{
    private MemoryRateLimiterStore _store = null!;
    private TokenBucketRateLimiter _limiter = null!;

    [SetUp]
    public void Setup()
    {
        _store = new MemoryRateLimiterStore();
        _limiter = new TokenBucketRateLimiter(
            NullLogger<TokenBucketRateLimiter>.Instance,
            _store);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public async Task TryAcquireAsync_FirstRequest_ShouldAllow()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokensPerSecond = 10,
            BucketCapacity = 100
        };

        // Act
        var result = await _limiter.TryAcquireAsync("test-key", policy);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Test]
    public async Task TryAcquireAsync_MultipleRequests_ShouldAllowUpToCapacity()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokensPerSecond = 10,
            BucketCapacity = 5
        };

        // Act - 发送 5 个请求（桶容量）
        var results = new List<RateLimitResult>();
        for (var i = 0; i < 5; i++)
        {
            results.Add(await _limiter.TryAcquireAsync("burst-key", policy));
        }

        // Assert - 前 5 个应该都允许
        results.Should().AllSatisfy(r => r.IsAllowed.Should().BeTrue());
    }

    [Test]
    public async Task TryAcquireAsync_ExceedCapacity_ShouldReject()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokensPerSecond = 1,
            BucketCapacity = 2
        };

        // Act - 发送超过桶容量的请求
        await _limiter.TryAcquireAsync("exceed-key", policy);
        await _limiter.TryAcquireAsync("exceed-key", policy);
        var thirdResult = await _limiter.TryAcquireAsync("exceed-key", policy);

        // Assert - 第三个请求应该被拒绝
        thirdResult.IsAllowed.Should().BeFalse();
        thirdResult.RetryAfter.Should().NotBeNull();
    }

    [Test]
    public async Task TryAcquireAsync_DifferentKeys_ShouldBeIndependent()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokensPerSecond = 1,
            BucketCapacity = 1
        };

        // Act
        var result1 = await _limiter.TryAcquireAsync("key-1", policy);
        var result2 = await _limiter.TryAcquireAsync("key-2", policy);

        // Assert - 不同的键应该独立管理
        result1.IsAllowed.Should().BeTrue();
        result2.IsAllowed.Should().BeTrue();
    }
}

[TestFixture]
public class LeakyBucketRateLimiterTests
{
    private MemoryRateLimiterStore _store = null!;
    private LeakyBucketRateLimiter _limiter = null!;

    [SetUp]
    public void Setup()
    {
        _store = new MemoryRateLimiterStore();
        _limiter = new LeakyBucketRateLimiter(
            NullLogger<LeakyBucketRateLimiter>.Instance,
            _store);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public async Task TryAcquireAsync_FirstRequest_ShouldAllow()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            Algorithm = RateLimitAlgorithm.LeakyBucket,
            TokensPerSecond = 10,
            BucketCapacity = 100,
            Limit = 100
        };

        // Act
        var result = await _limiter.TryAcquireAsync("leaky-test", policy);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Test]
    public async Task TryAcquireAsync_MultipleRequests_ShouldQueueUpToCapacity()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            Algorithm = RateLimitAlgorithm.LeakyBucket,
            TokensPerSecond = 10,
            BucketCapacity = 5,
            Limit = 5
        };

        // Act
        var results = new List<RateLimitResult>();
        for (var i = 0; i < 5; i++)
        {
            results.Add(await _limiter.TryAcquireAsync("leaky-queue", policy));
        }

        // Assert - 前 5 个应该都允许入队
        results.Should().AllSatisfy(r => r.IsAllowed.Should().BeTrue());
    }

    [Test]
    public async Task TryAcquireAsync_DifferentKeys_ShouldBeIndependent()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            Algorithm = RateLimitAlgorithm.LeakyBucket,
            TokensPerSecond = 1,
            BucketCapacity = 1,
            Limit = 1
        };

        // Act
        var result1 = await _limiter.TryAcquireAsync("leaky-key-1", policy);
        var result2 = await _limiter.TryAcquireAsync("leaky-key-2", policy);

        // Assert
        result1.IsAllowed.Should().BeTrue();
        result2.IsAllowed.Should().BeTrue();
    }
}

[TestFixture]
public class RateLimiterFactoryTests
{
    private MemoryRateLimiterStore _store = null!;

    [SetUp]
    public void Setup()
    {
        _store = new MemoryRateLimiterStore();
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public void GetOrCreate_SlidingWindow_ShouldReturnSlidingWindowLimiter()
    {
        // Arrange
        var factory = new RateLimiterFactory(
            null!,
            _store,
            NullLoggerFactory.Instance);

        // Act
        var limiter = factory.GetOrCreate(RateLimitAlgorithm.SlidingWindow);

        // Assert
        limiter.Should().BeOfType<SlidingWindowRateLimiter>();
    }

    [Test]
    public void GetOrCreate_TokenBucket_ShouldReturnTokenBucketLimiter()
    {
        // Arrange
        var factory = new RateLimiterFactory(
            null!,
            _store,
            NullLoggerFactory.Instance);

        // Act
        var limiter = factory.GetOrCreate(RateLimitAlgorithm.TokenBucket);

        // Assert
        limiter.Should().BeOfType<TokenBucketRateLimiter>();
    }

    [Test]
    public void GetOrCreate_LeakyBucket_ShouldReturnLeakyBucketLimiter()
    {
        // Arrange
        var factory = new RateLimiterFactory(
            null!,
            _store,
            NullLoggerFactory.Instance);

        // Act
        var limiter = factory.GetOrCreate(RateLimitAlgorithm.LeakyBucket);

        // Assert
        limiter.Should().BeOfType<LeakyBucketRateLimiter>();
    }

    [Test]
    public void GetOrCreate_SameAlgorithm_ShouldReturnCachedInstance()
    {
        // Arrange
        var factory = new RateLimiterFactory(
            null!,
            _store,
            NullLoggerFactory.Instance);

        // Act
        var limiter1 = factory.GetOrCreate(RateLimitAlgorithm.TokenBucket);
        var limiter2 = factory.GetOrCreate(RateLimitAlgorithm.TokenBucket);

        // Assert
        limiter1.Should().BeSameAs(limiter2);
    }
}
