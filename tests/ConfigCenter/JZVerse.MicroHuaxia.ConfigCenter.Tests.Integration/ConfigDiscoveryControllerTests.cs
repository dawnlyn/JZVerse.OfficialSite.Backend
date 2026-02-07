using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Tests.Integration;

/// <summary>
/// ConfigDiscoveryController API 集成测试
/// </summary>
[TestFixture]
public class ConfigDiscoveryControllerTests : IntegrationTestBase
{
    private static ConfigItemRequest CreateTestConfigRequest(string key, string value, string namespaceId = "default", string environmentId = "dev")
    {
        return new ConfigItemRequest
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Key = key,
            Value = value,
            NamespaceId = namespaceId,
            EnvironmentId = environmentId,
            ValueType = ConfigValueType.String,
        };
    }

    #region Query Tests

    [Test]
    public async Task Query_WithNamespaceAndEnvironment_ShouldReturnMatchingItems()
    {
        // Arrange
        var namespaceId = $"ns-{Guid.NewGuid():N}";
        var requests = new List<ConfigItemRequest>
        {
            CreateTestConfigRequest("query.key1", "value1", namespaceId),
            CreateTestConfigRequest("query.key2", "value2", namespaceId),
        };

        foreach (var request in requests)
        {
            await Client.PostAsJsonAsync("/api/v1/config/items", request);
        }

        var query = new ConfigQuery
        {
            ApplicationId = namespaceId, // Using namespaceId as applicationId for simplicity
            NamespaceId = namespaceId,
            EnvironmentId = "dev"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/discovery/query", query);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<ConfigItem>>();
        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
        result.Should().AllSatisfy(item => item.NamespaceId.Should().Be(namespaceId));
    }

    [Test]
    public async Task Query_WithKeys_ShouldReturnFilteredItems()
    {
        // Arrange
        var namespaceId = $"ns-filter-{Guid.NewGuid():N}";
        var requests = new List<ConfigItemRequest>
        {
            CreateTestConfigRequest("filter.key1", "value1", namespaceId),
            CreateTestConfigRequest("filter.key2", "value2", namespaceId),
            CreateTestConfigRequest("filter.key3", "value3", namespaceId),
        };

        foreach (var request in requests)
        {
            await Client.PostAsJsonAsync("/api/v1/config/items", request);
        }

        var query = new ConfigQuery
        {
            ApplicationId = namespaceId,
            NamespaceId = namespaceId,
            EnvironmentId = "dev",
            Keys = ["filter.key1", "filter.key3"]
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/discovery/query", query);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<ConfigItem>>();
        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
        result.Select(i => i.Key).Should().BeEquivalentTo(["filter.key1", "filter.key3"]);
    }

    #endregion

    #region GetByNamespace Tests

    [Test]
    public async Task GetByNamespace_ShouldReturnConfigDictionary()
    {
        // Arrange
        var namespaceId = $"ns-all-{Guid.NewGuid():N}";
        var requests = new List<ConfigItemRequest>
        {
            CreateTestConfigRequest("ns.key1", "value1", namespaceId),
            CreateTestConfigRequest("ns.key2", "value2", namespaceId),
        };

        foreach (var request in requests)
        {
            await Client.PostAsJsonAsync("/api/v1/config/items", request);
        }

        // Act
        var response = await Client.GetAsync(
            $"/api/v1/discovery/applications/{namespaceId}/environments/dev/namespaces/{namespaceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
        result.Should().ContainKey("ns.key1");
        result.Should().ContainKey("ns.key2");
    }

    #endregion

    #region GetValue Tests

    [Test]
    public async Task GetValue_ExistingKey_ShouldReturnValue()
    {
        // Arrange
        var namespaceId = $"ns-value-{Guid.NewGuid():N}";
        var key = "value.test.key";
        var request = CreateTestConfigRequest(key, "expected-value", namespaceId);
        await Client.PostAsJsonAsync("/api/v1/config/items", request);

        // Act - GetValue requires applicationId (using namespaceId as applicationId)
        var response = await Client.GetAsync(
            $"/api/v1/discovery/value?applicationId={namespaceId}&environmentId=dev&key={key}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadAsStringAsync();
        result.Should().Contain("expected-value");
    }

    [Test]
    public async Task GetValue_NonExistingKey_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync(
            "/api/v1/discovery/value?applicationId=app&environmentId=dev&key=non-existing");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
