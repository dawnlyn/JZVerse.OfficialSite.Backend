using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Tests.Integration;

/// <summary>
/// GrayReleaseController API 集成测试
/// </summary>
[TestFixture]
public class GrayReleaseControllerTests : IntegrationTestBase
{
    private static CreateGrayReleaseRequest CreateTestGrayReleaseRequest(string? namespaceId = null)
    {
        return new CreateGrayReleaseRequest
        {
            ReleaseName = $"Gray Release {DateTime.UtcNow:yyyyMMddHHmmss}",
            NamespaceId = namespaceId ?? $"ns-gray-{Guid.NewGuid():N}",
            EnvironmentId = "dev",
            Strategy = GrayReleaseStrategy.Manual,
            Rules =
            [
                new GrayReleaseRule
                {
                    RuleType = GrayRuleType.Percentage,
                    MatchPattern = "50",
                    Priority = 1
                }
            ],
            RolloutPercentage = 50
        };
    }

    #region Create Tests

    [Test]
    public async Task Create_ValidRelease_ShouldReturnCreated()
    {
        // Arrange
        var request = CreateTestGrayReleaseRequest();

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/gray-releases", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<GrayRelease>();
        result.Should().NotBeNull();
        result!.Status.Should().Be(GrayReleaseStatus.Draft);
    }

    #endregion

    #region Get Tests

