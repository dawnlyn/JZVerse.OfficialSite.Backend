using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;
using JZVerse.MicroHuaxia.Gateway.Resilience.Fallback;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Resilience.Fallback;

[TestFixture]
public class FallbackHandlerRegistryTests
{
    private Mock<ILogger<FallbackHandlerRegistry>> _loggerMock = null!;
    private Mock<IFallbackHandler> _staticHandlerMock = null!;
    private Mock<IFallbackHandler> _cacheHandlerMock = null!;
    private Mock<IFallbackHandler> _customHandlerMock = null!;
    private FallbackHandlerRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<FallbackHandlerRegistry>>();

        _staticHandlerMock = new Mock<IFallbackHandler>();
        _staticHandlerMock.Setup(h => h.Name).Returns("static");

        _cacheHandlerMock = new Mock<IFallbackHandler>();
        _cacheHandlerMock.Setup(h => h.Name).Returns("cache");

        _customHandlerMock = new Mock<IFallbackHandler>();
        _customHandlerMock.Setup(h => h.Name).Returns("my-custom-handler");

        var handlers = new[] { _staticHandlerMock.Object, _cacheHandlerMock.Object, _customHandlerMock.Object };
        _registry = new FallbackHandlerRegistry(handlers, _loggerMock.Object);
    }

    [Test]
    public void GetHandler_ExistingHandler_ShouldReturn()
    {
        // Act
        var handler = _registry.GetHandler("static");

        // Assert
        handler.Should().NotBeNull();
        handler.Should().BeSameAs(_staticHandlerMock.Object);
    }

    [Test]
    public void GetHandler_CaseInsensitive_ShouldReturn()
    {
        // Act
        var handler = _registry.GetHandler("STATIC");

        // Assert
        handler.Should().NotBeNull();
        handler.Should().BeSameAs(_staticHandlerMock.Object);
    }

    [Test]
    public void GetHandler_NonExistingHandler_ShouldReturnNull()
    {
        // Act
        var handler = _registry.GetHandler("non-existing");

        // Assert
        handler.Should().BeNull();
    }

    [Test]
    public void GetAllHandlers_ShouldReturnAll()
    {
        // Act
        var handlers = _registry.GetAllHandlers().ToList();

        // Assert
        handlers.Should().HaveCount(3);
        handlers.Should().Contain(_staticHandlerMock.Object);
        handlers.Should().Contain(_cacheHandlerMock.Object);
        handlers.Should().Contain(_customHandlerMock.Object);
    }

    [Test]
    public void SelectHandler_FallbackDisabled_ShouldReturnNull()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = false,
            Type = FallbackType.Static
        });

        // Act
        var handler = _registry.SelectHandler(context, new Exception());

        // Assert
        handler.Should().BeNull();
    }

    [Test]
    public void SelectHandler_NullFallback_ShouldReturnNull()
    {
        // Arrange
        var context = CreateContext(null);

        // Act
        var handler = _registry.SelectHandler(context, new Exception());

        // Assert
        handler.Should().BeNull();
    }

    [Test]
    public void SelectHandler_StaticType_ShouldReturnStaticHandler()
    {
        // Arrange
        _staticHandlerMock.Setup(h => h.CanHandle(It.IsAny<IGatewayResilienceContext>(), It.IsAny<Exception>()))
            .Returns(true);

        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Static
        });

        // Act
        var handler = _registry.SelectHandler(context, new Exception());

        // Assert
        handler.Should().BeSameAs(_staticHandlerMock.Object);
    }

    [Test]
    public void SelectHandler_CacheType_ShouldReturnCacheHandler()
    {
        // Arrange
        _cacheHandlerMock.Setup(h => h.CanHandle(It.IsAny<IGatewayResilienceContext>(), It.IsAny<Exception>()))
            .Returns(true);

        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Cache
        });

        // Act
        var handler = _registry.SelectHandler(context, new Exception());

        // Assert
        handler.Should().BeSameAs(_cacheHandlerMock.Object);
    }

    [Test]
    public void SelectHandler_CustomType_ShouldReturnCustomHandler()
    {
        // Arrange
        _customHandlerMock.Setup(h => h.CanHandle(It.IsAny<IGatewayResilienceContext>(), It.IsAny<Exception>()))
            .Returns(true);

        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Custom,
            CustomHandlerName = "my-custom-handler"
        });

        // Act
        var handler = _registry.SelectHandler(context, new Exception());

        // Assert
        handler.Should().BeSameAs(_customHandlerMock.Object);
    }

    [Test]
    public void SelectHandler_CustomType_NotFound_ShouldReturnNull()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Custom,
            CustomHandlerName = "unknown-handler"
        });

        // Act
        var handler = _registry.SelectHandler(context, new Exception());

        // Assert
        handler.Should().BeNull();
    }

    [Test]
    public void SelectHandler_HandlerCannotHandle_ShouldReturnNull()
    {
        // Arrange
        _staticHandlerMock.Setup(h => h.CanHandle(It.IsAny<IGatewayResilienceContext>(), It.IsAny<Exception>()))
            .Returns(false); // Handler reports it cannot handle

        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Static
        });

        // Act
        var handler = _registry.SelectHandler(context, new Exception());

        // Assert
        handler.Should().BeNull();
    }

    [Test]
    public void SelectHandler_CustomType_NoName_ShouldReturnNull()
    {
        // Arrange
        var context = CreateContext(new RouteFallback
        {
            Enabled = true,
            Type = FallbackType.Custom,
            CustomHandlerName = null // No custom handler name
        });

        // Act
        var handler = _registry.SelectHandler(context, new Exception());

        // Assert
        handler.Should().BeNull();
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
