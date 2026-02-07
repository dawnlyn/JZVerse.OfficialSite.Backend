using FluentAssertions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using JZVerse.MicroHuaxia.ConfigCenter.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace JZVerse.MicroHuaxia.ConfigCenter.Tests.Unit.Core;

[TestFixture]
public class ConfigRegistryTests
{
    private Mock<IConfigItemRepository> _repositoryMock = null!;
    private Mock<IConfigVersionManager> _versionManagerMock = null!;
    private Mock<IConfigEventPublisher> _eventPublisherMock = null!;
    private Mock<IConfigCache> _cacheMock = null!;
    private ConfigRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryMock = new Mock<IConfigItemRepository>();
        _versionManagerMock = new Mock<IConfigVersionManager>();
        _eventPublisherMock = new Mock<IConfigEventPublisher>();
        _cacheMock = new Mock<IConfigCache>();

        _registry = new ConfigRegistry(
            _repositoryMock.Object,
            _versionManagerMock.Object,
            _eventPublisherMock.Object,
            _cacheMock.Object,
            NullLogger<ConfigRegistry>.Instance);
    }

    [Test]
    public async Task SetAsync_ShouldCreateNewItem_WhenKeyNotExists()
    {
        // Arrange
        var item = CreateConfigItem("new-key", "new-value");

        _repositoryMock.Setup(r => r.GetByKeyAsync(item.NamespaceId, item.EnvironmentId, item.Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigItem?)null);

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<ConfigItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigItem ci, CancellationToken _) => ci);

        // Act
        var result = await _registry.SetAsync(item);

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().Be(1);
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<ConfigItem>(), It.IsAny<CancellationToken>()), Times.Once);
        _versionManagerMock.Verify(v => v.RecordChangeAsync(
            It.Is<ConfigItem?>(x => x == null),
            It.IsAny<ConfigItem>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SetAsync_ShouldUpdateExistingItem_WhenKeyExists()
    {
        // Arrange
        var existingItem = CreateConfigItem("existing-key", "old-value");
        existingItem = existingItem with { Version = 3 };

        var newItem = CreateConfigItem("existing-key", "new-value");

        _repositoryMock.Setup(r => r.GetByKeyAsync(newItem.NamespaceId, newItem.EnvironmentId, newItem.Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingItem);

        _repositoryMock.Setup(r => r.GetNextVersionAsync(existingItem.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ConfigItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _registry.SetAsync(newItem);

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().Be(4);
        result.Value.Should().Be("new-value");
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ConfigItem>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SetAsync_ShouldPublishEvent()
    {
        // Arrange
        var item = CreateConfigItem("key", "value");

        _repositoryMock.Setup(r => r.GetByKeyAsync(item.NamespaceId, item.EnvironmentId, item.Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigItem?)null);

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<ConfigItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigItem ci, CancellationToken _) => ci);

        // Act
        await _registry.SetAsync(item);

        // Assert
        _eventPublisherMock.Verify(e => e.PublishAsync(
            It.Is<ConfigChangeEvent>(evt => evt.EventType == ConfigEventType.ItemChanged),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SetAsync_ShouldInvalidateCache()
    {
        // Arrange
        var item = CreateConfigItem("key", "value");

        _repositoryMock.Setup(r => r.GetByKeyAsync(item.NamespaceId, item.EnvironmentId, item.Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigItem?)null);

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<ConfigItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigItem ci, CancellationToken _) => ci);

        // Act
        await _registry.SetAsync(item);

        // Assert
        _cacheMock.Verify(c => c.InvalidateAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteAsync_ShouldReturnFalse_WhenItemNotExists()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigItem?)null);

        // Act
        var result = await _registry.DeleteAsync("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task DeleteAsync_ShouldDeleteAndPublishEvent_WhenItemExists()
    {
        // Arrange
        var item = CreateConfigItem("key", "value");

        _repositoryMock.Setup(r => r.GetByIdAsync(item.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        _repositoryMock.Setup(r => r.RemoveAsync(item.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _registry.DeleteAsync(item.ItemId);

        // Assert
        result.Should().BeTrue();
        _repositoryMock.Verify(r => r.RemoveAsync(item.ItemId, It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(e => e.PublishAsync(
            It.Is<ConfigChangeEvent>(evt => evt.EventType == ConfigEventType.ItemChanged),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetAsync_ShouldReturnItem_WhenExists()
    {
        // Arrange
        var item = CreateConfigItem("key", "value");

        _repositoryMock.Setup(r => r.GetByIdAsync(item.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        // Act
        var result = await _registry.GetAsync(item.ItemId);

        // Assert
        result.Should().NotBeNull();
        result!.ItemId.Should().Be(item.ItemId);
    }

    [Test]
    public async Task BatchSetAsync_ShouldSetAllItems()
    {
        // Arrange
        var items = new List<ConfigItem>
        {
            CreateConfigItem("key1", "value1"),
            CreateConfigItem("key2", "value2"),
            CreateConfigItem("key3", "value3"),
        };

        _repositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigItem?)null);

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<ConfigItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigItem ci, CancellationToken _) => ci);

        // Act
        var result = await _registry.BatchSetAsync(items);

        // Assert
        result.Should().BeTrue();
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<ConfigItem>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    private static ConfigItem CreateConfigItem(string key, string value)
    {
        return new ConfigItem
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Key = key,
            Value = value,
            NamespaceId = "default",
            EnvironmentId = "dev",
        };
    }
}
