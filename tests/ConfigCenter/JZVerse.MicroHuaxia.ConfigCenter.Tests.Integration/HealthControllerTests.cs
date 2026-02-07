using System.Net;
using FluentAssertions;

namespace JZVerse.MicroHuaxia.ConfigCenter.Tests.Integration;

/// <summary>
/// HealthController API 集成测试
/// </summary>
[TestFixture]
public class HealthControllerTests : IntegrationTestBase
{
    [Test]
    public async Task Health_ShouldReturnOk()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
    }

    [Test]
    public async Task Alive_ShouldReturnOk()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/alive");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("alive");
    }
}
