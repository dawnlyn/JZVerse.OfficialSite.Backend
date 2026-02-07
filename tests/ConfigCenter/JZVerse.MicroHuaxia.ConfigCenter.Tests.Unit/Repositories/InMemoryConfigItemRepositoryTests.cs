using FluentAssertions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using JZVerse.MicroHuaxia.ConfigCenter.Core.Repositories;

namespace JZVerse.MicroHuaxia.ConfigCenter.Tests.Unit.Repositories;

[TestFixture]
public class InMemoryConfigItemRepositoryTests
{
    private InMemoryConfigItemRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryConfigItemRepository();
    }

    [Test]
    public async Task AddAsync_ShouldAddConfigItem()
    {
        // Arrange
        var item = CreateConfigItem("key1", "value1");

        // Act
        var result = await _repository.AddAsync(item);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be("key1");
        result.Value.Should().Be("value1");
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnConfigItem_WhenExists()
    {
        // Arrange
        var item = CreateConfigItem("key1", "value1");
        await _repository.AddAsync(item);

        // Act
        var result = await _repository.GetByIdAsync(item.ItemId);

        // Assert
        result.Should().NotBeNull();
        result!.ItemId.Should().Be(item.ItemId);
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task GetByKeyAsync_ShouldReturnConfigItem_WhenExists()
    {
        // Arrange
        var item = CreateConfigItem("database.host", "localhost");
        await _repository.AddAsync(item);

        // Act
        var result = await _repository.GetByKeyAsync(item.NamespaceId, item.EnvironmentId, "database.host");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be("localhost");
    }

    [Test]
    public async Task GetByNamespaceAsync_ShouldReturnAllItemsInNamespace()
    {
        // Arrange
        var item1 = CreateConfigItem("key1", "value1", "ns1", "dev");
        var item2 = CreateConfigItem("key2", "value2", "ns1", "dev");
        var item3 = CreateConfigItem("key3", "value3", "ns2", "dev");

        await _repository.AddAsync(item1);
        await _repository.AddAsync(item2);
        await _repository.AddAsync(item3);

        // Act
        var result = await _repository.GetByNamespaceAsync("ns1", "dev");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(i => i.Key == "key1");
        result.Should().Contain(i => i.Key == "key2");
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateConfigItem()
    {
        // Arrange
        var item = CreateConfigItem("key1", "value1");
        await _repository.AddAsync(item);

        var updatedItem = item with { Value = "updated-value" };

        // Act
        var result = await _repository.UpdateAsync(updatedItem);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByIdAsync(item.ItemId);
        retrieved!.Value.Should().Be("updated-value");
    }

    [Test]
    public async Task RemoveAsync_ShouldRemoveConfigItem()
    {
        // Arrange
        var item = CreateConfigItem("key1", "value1");
        await _repository.AddAsync(item);

        // Act
        var result = await _repository.RemoveAsync(item.ItemId);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByIdAsync(item.ItemId);
        retrieved.Should().BeNull();
    }

    [Test]
    public async Task GetNextVersionAsync_ShouldReturnIncrementedVersion()
    {
        // Arrange
        var item = CreateConfigItem("key1", "value1");
        item = item with { Version = 5 };
        await _repository.AddAsync(item);

        // Act
        var nextVersion = await _repository.GetNextVersionAsync(item.ItemId);

        // Assert
        nextVersion.Should().Be(6);
    }

    [Test]
    public async Task QueryAsync_ShouldFilterByKeys()
    {
        // Arrange
        var item1 = CreateConfigItem("key1", "value1", "ns1", "dev");
        var item2 = CreateConfigItem("key2", "value2", "ns1", "dev");
        var item3 = CreateConfigItem("key3", "value3", "ns1", "dev");

        await _repository.AddAsync(item1);
        await _repository.AddAsync(item2);
        await _repository.AddAsync(item3);

        var query = new ConfigQuery
        {
            ApplicationId = "app1",
            EnvironmentId = "dev",
            NamespaceId = "ns1",
            Keys = ["key1", "key3"],
        };

        // Act
        var result = await _repository.QueryAsync(query);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(i => i.Key == "key1");
        result.Should().Contain(i => i.Key == "key3");
    }

    private static ConfigItem CreateConfigItem(
        string key,
        string value,
        string namespaceId = "default",
        string environmentId = "dev")
    {
        return new ConfigItem
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Key = key,
            Value = value,
            NamespaceId = namespaceId,
            EnvironmentId = environmentId,
        };
    }
}
