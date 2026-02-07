using System.Text;
using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Caching;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;
using JZVerse.MicroHuaxia.Gateway.Resilience.Fallback;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Resilience.Fallback;

[TestFixture]
public class StaticResponseFallbackHandlerTests
{
    private StaticResponseFallbackHandler _handler = null!;
    private Mock<ILogger<StaticResponseFallbackHandler>> _loggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<StaticResponseFallbackHandler>>();
        _handler = new StaticResponseFallbackHandler(_loggerMock.Object);
    }

    [Test]
    public void Name_ShouldBeStatic()
    {
        _handler.Name.Should().Be("static");
    }

    [Test]
    public void CanHandle_WithStaticFallbackEnabled_ShouldReturnTrue()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Static
        });

        // Act & Assert
        _handler.CanHandle(context, new Exception()).Should().BeTrue();
    }

    [Test]
    public void CanHandle_WithFallbackDisabled_ShouldReturnFalse()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = false,
            Type = FallbackType.Static
        });

        // Act & Assert
        _handler.CanHandle(context, new Exception()).Should().BeFalse();
    }

    [Test]
    public void CanHandle_WithCacheType_ShouldReturnFalse()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Cache
        });

        // Act & Assert
        _handler.CanHandle(context, new Exception()).Should().BeFalse();
    }

    [Test]
    public async Task HandleAsync_WithCustomStaticResponse_ShouldReturnConfiguredResponse()
    {
        // Arrange
        var staticResponse = new FallbackStaticResponse
        {
            StatusCode = 503,
            Body = "{\"error\":\"maintenance\"}",
            ContentType = "application/json",
            Headers = new Dictionary<string, string>
            {
                ["X-Custom-Header"] = "custom-value"
            }
        };
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Static,
            StaticResponse = staticResponse
        });

        // Act
        var result = await _handler.HandleAsync(context, new Exception());

        // Assert
        result.StatusCode.Should().Be(503);
        Encoding.UTF8.GetString(result.Body).Should().Be("{\"error\":\"maintenance\"}");
        result.ContentType.Should().Be("application/json");
        result.Headers.Should().ContainKey("X-Custom-Header");
        result.Source.Should().Be("static");
    }

    [Test]
    public async Task HandleAsync_BrokenCircuitException_ShouldReturn503()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Static
        });
        var exception = new BrokenCircuitException("Circuit is open");

        // Act
        var result = await _handler.HandleAsync(context, exception);

        // Assert
        result.StatusCode.Should().Be(503);
        Encoding.UTF8.GetString(result.Body).Should().Contain("CircuitBreakerOpen");
    }

    [Test]
    public async Task HandleAsync_TimeoutException_ShouldReturn504()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Static
        });
        var exception = new TimeoutException("Request timeout");

        // Act
        var result = await _handler.HandleAsync(context, exception);

        // Assert
        result.StatusCode.Should().Be(504);
        Encoding.UTF8.GetString(result.Body).Should().Contain("GatewayTimeout");
    }

    [Test]
    public async Task HandleAsync_HttpRequestException_ShouldReturn502()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Static
        });
        var exception = new HttpRequestException("Connection refused");

        // Act
        var result = await _handler.HandleAsync(context, exception);

        // Assert
        result.StatusCode.Should().Be(502);
        Encoding.UTF8.GetString(result.Body).Should().Contain("BadGateway");
    }

    [Test]
    public async Task HandleAsync_UnknownException_ShouldReturn503()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Static
        });
        var exception = new InvalidOperationException("Unknown error");

        // Act
        var result = await _handler.HandleAsync(context, exception);

        // Assert
        result.StatusCode.Should().Be(503);
        Encoding.UTF8.GetString(result.Body).Should().Contain("ServiceUnavailable");
    }

    private static IGatewayResilienceContext CreateContext(RouteFallback? fallback)
    {
        var route = new GatewayRoute
        {
            RouteId = "test-route",
            RouteName = "Test Route",
            Match = new RouteMatch { Path = "/api/{**remainder}" },
            Destination = new RouteDestination { ServiceName = "test-service" },
            Fallback = fallback
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/test";

        var matchResult = new RouteMatchResult
        {
            Route = route,
            MatchedPattern = "/api/{**remainder}",
            PathParameters = new Dictionary<string, string>(),
            TransformedPath = "/test"
        };

        return new GatewayResilienceContext
        {
            Route = route,
            HttpContext = httpContext,
            MatchResult = matchResult
        };
    }
}

