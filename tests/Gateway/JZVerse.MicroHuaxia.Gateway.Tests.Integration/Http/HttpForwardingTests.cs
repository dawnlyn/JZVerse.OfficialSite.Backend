using System.Net;
using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Core.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Integration.Http;

[TestFixture]
public class HttpForwardingTests
{
    [Test]
    public async Task GatewayRouting_ExactPath_ShouldMatchAndSetRouteData()
    {
        // Arrange
        using var host = await CreateGatewayHostWithRouteCapture();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("users-list");
        content.Should().Contain("/backend/users");
    }

    [Test]
    public async Task GatewayRouting_PathWithParameter_ShouldExtractParameter()
    {
        // Arrange
        using var host = await CreateGatewayHostWithRouteCapture();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/users/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("users-get");
        content.Should().Contain("123");
    }

    [Test]
    public async Task GatewayRouting_PostMethod_ShouldMatchPostRoute()
    {
        // Arrange
        using var host = await CreateGatewayHostWithRouteCapture();
        var client = host.GetTestClient();
        var requestBody = new StringContent("""{"name":"test"}""", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/users", requestBody);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("users-create");
    }

    [Test]
    public async Task GatewayRouting_NonExistentRoute_ShouldReturn404()
    {
        // Arrange
        using var host = await CreateGatewayHostWithRouteCapture();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GatewayRouting_PriorityOrdering_ShouldMatchHigherPriorityFirst()
    {
        // Arrange
        using var host = await CreateGatewayHostWithPriorityRoutes();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("high-priority");
    }

    private static async Task<IHost> CreateGatewayHostWithRouteCapture()
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging(builder => builder.AddDebug());
                    services.AddSingleton<IRouteMatchingEngine, RouteMatchingEngine>();
                });
                webBuilder.Configure(app =>
                {
                    var routeEngine = app.ApplicationServices.GetRequiredService<IRouteMatchingEngine>();
                    ConfigureRoutes(routeEngine);

                    app.UseRouting();
                    
                    app.Use((Func<HttpContext, Func<Task>, Task>)(async (context, next) =>
                    {
                        var engine = context.RequestServices.GetRequiredService<IRouteMatchingEngine>();
                        var matchResult = await engine.MatchAsync(context);
                        
                        if (matchResult == null)
                        {
                            context.Response.StatusCode = 404;
                            await context.Response.WriteAsync("Not Found");
                            return;
                        }

                        // 返回路由匹配信息（模拟网关响应）
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "application/json";
                        
                        var pathParams = string.Join(", ", matchResult.PathParameters.Select(kv => $"{kv.Key}={kv.Value}"));
                        await context.Response.WriteAsync($$"""
                        {
                            "routeId": "{{matchResult.Route.RouteId}}",
                            "transformedPath": "{{matchResult.TransformedPath ?? matchResult.Route.Destination.PathTransform}}",
                            "pathParameters": "{{pathParams}}"
                        }
                        """);
                    }));
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private static async Task<IHost> CreateGatewayHostWithPriorityRoutes()
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging(builder => builder.AddDebug());
                    services.AddSingleton<IRouteMatchingEngine, RouteMatchingEngine>();
                });
                webBuilder.Configure(app =>
                {
                    var routeEngine = app.ApplicationServices.GetRequiredService<IRouteMatchingEngine>();
                    
                    // 添加低优先级的通配路由
                    routeEngine.AddRoute(new GatewayRoute
                    {
                        RouteId = "low-priority",
                        RouteName = "Low Priority",
                        Priority = 100,
                        Enabled = true,
                        Match = new RouteMatch { Path = "/api/{**catchall}", Methods = [] },
                        Destination = new RouteDestination { ServiceName = "test", PathTransform = "/low/{**catchall}" }
                    });
                    
                    // 添加高优先级的精确路由
                    routeEngine.AddRoute(new GatewayRoute
                    {
                        RouteId = "high-priority",
                        RouteName = "High Priority",
                        Priority = 1,
                        Enabled = true,
                        Match = new RouteMatch { Path = "/api/users", Methods = [] },
                        Destination = new RouteDestination { ServiceName = "test", PathTransform = "/high/users" }
                    });

                    app.UseRouting();
                    
                    app.Use((Func<HttpContext, Func<Task>, Task>)(async (context, next) =>
                    {
                        var engine = context.RequestServices.GetRequiredService<IRouteMatchingEngine>();
                        var matchResult = await engine.MatchAsync(context);
                        
                        if (matchResult == null)
                        {
                            context.Response.StatusCode = 404;
                            return;
                        }

                        context.Response.StatusCode = 200;
                        await context.Response.WriteAsync($$"""{"routeId": "{{matchResult.Route.RouteId}}"}""");
                    }));
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private static void ConfigureRoutes(IRouteMatchingEngine routeEngine)
    {
        routeEngine.AddRoute(new GatewayRoute
        {
            RouteId = "users-list",
            RouteName = "Users List",
            Priority = 1,
            Enabled = true,
            Match = new RouteMatch
            {
                Path = "/api/users",
                Methods = ["GET"]
            },
            Destination = new RouteDestination
            {
                ServiceName = "user-service",
                PathTransform = "/backend/users"
            }
        });

        routeEngine.AddRoute(new GatewayRoute
        {
            RouteId = "users-get",
            RouteName = "Get User",
            Priority = 1,
            Enabled = true,
            Match = new RouteMatch
            {
                Path = "/api/users/{id}",
                Methods = ["GET"]
            },
            Destination = new RouteDestination
            {
                ServiceName = "user-service",
                PathTransform = "/backend/users/{id}"
            }
        });

        routeEngine.AddRoute(new GatewayRoute
        {
            RouteId = "users-create",
            RouteName = "Create User",
            Priority = 1,
            Enabled = true,
            Match = new RouteMatch
            {
                Path = "/api/users",
                Methods = ["POST"]
            },
            Destination = new RouteDestination
            {
                ServiceName = "user-service",
                PathTransform = "/backend/users"
            }
        });
    }
}
