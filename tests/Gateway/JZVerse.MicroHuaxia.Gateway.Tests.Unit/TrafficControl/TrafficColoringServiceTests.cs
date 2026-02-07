using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.TrafficControl;
using JZVerse.MicroHuaxia.Gateway.Core.TrafficControl;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Security.Claims;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.TrafficControl;

[TestFixture]
public class TrafficColoringServiceTests
{
    private TrafficColoringService _service = null!;

    [SetUp]
    public void Setup()
    {
        _service = new TrafficColoringService(NullLogger<TrafficColoringService>.Instance);
    }

    [Test]
    public async Task ApplyColoringAsync_DisabledConfig_ShouldReturnEmpty()
    {
        // Arrange
        var context = CreateHttpContext();
        var config = new RouteTrafficColoring { Enabled = false };

        // Act
        var result = await _service.ApplyColoringAsync(context, config);

        // Assert
        result.HasTags.Should().BeFalse();
    }

    [Test]
    public async Task ApplyColoringAsync_NoRules_ShouldReturnEmpty()
    {
        // Arrange
        var context = CreateHttpContext();
        var config = new RouteTrafficColoring { Enabled = true, Rules = [] };

        // Act
        var result = await _service.ApplyColoringAsync(context, config);

        // Assert
        result.HasTags.Should().BeFalse();
    }

    [Test]
    public async Task ApplyColoringAsync_HeaderMatchRule_ShouldMatchExactValue()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["X-Beta-User"] = "true";

        var config = new RouteTrafficColoring
        {
            Enabled = true,
            Rules =
            [
                new TrafficColoringRule
                {
                    Name = "beta-users",
                    Type = TrafficColoringType.HeaderMatch,
                    Tag = "beta",
                    HeaderName = "X-Beta-User",
                    HeaderValue = "true"
                }
            ]
        };

        // Act
        var result = await _service.ApplyColoringAsync(context, config);

        // Assert
        result.HasTags.Should().BeTrue();
        result.Tags.Should().Contain("beta");
        result.MatchedRule.Should().Be("beta-users");
    }

    [Test]
    public async Task ApplyColoringAsync_HeaderMatchRule_NoMatch_ShouldReturnEmpty()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["X-Beta-User"] = "false";

        var config = new RouteTrafficColoring
        {
            Enabled = true,
            Rules =
            [
                new TrafficColoringRule
                {
                    Name = "beta-users",
                    Type = TrafficColoringType.HeaderMatch,
                    Tag = "beta",
                    HeaderName = "X-Beta-User",
                    HeaderValue = "true"
                }
            ]
        };

        // Act
        var result = await _service.ApplyColoringAsync(context, config);

        // Assert
        result.HasTags.Should().BeFalse();
    }

    [Test]
    public async Task ApplyColoringAsync_UserWhitelistRule_ShouldMatchUserId()
    {
        // Arrange
        var context = CreateHttpContext(userId: "user-123");

        var config = new RouteTrafficColoring
        {
            Enabled = true,
            Rules =
            [
                new TrafficColoringRule
                {
                    Name = "whitelist",
                    Type = TrafficColoringType.UserWhitelist,
                    Tag = "canary",
                    UserIds = ["user-123", "user-456"]
                }
            ]
        };

        // Act
        var result = await _service.ApplyColoringAsync(context, config);

        // Assert
        result.HasTags.Should().BeTrue();
        result.Tags.Should().Contain("canary");
    }

    [Test]
    public async Task ApplyColoringAsync_MultipleRules_ShouldApplyAll()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["X-Test"] = "enabled";
        context.Request.Headers["X-Feature"] = "new";

        var config = new RouteTrafficColoring
        {
            Enabled = true,
            Rules =
            [
                new TrafficColoringRule
                {
                    Name = "test-rule",
                    Type = TrafficColoringType.HeaderMatch,
                    Tag = "test",
                    HeaderName = "X-Test",
                    HeaderValue = "enabled"
                },
                new TrafficColoringRule
                {
                    Name = "feature-rule",
                    Type = TrafficColoringType.HeaderMatch,
                    Tag = "feature-new",
                    HeaderName = "X-Feature",
                    HeaderValue = "new"
                }
            ]
        };

        // Act
        var result = await _service.ApplyColoringAsync(context, config);

        // Assert
        result.Tags.Should().HaveCount(2);
        result.Tags.Should().Contain("test");
        result.Tags.Should().Contain("feature-new");
    }

    [Test]
    public async Task ApplyColoringAsync_PercentageRule_ShouldBeConsistent()
    {
        // Arrange
        var context = CreateHttpContext(clientIp: "192.168.1.100");

        var config = new RouteTrafficColoring
        {
            Enabled = true,
            Rules =
            [
                new TrafficColoringRule
                {
                    Name = "canary-10",
                    Type = TrafficColoringType.Percentage,
                    Tag = "canary",
                    Percentage = 100, // 100% 确保命中
                    HashSource = TrafficColoringHashSource.ClientIp
                }
            ]
        };

        // Act - 多次调用应该返回相同结果
        var result1 = await _service.ApplyColoringAsync(context, config);
        var result2 = await _service.ApplyColoringAsync(context, config);

        // Assert
        result1.HasTags.Should().BeTrue();
        result1.Tags.Should().BeEquivalentTo(result2.Tags);
    }

    private static HttpContext CreateHttpContext(string? userId = null, string? clientIp = null)
    {
        var context = new DefaultHttpContext();

        if (!string.IsNullOrEmpty(userId))
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims, "Test");
            context.User = new ClaimsPrincipal(identity);
        }

        if (!string.IsNullOrEmpty(clientIp))
        {
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(clientIp);
        }

        return context;
    }
}