    [Test]
    public async Task Get_ExistingRelease_ShouldReturnRelease()
    {
        // Arrange
        var request = CreateTestGrayReleaseRequest();
        var createResponse = await Client.PostAsJsonAsync("/api/v1/gray-releases", request);
        var created = await createResponse.Content.ReadFromJsonAsync<GrayRelease>();

        // Act
        var response = await Client.GetAsync($"/api/v1/gray-releases/{created!.ReleaseId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GrayRelease>();
        result.Should().NotBeNull();
        result!.ReleaseId.Should().Be(created.ReleaseId);
    }

    [Test]
    public async Task Get_NonExistingRelease_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/gray-releases/non-existing-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Lifecycle Tests

    [Test]
    public async Task Start_DraftRelease_ShouldReturnOk()
    {
        // Arrange
        var request = CreateTestGrayReleaseRequest();
        var createResponse = await Client.PostAsJsonAsync("/api/v1/gray-releases", request);
        var created = await createResponse.Content.ReadFromJsonAsync<GrayRelease>();

        // Act
        var response = await Client.PostAsync($"/api/v1/gray-releases/{created!.ReleaseId}/start", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify status changed
        var getResponse = await Client.GetAsync($"/api/v1/gray-releases/{created.ReleaseId}");
        var release = await getResponse.Content.ReadFromJsonAsync<GrayRelease>();
        release!.Status.Should().Be(GrayReleaseStatus.InProgress);
    }

    [Test]
    public async Task Complete_InProgressRelease_ShouldReturnOk()
    {
        // Arrange
        var request = CreateTestGrayReleaseRequest();
        var createResponse = await Client.PostAsJsonAsync("/api/v1/gray-releases", request);
        var created = await createResponse.Content.ReadFromJsonAsync<GrayRelease>();

        await Client.PostAsync($"/api/v1/gray-releases/{created!.ReleaseId}/start", null);

        // Act
        var response = await Client.PostAsync($"/api/v1/gray-releases/{created.ReleaseId}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify status changed
        var getResponse = await Client.GetAsync($"/api/v1/gray-releases/{created.ReleaseId}");
        var release = await getResponse.Content.ReadFromJsonAsync<GrayRelease>();
        release!.Status.Should().Be(GrayReleaseStatus.Completed);
    }

    [Test]
    public async Task Rollback_InProgressRelease_ShouldReturnOk()
    {
        // Arrange
        var request = CreateTestGrayReleaseRequest();
        var createResponse = await Client.PostAsJsonAsync("/api/v1/gray-releases", request);
        var created = await createResponse.Content.ReadFromJsonAsync<GrayRelease>();

        await Client.PostAsync($"/api/v1/gray-releases/{created!.ReleaseId}/start", null);

        // Act
        var response = await Client.PostAsync($"/api/v1/gray-releases/{created.ReleaseId}/rollback", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify status changed
        var getResponse = await Client.GetAsync($"/api/v1/gray-releases/{created.ReleaseId}");
        var release = await getResponse.Content.ReadFromJsonAsync<GrayRelease>();
        release!.Status.Should().Be(GrayReleaseStatus.Rollback);
    }

    #endregion

    #region Active Releases Tests

    [Test]
    public async Task GetActive_ShouldReturnInProgressReleases()
    {
        // Arrange
        var namespaceId = $"ns-active-{Guid.NewGuid():N}";
        var request = CreateTestGrayReleaseRequest(namespaceId);

        var createResponse = await Client.PostAsJsonAsync("/api/v1/gray-releases", request);
        var created = await createResponse.Content.ReadFromJsonAsync<GrayRelease>();

        await Client.PostAsync($"/api/v1/gray-releases/{created!.ReleaseId}/start", null);

        // Act
        var response = await Client.GetAsync(
            $"/api/v1/gray-releases/namespaces/{namespaceId}/active?environmentId=dev");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<GrayRelease>>();
        result.Should().NotBeNull();
        result!.Should().Contain(r => r.ReleaseId == created.ReleaseId);
    }

    #endregion

    #region Match Tests

    [Test]
    public async Task Match_WithMatchingClient_ShouldReturnMatchesTrue()
    {
        // Arrange
        var namespaceId = $"ns-match-{Guid.NewGuid():N}";
        var request = new CreateGrayReleaseRequest
        {
            ReleaseName = "Match Test Release",
            NamespaceId = namespaceId,
            EnvironmentId = "dev",
            Strategy = GrayReleaseStrategy.Manual,
            Rules =
            [
                new GrayReleaseRule
                {
                    RuleType = GrayRuleType.IP,
                    MatchPattern = "192.168.1.*",
                    Priority = 1
                }
            ],
            RolloutPercentage = 100
        };

        var createResponse = await Client.PostAsJsonAsync("/api/v1/gray-releases", request);
        var created = await createResponse.Content.ReadFromJsonAsync<GrayRelease>();

        await Client.PostAsync($"/api/v1/gray-releases/{created!.ReleaseId}/start", null);

        var clientInfo = new ClientInfo
        {
            ClientId = "test-client",
            IpAddress = "192.168.1.100",
            Tags = []
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/gray-releases/{created.ReleaseId}/match",
            clientInfo);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MatchResult>();
        result.Should().NotBeNull();
        result!.Matches.Should().BeTrue();
        result.ReleaseId.Should().Be(created.ReleaseId);
    }

    [Test]
    public async Task Match_NonExistingRelease_ShouldReturnNotFound()
    {
        // Arrange
        var clientInfo = new ClientInfo
        {
            ClientId = "no-match-client",
            IpAddress = "10.0.0.1",
            Tags = []
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/v1/gray-releases/non-existing-id/match",
            clientInfo);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}

/// <summary>
/// 创建灰度发布请求模型（用于测试）
/// </summary>
public sealed class CreateGrayReleaseRequest
{
    public string? ReleaseName { get; init; }
    public string? NamespaceId { get; init; }
    public string? EnvironmentId { get; init; }
    public GrayReleaseStrategy Strategy { get; init; }
    public List<GrayReleaseRule>? Rules { get; init; }
    public int RolloutPercentage { get; init; }
    public string? CreatedBy { get; init; }
}

/// <summary>
/// 匹配结果模型（用于测试）
/// </summary>
public sealed class MatchResult
{
    public bool Matches { get; init; }
    public required string ReleaseId { get; init; }
}
