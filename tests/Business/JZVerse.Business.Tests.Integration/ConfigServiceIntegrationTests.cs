using JZVerse.Business.Dashboard.Arguments;
using JZVerse.Business.Dashboard.Database;
using JZVerse.Business.Dashboard.Services;

namespace JZVerse.Business.Tests.Integration;

/// <summary>
/// 配置服务集成测试
/// </summary>
[TestFixture]
public class ConfigServiceIntegrationTests : PostgresTestBase
{
    private ConfigService _configService = null!;

    protected override string GetTableCreationSql() => """
        CREATE TABLE IF NOT EXISTS system_configs (
            id UUID PRIMARY KEY,
            config_key VARCHAR(100) NOT NULL UNIQUE,
            config_value TEXT NOT NULL,
            description VARCHAR(500),
            category INT NOT NULL DEFAULT 1,
            is_public BOOLEAN DEFAULT FALSE,
            created_at TIMESTAMP NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMP
        );
        """;

    [SetUp]
    public void SetUp()
    {
        var connectionFactory = new TestConnectionFactory(ConnectionString);
        var dbExecutor = new TestDbExecutor(connectionFactory);
        var idGenerator = new TestSnowflakeIdGenerator();

        _configService = new ConfigService(dbExecutor, idGenerator);
    }

    [TearDown]
    public async Task TearDown()
    {
        await ExecuteSqlAsync("DELETE FROM system_configs");
    }

    #region Integration Tests

    [Test]
    public async Task SaveAsync_NewConfig_ShouldPersistToDatabase()
    {
        // Arrange
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

        var value = await QueryScalarAsync<string>("SELECT config_value FROM system_configs WHERE config_key = 'site.name'");
        value.Should().Be("JZVerse");
    }

    [Test]
    public async Task SaveAsync_ExistingConfig_ShouldUpdateValue()
    {
        // Arrange
        var createArg = new ArgSaveConfig
        {
            ConfigKey = "site.name",
            ConfigValue = "OldValue",
            Category = ConfigCategory.Basic
        };
        await _configService.SaveAsync(createArg);

        var updateArg = new ArgSaveConfig
        {
            ConfigKey = "site.name",
            ConfigValue = "NewValue",
            Category = ConfigCategory.Basic
        };

        // Act
        var result = await _configService.SaveAsync(updateArg);

        // Assert
        result.Should().BeTrue();

        var value = await QueryScalarAsync<string>("SELECT config_value FROM system_configs WHERE config_key = 'site.name'");
        value.Should().Be("NewValue");
    }

    [Test]
    public async Task GetByKeyAsync_ExistingKey_ShouldReturnConfig()
    {
        // Arrange
        var arg = new ArgSaveConfig
        {
            ConfigKey = "test.key",
            ConfigValue = "test.value",
            Description = "Test config",
            Category = ConfigCategory.Module,
            IsPublic = false
        };
        await _configService.SaveAsync(arg);

        // Act
        var result = await _configService.GetByKeyAsync("test.key");

        // Assert
        result.Should().NotBeNull();
        result!.ConfigKey.Should().Be("test.key");
        result.ConfigValue.Should().Be("test.value");
        result.Category.Should().Be(ConfigCategory.Module);
    }

    [Test]
    public async Task GetValueAsync_ExistingKey_ShouldReturnValue()
    {
        // Arrange
        var arg = new ArgSaveConfig
        {
            ConfigKey = "site.title",
            ConfigValue = "JZVerse Official",
            Category = ConfigCategory.Basic
        };
        await _configService.SaveAsync(arg);

        // Act
        var result = await _configService.GetValueAsync("site.title");

        // Assert
        result.Should().Be("JZVerse Official");
    }

    [Test]
    public async Task GetValueAsync_NonExistingKey_ShouldReturnDefault()
    {
        // Act
        var result = await _configService.GetValueAsync("non.existing", "default");

        // Assert
        result.Should().Be("default");
    }

    [Test]
    public async Task GetByCategoryAsync_ShouldReturnConfigsInCategory()
    {
        // Arrange
        await _configService.SaveAsync(new ArgSaveConfig
        {
            ConfigKey = "basic.1",
            ConfigValue = "value1",
            Category = ConfigCategory.Basic
        });
        await _configService.SaveAsync(new ArgSaveConfig
        {
            ConfigKey = "basic.2",
            ConfigValue = "value2",
            Category = ConfigCategory.Basic
        });
        await _configService.SaveAsync(new ArgSaveConfig
        {
            ConfigKey = "module.1",
            ConfigValue = "value3",
            Category = ConfigCategory.Module
        });

        // Act
        var result = await _configService.GetByCategoryAsync(ConfigCategory.Basic);

        // Assert
        result.Should().HaveCount(2);
        result.All(c => c.Category == ConfigCategory.Basic).Should().BeTrue();
    }

    [Test]
    public async Task GetAllGroupedAsync_ShouldReturnGroupedConfigs()
    {
        // Arrange
        await _configService.SaveAsync(new ArgSaveConfig
        {
            ConfigKey = "basic.config",
            ConfigValue = "value1",
            Category = ConfigCategory.Basic
        });
        await _configService.SaveAsync(new ArgSaveConfig
        {
            ConfigKey = "module.config",
            ConfigValue = "value2",
            Category = ConfigCategory.Module
        });

        // Act
        var result = await _configService.GetAllGroupedAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(g => g.Category == ConfigCategory.Basic);
        result.Should().Contain(g => g.Category == ConfigCategory.Module);
    }

    [Test]
    public async Task GetPublicConfigsAsync_ShouldReturnOnlyPublicConfigs()
    {
        // Arrange
        await _configService.SaveAsync(new ArgSaveConfig
        {
            ConfigKey = "public.config",
            ConfigValue = "public",
            Category = ConfigCategory.Basic,
            IsPublic = true
        });
        await _configService.SaveAsync(new ArgSaveConfig
        {
            ConfigKey = "private.config",
            ConfigValue = "private",
            Category = ConfigCategory.Basic,
            IsPublic = false
        });

        // Act
        var result = await _configService.GetPublicConfigsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().ConfigKey.Should().Be("public.config");
    }

    [Test]
    public async Task DeleteAsync_ExistingKey_ShouldRemoveFromDatabase()
    {
        // Arrange
        await _configService.SaveAsync(new ArgSaveConfig
        {
            ConfigKey = "to.delete",
            ConfigValue = "value",
            Category = ConfigCategory.Basic
        });

        // Act
        var result = await _configService.DeleteAsync("to.delete");

        // Assert
        result.Should().BeTrue();

        var count = await QueryScalarAsync<int>("SELECT COUNT(*) FROM system_configs WHERE config_key = 'to.delete'");
        count.Should().Be(0);
    }

    #endregion
}