[TestFixture]
public class CachedResponseFallbackHandlerTests
{
    private CachedResponseFallbackHandler _handler = null!;
    private Mock<IGatewayCache> _cacheMock = null!;
    private Mock<ILogger<CachedResponseFallbackHandler>> _loggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IGatewayCache>();
        _loggerMock = new Mock<ILogger<CachedResponseFallbackHandler>>();
        _handler = new CachedResponseFallbackHandler(_cacheMock.Object, _loggerMock.Object);
    }

    [Test]
    public void Name_ShouldBeCache()
    {
        _handler.Name.Should().Be("cache");
    }

    [Test]
    public void CanHandle_WithCacheFallbackEnabled_ShouldReturnTrue()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Cache
        });

        // Act & Assert
        _handler.CanHandle(context, new Exception()).Should().BeTrue();
    }

    [Test]
    public void CanHandle_WithStaticType_ShouldReturnFalse()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Static
        });

        // Act & Assert
        _handler.CanHandle(context, new Exception()).Should().BeFalse();
    }

    [Test]
    public async Task HandleAsync_WithCachedResponse_ShouldReturnCachedData()
    {
        // Arrange
        var cachedResponse = new CachedResponse
        {
            StatusCode = 200,
            Body = Encoding.UTF8.GetBytes("{\"data\":\"cached\"}"),
            ContentType = "application/json",
            Headers = new Dictionary<string, string[]>
            {
                ["X-Cached"] = ["true"]
            }
        };
        _cacheMock
            .Setup(c => c.TryGetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Cache
        });

        // Act
        var result = await _handler.HandleAsync(context, new Exception());

        // Assert
        result.StatusCode.Should().Be(200);
        Encoding.UTF8.GetString(result.Body).Should().Be("{\"data\":\"cached\"}");
        result.ContentType.Should().Be("application/json");
        result.Source.Should().Be("cache");
    }

    [Test]
    public async Task HandleAsync_CacheMiss_WithDefaultResponse_ShouldReturnDefault()
    {
        // Arrange
        _cacheMock
            .Setup(c => c.TryGetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedResponse?)null);

        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Cache,
            CacheOptions = new FallbackCacheOptions
            {
                MaxAge = TimeSpan.FromMinutes(5),
                UseDefaultIfNotCached = true,
                DefaultResponse = new FallbackStaticResponse
                {
                    StatusCode = 200,
                    Body = "{\"default\":true}",
                    ContentType = "application/json"
                }
            }
        });

        // Act
        var result = await _handler.HandleAsync(context, new Exception());

        // Assert
        result.StatusCode.Should().Be(200);
        Encoding.UTF8.GetString(result.Body).Should().Be("{\"default\":true}");
        result.Source.Should().Be("cache-default");
    }

    [Test]
    public async Task HandleAsync_CacheMiss_NoDefaultResponse_ShouldReturn503()
    {
        // Arrange
        _cacheMock
            .Setup(c => c.TryGetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedResponse?)null);

        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Cache,
            CacheOptions = new FallbackCacheOptions
            {
                MaxAge = TimeSpan.FromMinutes(5),
                UseDefaultIfNotCached = false
            }
        });

        // Act
        var result = await _handler.HandleAsync(context, new Exception());

        // Assert
        result.StatusCode.Should().Be(503);
        Encoding.UTF8.GetString(result.Body).Should().Contain("ServiceUnavailable");
    }

    [Test]
    public async Task SaveFallbackCacheAsync_ShouldSaveToCache()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Cache,
            CacheOptions = new FallbackCacheOptions
            {
                MaxAge = TimeSpan.FromMinutes(10)
            }
        });
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = Encoding.UTF8.GetBytes("{\"data\":\"value\"}"),
            ContentType = "application/json",
            Headers = new Dictionary<string, string[]>()
        };

        // Act
        await _handler.SaveFallbackCacheAsync(context, response);

        // Assert
        _cacheMock.Verify(c => c.SetAsync(
            It.Is<string>(k => k.Contains("fallback:")),
            response,
            TimeSpan.FromMinutes(10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SaveFallbackCacheAsync_WithNoCacheOptions_ShouldNotSave()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Cache,
            CacheOptions = null
        });
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = Encoding.UTF8.GetBytes("{\"data\":\"value\"}"),
            ContentType = "application/json",
            Headers = new Dictionary<string, string[]>()
        };

        // Act
        await _handler.SaveFallbackCacheAsync(context, response);

        // Assert
        _cacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<CachedResponse>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IGatewayResilienceContext CreateContext(RouteFallback? fallback)
    {
        var route = new GatewayRoute
        {
            RouteId = "test-route",
            RouteName = "Test Route",
            Match = new RouteMatch { Path = "/api/{**remainder}" },
            Destination = new RouteDestination { ServiceName = "test-service" },
            Fallback = fallback
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/test";
        httpContext.Request.Method = "GET";

        var matchResult = new RouteMatchResult
        {
            Route = route,
            MatchedPattern = "/api/{**remainder}",
            PathParameters = new Dictionary<string, string>(),
            TransformedPath = "/test"
        };

        return new GatewayResilienceContext
        {
            Route = route,
            HttpContext = httpContext,
            MatchResult = matchResult
        };
    }
}

[TestFixture]
public class FallbackResultTests
{
    [Test]
    public void FromStatic_ShouldCreateCorrectResult()
    {
        // Act
        var result = FallbackResult.FromStatic(503, "{\"error\":\"test\"}", "application/json");

        // Assert
        result.StatusCode.Should().Be(503);
        Encoding.UTF8.GetString(result.Body).Should().Be("{\"error\":\"test\"}");
        result.ContentType.Should().Be("application/json");
        result.Source.Should().Be("static");
    }

    [Test]
    public void FromCache_ShouldCreateCorrectResult()
    {
        // Arrange
        var body = Encoding.UTF8.GetBytes("{\"cached\":true}");
        var headers = new Dictionary<string, string[]>
        {
            ["X-Cache"] = ["HIT"]
        };

        // Act
        var result = FallbackResult.FromCache(200, body, "application/json", headers);

        // Assert
        result.StatusCode.Should().Be(200);
        result.Body.Should().BeEquivalentTo(body);
        result.ContentType.Should().Be("application/json");
        result.Headers.Should().ContainKey("X-Cache");
        result.Source.Should().Be("cache");
    }
}
