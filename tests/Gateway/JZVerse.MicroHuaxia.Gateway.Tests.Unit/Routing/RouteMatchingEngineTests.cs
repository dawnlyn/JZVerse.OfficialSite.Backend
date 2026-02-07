using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Core.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Routing;

[TestFixture]
public class RouteMatchingEngineTests
{
    private RouteMatchingEngine _engine = null!;
    private Mock<ILogger<RouteMatchingEngine>> _loggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<RouteMatchingEngine>>();
        _engine = new RouteMatchingEngine(_loggerMock.Object);
    }

    [Test]
    public void AddRoute_ValidRoute_ShouldAddSuccessfully()
    {
        // Arrange
        var route = CreateTestRoute("route-1", "/api/users");

        // Act
        _engine.AddRoute(route);

        // Assert
        _engine.GetAllRoutes().Should().HaveCount(1);
        _engine.GetRoute("route-1").Should().NotBeNull();
    }

    [Test]
    public void RemoveRoute_ExistingRoute_ShouldRemoveSuccessfully()
    {
        // Arrange
        var route = CreateTestRoute("route-1", "/api/users");
        _engine.AddRoute(route);

        // Act
        var result = _engine.RemoveRoute("route-1");

        // Assert
        result.Should().BeTrue();
        _engine.GetAllRoutes().Should().BeEmpty();
    }

    [Test]
    public void RemoveRoute_NonExistingRoute_ShouldReturnFalse()
    {
        // Act
        var result = _engine.RemoveRoute("non-existing");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void UpdateRoutes_MultipleRoutes_ShouldReplaceAll()
    {
        // Arrange
        _engine.AddRoute(CreateTestRoute("route-1", "/api/users"));
        var newRoutes = new[]
        {
            CreateTestRoute("route-2", "/api/products"),
            CreateTestRoute("route-3", "/api/orders")
        };

        // Act
        _engine.UpdateRoutes(newRoutes);

        // Assert
        _engine.GetAllRoutes().Should().HaveCount(2);
        _engine.GetRoute("route-1").Should().BeNull();
        _engine.GetRoute("route-2").Should().NotBeNull();
        _engine.GetRoute("route-3").Should().NotBeNull();
    }

    [Test]
    public async Task MatchAsync_ExactPath_ShouldMatch()
    {
        // Arrange
        var route = CreateTestRoute("route-1", "/api/users");
        _engine.AddRoute(route);
        var context = CreateHttpContext("/api/users", "GET");

        // Act
        var result = await _engine.MatchAsync(context);

        // Assert
        result.Should().NotBeNull();
        result!.Route.RouteId.Should().Be("route-1");
        result.PathParameters.Should().BeEmpty();
    }

    [Test]
    public async Task MatchAsync_PathWithParameter_ShouldExtractParameter()
    {
        // Arrange
        var route = CreateTestRoute("route-1", "/api/users/{id}");
        _engine.AddRoute(route);
        var context = CreateHttpContext("/api/users/123", "GET");

        // Act
        var result = await _engine.MatchAsync(context);

        // Assert
        result.Should().NotBeNull();
        result!.Route.RouteId.Should().Be("route-1");
        result.PathParameters.Should().ContainKey("id");
        result.PathParameters["id"].Should().Be("123");
    }

    [Test]
    public async Task MatchAsync_CatchAllParameter_ShouldCaptureRemainingPath()
    {
        // Arrange
        var route = CreateTestRoute("route-1", "/api/{**catchall}");
        _engine.AddRoute(route);
        var context = CreateHttpContext("/api/users/123/orders", "GET");

        // Act
        var result = await _engine.MatchAsync(context);

        // Assert
        result.Should().NotBeNull();
        result!.PathParameters.Should().ContainKey("catchall");
        result.PathParameters["catchall"].Should().Be("users/123/orders");
    }

    [Test]
    public async Task MatchAsync_NoMatchingRoute_ShouldReturnNull()
    {
        // Arrange
        var route = CreateTestRoute("route-1", "/api/users");
        _engine.AddRoute(route);
        var context = CreateHttpContext("/api/products", "GET");

        // Act
        var result = await _engine.MatchAsync(context);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task MatchAsync_MethodMismatch_ShouldNotMatch()
    {
        // Arrange
        var route = CreateTestRoute("route-1", "/api/users", methods: ["POST"]);
        _engine.AddRoute(route);
        var context = CreateHttpContext("/api/users", "GET");

        // Act
        var result = await _engine.MatchAsync(context);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task MatchAsync_MethodMatch_ShouldMatch()
    {
        // Arrange
        var route = CreateTestRoute("route-1", "/api/users", methods: ["GET", "POST"]);
        _engine.AddRoute(route);
        var context = CreateHttpContext("/api/users", "GET");

        // Act
        var result = await _engine.MatchAsync(context);

        // Assert
        result.Should().NotBeNull();
    }

    [Test]
    public async Task MatchAsync_DisabledRoute_ShouldNotMatch()
    {
        // Arrange
        var route = CreateTestRoute("route-1", "/api/users", enabled: false);
        _engine.AddRoute(route);
        var context = CreateHttpContext("/api/users", "GET");

        // Act
        var result = await _engine.MatchAsync(context);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task MatchAsync_PriorityOrdering_ShouldMatchHigherPriorityFirst()
    {
        // Arrange
        var lowPriorityRoute = CreateTestRoute("route-low", "/api/{**catchall}", priority: 100);
        var highPriorityRoute = CreateTestRoute("route-high", "/api/users", priority: 1);
        _engine.AddRoute(lowPriorityRoute);
        _engine.AddRoute(highPriorityRoute);
        var context = CreateHttpContext("/api/users", "GET");

        // Act
        var result = await _engine.MatchAsync(context);

        // Assert
        result.Should().NotBeNull();
        result!.Route.RouteId.Should().Be("route-high");
    }

    [Test]
    public async Task MatchAsync_WithPathTransform_ShouldReturnTransformedPath()
    {
        // Arrange
        var route = CreateTestRoute("route-1", "/api/users/{id}", pathTransform: "/v1/users/{id}");
        _engine.AddRoute(route);
        var context = CreateHttpContext("/api/users/123", "GET");

        // Act
        var result = await _engine.MatchAsync(context);

        // Assert
        result.Should().NotBeNull();
        result!.TransformedPath.Should().Be("/v1/users/123");
    }

    private static GatewayRoute CreateTestRoute(
        string routeId,
        string path,
        List<string>? methods = null,
        int priority = 100,
        bool enabled = true,
        string? pathTransform = null)
    {
        return new GatewayRoute
        {
            RouteId = routeId,
            RouteName = routeId,
            Priority = priority,
            Enabled = enabled,
            Match = new RouteMatch
            {
                Path = path,
                Methods = methods ?? []
            },
            Destination = new RouteDestination
            {
                ServiceName = "test-service",
                PathTransform = pathTransform
            }
        };
    }

    private static HttpContext CreateHttpContext(string path, string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Request.Scheme = "http";
        return context;
    }
}
