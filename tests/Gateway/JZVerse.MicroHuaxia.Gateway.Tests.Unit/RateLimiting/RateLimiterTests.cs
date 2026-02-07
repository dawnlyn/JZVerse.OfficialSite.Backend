using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.RateLimiting;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Core.RateLimiting;
using JZVerse.MicroHuaxia.Gateway.Core.RateLimiting.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.RateLimiting;

[TestFixture]
public class SlidingWindowRateLimiterTests
{
    private SlidingWindowRateLimiter _rateLimiter = null!;
    private Mock<ILogger<SlidingWindowRateLimiter>> _loggerMock = null!;
    private MemoryRateLimiterStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<SlidingWindowRateLimiter>>();
        _store = new MemoryRateLimiterStore();
        _rateLimiter = new SlidingWindowRateLimiter(_loggerMock.Object, _store);
    }

    [Test]
    public async Task TryAcquireAsync_FirstRequest_ShouldBeAllowed()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            Limit = 10,
            Window = TimeSpan.FromMinutes(1)
        };

        // Act
        var result = await _rateLimiter.TryAcquireAsync("test-key", policy);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.CurrentCount.Should().Be(1);
        result.Limit.Should().Be(10);
        result.Remaining.Should().Be(9);
    }

    [Test]
    public async Task TryAcquireAsync_UnderLimit_ShouldBeAllowed()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            Limit = 10,
            Window = TimeSpan.FromMinutes(1)
        };

        // Act
        for (var i = 0; i < 5; i++)
        {
            await _rateLimiter.TryAcquireAsync("test-key", policy);
        }

        var result = await _rateLimiter.TryAcquireAsync("test-key", policy);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.CurrentCount.Should().Be(6);
        result.Remaining.Should().Be(4);
    }

    [Test]
    public async Task TryAcquireAsync_OverLimit_ShouldBeDenied()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            Limit = 3,
            Window = TimeSpan.FromMinutes(1)
        };

        // Act
        for (var i = 0; i < 3; i++)
        {
            await _rateLimiter.TryAcquireAsync("test-key", policy);
        }

        var result = await _rateLimiter.TryAcquireAsync("test-key", policy);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.CurrentCount.Should().Be(4);
        result.Remaining.Should().Be(0);
        result.RetryAfter.Should().NotBeNull();
    }

    [Test]
    public async Task TryAcquireAsync_DifferentKeys_ShouldBeIndependent()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            Limit = 2,
            Window = TimeSpan.FromMinutes(1)
        };

        // Act
        await _rateLimiter.TryAcquireAsync("key-1", policy);
        await _rateLimiter.TryAcquireAsync("key-1", policy);
        var result1 = await _rateLimiter.TryAcquireAsync("key-1", policy);
        var result2 = await _rateLimiter.TryAcquireAsync("key-2", policy);

        // Assert
        result1.IsAllowed.Should().BeFalse();
        result2.IsAllowed.Should().BeTrue();
        result2.CurrentCount.Should().Be(1);
    }
}

[TestFixture]
public class MemoryRateLimiterStoreTests
{
    private MemoryRateLimiterStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new MemoryRateLimiterStore();
    }

    [Test]
    public async Task IncrementAsync_NewKey_ShouldReturnOne()
    {
        // Act
        var count = await _store.IncrementAsync("test-key", TimeSpan.FromMinutes(1));

        // Assert
        count.Should().Be(1);
    }

    [Test]
    public async Task IncrementAsync_ExistingKey_ShouldIncrement()
    {
        // Arrange
        await _store.IncrementAsync("test-key", TimeSpan.FromMinutes(1));

        // Act
        var count = await _store.IncrementAsync("test-key", TimeSpan.FromMinutes(1));

        // Assert
        count.Should().Be(2);
    }

    [Test]
    public async Task GetCountAsync_ExistingKey_ShouldReturnCount()
    {
        // Arrange
        await _store.IncrementAsync("test-key", TimeSpan.FromMinutes(1));
        await _store.IncrementAsync("test-key", TimeSpan.FromMinutes(1));

        // Act
        var count = await _store.GetCountAsync("test-key");

        // Assert
        count.Should().Be(2);
    }

    [Test]
    public async Task GetCountAsync_NonExistingKey_ShouldReturnZero()
    {
        // Act
        var count = await _store.GetCountAsync("non-existing");

        // Assert
        count.Should().Be(0);
    }

    [Test]
    public async Task ResetAsync_ExistingKey_ShouldClearCount()
    {
        // Arrange
        await _store.IncrementAsync("test-key", TimeSpan.FromMinutes(1));

        // Act
        await _store.ResetAsync("test-key");
        var count = await _store.GetCountAsync("test-key");

        // Assert
        count.Should().Be(0);
    }
}

[TestFixture]
public class RateLimitKeyGeneratorTests
{
    private RateLimitKeyGenerator _keyGenerator = null!;

    [SetUp]
    public void SetUp()
    {
        _keyGenerator = new RateLimitKeyGenerator();
    }

    [Test]
    public void GenerateKey_ClientIpStrategy_ShouldUseIp()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        // Act
        var key = _keyGenerator.GenerateKey(context, "route-1", RateLimitKeyStrategy.ClientIp);

        // Assert
        key.Should().Contain("ClientIp");
        key.Should().Contain("192.168.1.100");
    }

    [Test]
    public void GenerateKey_RouteStrategy_ShouldUseRouteId()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var key = _keyGenerator.GenerateKey(context, "route-1", RateLimitKeyStrategy.Route);

        // Assert
        key.Should().Contain("Route");
        key.Should().Contain("route-1");
    }

    [Test]
    public void GenerateKey_GlobalStrategy_ShouldUseGlobal()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var key = _keyGenerator.GenerateKey(context, "route-1", RateLimitKeyStrategy.Global);

        // Assert
        key.Should().Contain("Global");
        key.Should().Contain("global");
    }

    [Test]
    public void GenerateKey_CustomHeaderStrategy_ShouldUseHeaderValue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Custom-Key"] = "custom-value";

        // Act
        var key = _keyGenerator.GenerateKey(context, "route-1", RateLimitKeyStrategy.CustomHeader, "X-Custom-Key");

        // Assert
        key.Should().Contain("CustomHeader");
        key.Should().Contain("custom-value");
    }
}
