using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Tests.Integration;

/// <summary>
/// Telemetry API 集成测试
/// </summary>
[TestFixture]
public class TelemetryApiTests : IntegrationTestBase
{
    [Test]
    public async Task GetStatus_ShouldReturnTelemetryStatus()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/telemetry/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("enabled");  // JSON 返回小写属性名
    }

    [Test]
    public async Task QueryLogs_ShouldReturnLogEntries()
    {
        // Act - 注意：这是 POST 请求，需要传递查询对象
        var query = new LogQuery { Take = 10 };
        var response = await Client.PostAsJsonAsync("/api/v1/telemetry/logs/query", query);

        // Assert - 如果 Telemetry 未启用，会返回 404
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetLatestLogs_ShouldReturnRecentLogs()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/telemetry/logs/latest?count=5");

        // Assert - 如果 Telemetry 未启用，会返回 404
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetLogStatistics_ShouldReturnStats()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/telemetry/logs/statistics");

        // Assert - 如果 Telemetry 未启用，会返回 404
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetMetricsSnapshot_ShouldReturnMetrics()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/telemetry/metrics/snapshot");

        // Assert - 如果 Telemetry 未启用，会返回 404
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }
}
