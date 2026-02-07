using FluentAssertions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using JZVerse.MicroHuaxia.ServiceDiscovery.Core.Repositories;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Tests.Unit.Core;

/// <summary>
/// InMemoryServiceInstanceRepository 单元测试
/// </summary>
[TestFixture]
public class InMemoryServiceInstanceRepositoryTests
{
    private InMemoryServiceInstanceRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryServiceInstanceRepository();
    }

    private static ServiceInstance CreateTestInstance(
        string serviceName = "test-service",
        string? instanceId = null,
        HealthStatus health = HealthStatus.Healthy)
    {
        return new ServiceInstance
        {
            InstanceId = instanceId ?? Guid.NewGuid().ToString(),
            ServiceName = serviceName,
            Host = "localhost",
            Port = 5000,
            Health = health,
            Version = "1.0.0",
            RegisteredAt = DateTimeOffset.UtcNow,
            LastHeartbeatAt = DateTimeOffset.UtcNow
        };
    }

    #region AddAsync Tests

    [Test]
    public async Task AddAsync_ValidInstance_ShouldAddSuccessfully()
    {
        // Arrange
        var instance = CreateTestInstance();

        // Act
        var result = await _repository.AddAsync(instance);

        // Assert
        result.Should().NotBeNull();
        result.InstanceId.Should().Be(instance.InstanceId);
    }

    [Test]
    public async Task AddAsync_DuplicateInstanceId_ShouldThrowException()
    {
        // Arrange
        var instance = CreateTestInstance(instanceId: "duplicate-id");
        await _repository.AddAsync(instance);

        var duplicateInstance = CreateTestInstance(instanceId: "duplicate-id");

        // Act & Assert
        var act = () => _repository.AddAsync(duplicateInstance);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*已经存在*");
    }

    [Test]
    public async Task AddAsync_EmptyInstanceId_ShouldThrowArgumentException()
    {
        // Arrange
        var instance = new ServiceInstance
        {
            InstanceId = "",  // 使用空 InstanceId
            ServiceName = "test-service",
            Host = "localhost",
            Port = 5000
        };

        // Act & Assert
        var act = () => _repository.AddAsync(instance);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingInstance_ShouldReturnInstance()
    {
        // Arrange
        var instance = CreateTestInstance();
        await _repository.AddAsync(instance);

        // Act
        var result = await _repository.GetByIdAsync(instance.InstanceId);

        // Assert
        result.Should().NotBeNull();
        result!.InstanceId.Should().Be(instance.InstanceId);
    }

    [Test]
    public async Task GetByIdAsync_NonExistingInstance_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync("non-existing-id");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByServiceNameAsync Tests

    [Test]
    public async Task GetByServiceNameAsync_MultipleInstances_ShouldReturnAll()
    {
        // Arrange
        var serviceName = "my-service";
        var instance1 = CreateTestInstance(serviceName);
        var instance2 = CreateTestInstance(serviceName);
        var instance3 = CreateTestInstance("other-service");

        await _repository.AddAsync(instance1);
        await _repository.AddAsync(instance2);
        await _repository.AddAsync(instance3);

        // Act
        var result = await _repository.GetByServiceNameAsync(serviceName);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.ServiceName == serviceName);
    }

    [Test]
    public async Task GetByServiceNameAsync_NoInstances_ShouldReturnEmpty()
    {
        // Act
        var result = await _repository.GetByServiceNameAsync("non-existing-service");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region UpdateAsync Tests

    [Test]
    public async Task UpdateAsync_ExistingInstance_ShouldUpdateSuccessfully()
    {
        // Arrange
        var instance = CreateTestInstance();
        await _repository.AddAsync(instance);

        // 创建更新后的实例（init-only 属性不能修改，需要创建新实例）
        var updatedInstance = new ServiceInstance
        {
            InstanceId = instance.InstanceId,
            ServiceName = instance.ServiceName,
            Host = instance.Host,
            Port = instance.Port,
            Health = HealthStatus.Unhealthy,
            Version = "2.0.0",
            RegisteredAt = instance.RegisteredAt,
            LastHeartbeatAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _repository.UpdateAsync(updatedInstance);

        // Assert
        result.Should().BeTrue();

        var updated = await _repository.GetByIdAsync(instance.InstanceId);
        updated!.Health.Should().Be(HealthStatus.Unhealthy);
        updated.Version.Should().Be("2.0.0");
    }

    [Test]
    public async Task UpdateAsync_NonExistingInstance_ShouldReturnFalse()
    {
        // Arrange
        var instance = CreateTestInstance();

        // Act
        var result = await _repository.UpdateAsync(instance);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task UpdateAsync_ChangeServiceName_ShouldUpdateIndex()
    {
        // Arrange
        var instance = CreateTestInstance("old-service");
        await _repository.AddAsync(instance);

        // 创建新实例，使用新的服务名
        var updatedInstance = new ServiceInstance
        {
            InstanceId = instance.InstanceId,
            ServiceName = "new-service",
            Host = instance.Host,
            Port = instance.Port,
            Health = instance.Health,
            Version = instance.Version,
            RegisteredAt = instance.RegisteredAt,
            LastHeartbeatAt = DateTimeOffset.UtcNow
        };

        // Act
        await _repository.UpdateAsync(updatedInstance);

        // Assert
        var oldServiceInstances = await _repository.GetByServiceNameAsync("old-service");
        var newServiceInstances = await _repository.GetByServiceNameAsync("new-service");

        oldServiceInstances.Should().BeEmpty();
        newServiceInstances.Should().HaveCount(1);
    }

    #endregion

    #region RemoveAsync Tests

    [Test]
    public async Task RemoveAsync_ExistingInstance_ShouldRemoveSuccessfully()
    {
        // Arrange
        var instance = CreateTestInstance();
        await _repository.AddAsync(instance);

        // Act
        var result = await _repository.RemoveAsync(instance.InstanceId);

        // Assert
        result.Should().BeTrue();

        var removed = await _repository.GetByIdAsync(instance.InstanceId);
        removed.Should().BeNull();
    }

    [Test]
    public async Task RemoveAsync_NonExistingInstance_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.RemoveAsync("non-existing-id");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task RemoveAsync_ShouldUpdateServiceIndex()
    {
        // Arrange
        var instance = CreateTestInstance();
        await _repository.AddAsync(instance);

        // Act
        await _repository.RemoveAsync(instance.InstanceId);

        // Assert
        var instances = await _repository.GetByServiceNameAsync(instance.ServiceName);
        instances.Should().BeEmpty();
    }

    #endregion

    #region QueryAsync Tests

    [Test]
    public async Task QueryAsync_FilterByServiceName_ShouldReturnMatchingInstances()
    {
        // Arrange
        await _repository.AddAsync(CreateTestInstance("api-gateway"));
        await _repository.AddAsync(CreateTestInstance("api-users"));
        await _repository.AddAsync(CreateTestInstance("web-frontend"));

        // Act
        var result = await _repository.QueryAsync(new ServiceQuery { ServiceName = "api-*" });

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.ServiceName.StartsWith("api-"));
    }

    [Test]
    public async Task QueryAsync_FilterByHealthStatus_ShouldReturnMatchingInstances()
    {
        // Arrange
        await _repository.AddAsync(CreateTestInstance(health: HealthStatus.Healthy));
        await _repository.AddAsync(CreateTestInstance(health: HealthStatus.Healthy));
        await _repository.AddAsync(CreateTestInstance(health: HealthStatus.Unhealthy));

        // Act
        var result = await _repository.QueryAsync(new ServiceQuery { HealthStatus = HealthStatus.Healthy });

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.Health == HealthStatus.Healthy);
    }

    [Test]
    public async Task QueryAsync_OnlyHealthy_ShouldReturnOnlyHealthyInstances()
    {
        // Arrange
        await _repository.AddAsync(CreateTestInstance(health: HealthStatus.Healthy));
        await _repository.AddAsync(CreateTestInstance(health: HealthStatus.Unhealthy));
        await _repository.AddAsync(CreateTestInstance(health: HealthStatus.Unknown));

        // Act
        var result = await _repository.QueryAsync(new ServiceQuery { OnlyHealthy = true });

        // Assert
        result.Should().HaveCount(1);
        result.Should().OnlyContain(i => i.Health == HealthStatus.Healthy);
    }

    [Test]
    public async Task QueryAsync_FilterByVersion_ShouldReturnMatchingInstances()
    {
        // Arrange
        var instance1 = new ServiceInstance
        {
            InstanceId = Guid.NewGuid().ToString(),
            ServiceName = "test-service",
            Host = "localhost",
            Port = 5000,
            Version = "1.0.0",
            Health = HealthStatus.Healthy  // 设置健康状态以通过默认过滤
        };
        var instance2 = new ServiceInstance
        {
            InstanceId = Guid.NewGuid().ToString(),
            ServiceName = "test-service",
            Host = "localhost",
            Port = 5001,
            Version = "2.0.0",
            Health = HealthStatus.Healthy  // 设置健康状态以通过默认过滤
        };

        await _repository.AddAsync(instance1);
        await _repository.AddAsync(instance2);

        // Act
        var result = await _repository.QueryAsync(new ServiceQuery { Version = "1.0.0" });

        // Assert
        result.Should().HaveCount(1);
        result[0].Version.Should().Be("1.0.0");
    }

    #endregion

    #region GetAllAsync Tests

    [Test]
    public async Task GetAllAsync_ShouldReturnAllInstances()
    {
        // Arrange
        await _repository.AddAsync(CreateTestInstance("service-1"));
        await _repository.AddAsync(CreateTestInstance("service-2"));
        await _repository.AddAsync(CreateTestInstance("service-3"));

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Test]
    public async Task GetAllAsync_EmptyRepository_ShouldReturnEmpty()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetServiceNamesAsync Tests

    [Test]
    public async Task GetServiceNamesAsync_ShouldReturnDistinctServiceNames()
    {
        // Arrange
        await _repository.AddAsync(CreateTestInstance("service-a"));
        await _repository.AddAsync(CreateTestInstance("service-a"));
        await _repository.AddAsync(CreateTestInstance("service-b"));

        // Act
        var result = await _repository.GetServiceNamesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("service-a");
        result.Should().Contain("service-b");
    }

    #endregion
}
