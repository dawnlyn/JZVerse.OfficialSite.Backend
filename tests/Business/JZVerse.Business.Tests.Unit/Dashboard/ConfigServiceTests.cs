using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Dashboard.Arguments;
using JZVerse.Business.Dashboard.Database;
using JZVerse.Business.Dashboard.Services;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;

namespace JZVerse.Business.Tests.Unit.Dashboard;

/// <summary>
/// 配置服务单元测试
/// </summary>
[TestFixture]
public class ConfigServiceTests
{
    private Mock<IDbExecutor> _dbExecutorMock = null!;
    private Mock<ISnowflakeIdGenerator> _idGeneratorMock = null!;
    private ConfigService _configService = null!;

    [SetUp]
    public void SetUp()
    {
        _dbExecutorMock = new Mock<IDbExecutor>();
        _idGeneratorMock = new Mock<ISnowflakeIdGenerator>();
        _configService = new ConfigService(_dbExecutorMock.Object, _idGeneratorMock.Object);
    }

    #region SaveAsync Tests

    [Test]
    public async Task SaveAsync_ValidConfig_ShouldReturnTrue()
    {
        // Arrange
        var newId = Guid.NewGuid();
        _idGeneratorMock.Setup(x => x.GenerateGuid()).Returns(newId);
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        var arg = new ArgSaveConfig
        {
            ConfigKey = "site.name",
            ConfigValue = "JZVerse",
            Description = "Site name",
            Category = ConfigCategory.Basic,
            IsPublic = true
        };

        // Act
        var result = await _configService.SaveAsync(arg);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetByKeyAsync Tests

    [Test]
    public async Task GetByKeyAsync_ExistingKey_ShouldReturnConfig()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var config = new SystemConfigEntity
        {
            Id = configId,
            ConfigKey = "site.name",
            ConfigValue = "JZVerse",
            Category = ConfigCategory.Basic,
            IsPublic = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<SystemConfigEntity>(
            It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(config);

        // Act
        var result = await _configService.GetByKeyAsync("site.name");

        // Assert
        result.Should().NotBeNull();
        result!.ConfigKey.Should().Be("site.name");
        result.ConfigValue.Should().Be("JZVerse");
    }

    [Test]
    public async Task GetByKeyAsync_NonExistingKey_ShouldReturnNull()
    {
        // Arrange
        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<SystemConfigEntity>(
            It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync((SystemConfigEntity?)null);

        // Act
        var result = await _configService.GetByKeyAsync("non.existing");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetValueAsync Tests

    [Test]
    public async Task GetValueAsync_ExistingKey_ShouldReturnValue()
    {
        // Arrange
        var config = new SystemConfigEntity
        {
            ConfigKey = "site.name",
            ConfigValue = "JZVerse"
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<SystemConfigEntity>(
            It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(config);

        // Act
        var result = await _configService.GetValueAsync("site.name");

        // Assert
        result.Should().Be("JZVerse");
    }

    [Test]
    public async Task GetValueAsync_NonExistingKey_ShouldReturnDefaultValue()
    {
        // Arrange
        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<SystemConfigEntity>(
            It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync((SystemConfigEntity?)null);

        // Act
        var result = await _configService.GetValueAsync("non.existing", "default");

        // Assert
        result.Should().Be("default");
    }

    #endregion

    #region GetByCategoryAsync Tests

    [Test]
    public async Task GetByCategoryAsync_ShouldReturnConfigsInCategory()
    {
        // Arrange
        var configs = new List<SystemConfigEntity>
        {
            new() { ConfigKey = "basic.1", ConfigValue = "value1", Category = ConfigCategory.Basic },
            new() { ConfigKey = "basic.2", ConfigValue = "value2", Category = ConfigCategory.Basic }
        };

        _dbExecutorMock.Setup(x => x.QueryAsync<SystemConfigEntity>(
            It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(configs);

        // Act
        var result = await _configService.GetByCategoryAsync(ConfigCategory.Basic);

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_ExistingKey_ShouldReturnTrue()
    {
        // Arrange
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _configService.DeleteAsync("site.name");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task DeleteAsync_NonExistingKey_ShouldReturnFalse()
    {
        // Arrange
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(0);

        // Act
        var result = await _configService.DeleteAsync("non.existing");

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
