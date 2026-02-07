using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Tests.Integration;

/// <summary>
/// ConfigController API 集成测试
/// </summary>
[TestFixture]
public class ConfigControllerTests : IntegrationTestBase
{
    private static ConfigItemRequest CreateTestConfigItemRequest(string? key = null, string? value = null)
    {
        return new ConfigItemRequest
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Key = key ?? $"test-key-{Guid.NewGuid():N}",
            Value = value ?? "test-value",
            NamespaceId = "default",
            EnvironmentId = "dev",
            ValueType = ConfigValueType.String,
        };
    }

    #region Set Config Tests

    [Test]
    public async Task SetConfig_ValidItem_ShouldReturnCreated()
    {
        // Arrange
        var request = CreateTestConfigItemRequest("api.url", "https://api.example.com");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/config/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ConfigItem>();
        result.Should().NotBeNull();
        result!.Key.Should().Be("api.url");
        result.Value.Should().Be("https://api.example.com");
        result.Version.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task SetConfig_UpdateExistingKey_ShouldIncrementVersion()
    {
        // Arrange
        var key = $"version-test-{Guid.NewGuid():N}";
        var request1 = CreateTestConfigItemRequest(key, "initial-value");

        // Act - First set
        var response1 = await Client.PostAsJsonAsync("/api/v1/config/items", request1);
        var result1 = await response1.Content.ReadFromJsonAsync<ConfigItem>();

        // Act - Update with same key
        var request2 = new ConfigItemRequest
        {
            ItemId = result1!.ItemId,
            Key = key,
            Value = "updated-value",
            NamespaceId = "default",
            EnvironmentId = "dev",
        };
        var response2 = await Client.PostAsJsonAsync("/api/v1/config/items", request2);
        var result2 = await response2.Content.ReadFromJsonAsync<ConfigItem>();

        // Assert
        result1!.Version.Should().Be(1);
        result2!.Version.Should().BeGreaterThan(result1.Version);
        result2.Value.Should().Be("updated-value");
    }

    #endregion

    #region Delete Config Tests

    [Test]
    public async Task DeleteConfig_ExistingItem_ShouldReturnNoContent()
    {
        // Arrange
        var request = CreateTestConfigItemRequest();
        var setResponse = await Client.PostAsJsonAsync("/api/v1/config/items", request);
        var savedItem = await setResponse.Content.ReadFromJsonAsync<ConfigItem>();

        // Act
        var response = await Client.DeleteAsync($"/api/v1/config/items/{savedItem!.ItemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task DeleteConfig_NonExistingItem_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.DeleteAsync("/api/v1/config/items/non-existing-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Get Config Tests

    [Test]
    public async Task GetConfig_ExistingItem_ShouldReturnItem()
    {
        // Arrange
        var request = CreateTestConfigItemRequest();
        var setResponse = await Client.PostAsJsonAsync("/api/v1/config/items", request);
        var savedItem = await setResponse.Content.ReadFromJsonAsync<ConfigItem>();

        // Act
        var response = await Client.GetAsync($"/api/v1/config/items/{savedItem!.ItemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ConfigItem>();
        result.Should().NotBeNull();
        result!.ItemId.Should().Be(savedItem.ItemId);
    }

    [Test]
    public async Task GetConfig_NonExistingItem_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/config/items/non-existing-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Batch Set Tests

    [Test]
    public async Task BatchSet_MultipleItems_ShouldSetAllItems()
    {
        // Arrange
        var requests = new List<ConfigItemRequest>
        {
            CreateTestConfigItemRequest($"batch-key-1-{Guid.NewGuid():N}", "value1"),
            CreateTestConfigItemRequest($"batch-key-2-{Guid.NewGuid():N}", "value2"),
            CreateTestConfigItemRequest($"batch-key-3-{Guid.NewGuid():N}", "value3"),
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/config/items/batch", requests);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}

/// <summary>
/// 配置项请求模型（用于测试）
/// </summary>
public sealed class ConfigItemRequest
{
    public string? ItemId { get; init; }
    public string? Key { get; init; }
    public string? Value { get; init; }
    public ConfigValueType ValueType { get; init; }
    public string? NamespaceId { get; init; }
    public string? EnvironmentId { get; init; }
    public string? Comment { get; init; }
    public bool IsSecret { get; init; }
    public bool IsRequired { get; init; }
    public string? UpdatedBy { get; init; }
}
