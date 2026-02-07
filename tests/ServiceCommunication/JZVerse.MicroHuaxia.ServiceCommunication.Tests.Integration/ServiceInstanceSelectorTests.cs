using FluentAssertions;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Configuration;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.ServiceDiscovery;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CommLoadBalancer = JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing.ILoadBalancer;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Tests.Integration;

/// <summary>
/// 服务实例选择器集成测试
/// </summary>
[TestFixture]
public class ServiceInstanceSelectorTests
{
    private Mock<IServiceDiscovery> _serviceDiscoveryMock = null!;
    private Mock<ILoadBalancerFactory> _loadBalancerFactoryMock = null!;
    private Mock<CommLoadBalancer> _loadBalancerMock = null!;
    private Mock<ILogger<ServiceInstanceSelector>> _loggerMock = null!;
    private ServiceInstanceSelector _selector = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceDiscoveryMock = new Mock<IServiceDiscovery>();
        _loadBalancerFactoryMock = new Mock<ILoadBalancerFactory>();
        _loadBalancerMock = new Mock<CommLoadBalancer>();
        _loggerMock = new Mock<ILogger<ServiceInstanceSelector>>();

        var options = Options.Create(new ServiceCommunicationOptions());

        _loadBalancerFactoryMock
            .Setup(f => f.GetOrCreate(It.IsAny<string>(), It.IsAny<LoadBalancerStrategy>()))
            .Returns(_loadBalancerMock.Object);

        _selector = new ServiceInstanceSelector(
            _serviceDiscoveryMock.Object,
            _loadBalancerFactoryMock.Object,
            options,
            _loggerMock.Object);
    }

    private static ServiceInstance CreateTestInstance(string instanceId, string serviceName, int port)
    {
        return new ServiceInstance
        {
            InstanceId = instanceId,
            ServiceName = serviceName,
            Host = "localhost",
            Port = port,
            Health = HealthStatus.Healthy,
            Enabled = true
        };
    }

    [Test]
    public async Task SelectAsync_WithAvailableInstances_ShouldReturnInstance()
    {
        // Arrange
        var serviceName = "test-service";
        var instances = new List<ServiceInstance>
        {
            CreateTestInstance("inst-1", serviceName, 5001),
            CreateTestInstance("inst-2", serviceName, 5002)
        };
        var selectedInstance = instances[0];

        _serviceDiscoveryMock.Setup(s => s.GetInstancesAsync(serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instances);

        _loadBalancerMock.Setup(lb => lb.Select(It.IsAny<IReadOnlyList<ServiceInstance>>(), It.IsAny<LoadBalancerContext?>()))
            .Returns(selectedInstance);

        // Act
        var result = await _selector.SelectAsync(serviceName);

        // Assert
        result.Should().NotBeNull();
        result!.InstanceId.Should().Be("inst-1");
    }

    [Test]
    public async Task SelectAsync_WithNoInstances_ShouldReturnNull()
    {
        // Arrange
        var serviceName = "empty-service";

        _serviceDiscoveryMock.Setup(s => s.GetInstancesAsync(serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceInstance>());

        // Act
        var result = await _selector.SelectAsync(serviceName);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task SelectAsync_WithContext_ShouldPassContextToLoadBalancer()
    {
        // Arrange
        var serviceName = "test-service";
        var instances = new List<ServiceInstance>
        {
            CreateTestInstance("inst-1", serviceName, 5001)
        };
        var context = new LoadBalancerContext
        {
            HashKey = "user-123",
            PreferredVersion = "1.0.0"
        };

        _serviceDiscoveryMock.Setup(s => s.GetInstancesAsync(serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instances);

        _loadBalancerMock.Setup(lb => lb.Select(It.IsAny<IReadOnlyList<ServiceInstance>>(), context))
            .Returns(instances[0]);

        // Act
        await _selector.SelectAsync(serviceName, context);

        // Assert
        _loadBalancerMock.Verify(lb => lb.Select(It.IsAny<IReadOnlyList<ServiceInstance>>(), context), Times.Once);
    }

    [Test]
    public async Task SelectAsync_ShouldFilterHealthyInstances()
    {
        // Arrange
        var serviceName = "test-service";
        var instances = new List<ServiceInstance>
        {
            CreateTestInstance("healthy-1", serviceName, 5001),
            new ServiceInstance
            {
                InstanceId = "unhealthy",
                ServiceName = serviceName,
                Host = "localhost",
                Port = 5002,
                Health = HealthStatus.Unhealthy,
                Enabled = true
            }
        };
        var healthyInstances = instances.Where(i => i.Health == HealthStatus.Healthy).ToList();

        _serviceDiscoveryMock.Setup(s => s.GetInstancesAsync(serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instances);

        _loadBalancerMock.Setup(lb => lb.Select(It.Is<IReadOnlyList<ServiceInstance>>(l => l.Count == 1), It.IsAny<LoadBalancerContext?>()))
            .Returns(healthyInstances[0]);

        // Act
        var result = await _selector.SelectAsync(serviceName);

        // Assert
        result.Should().NotBeNull();
        result!.InstanceId.Should().Be("healthy-1");
    }

    [Test]
    public async Task GetInstancesAsync_ShouldReturnOnlyHealthyAndEnabledInstances()
    {
        // Arrange
        var serviceName = "test-service";
        var instances = new List<ServiceInstance>
        {
            CreateTestInstance("healthy-enabled", serviceName, 5001),
            new ServiceInstance
            {
                InstanceId = "healthy-disabled",
                ServiceName = serviceName,
                Host = "localhost",
                Port = 5002,
                Health = HealthStatus.Healthy,
                Enabled = false
            },
            new ServiceInstance
            {
                InstanceId = "unhealthy-enabled",
                ServiceName = serviceName,
                Host = "localhost",
                Port = 5003,
                Health = HealthStatus.Unhealthy,
                Enabled = true
            }
        };

        _serviceDiscoveryMock.Setup(s => s.GetInstancesAsync(serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instances);

        // Act
        var result = await _selector.GetInstancesAsync(serviceName);

        // Assert
        result.Should().HaveCount(1);
        result[0].InstanceId.Should().Be("healthy-enabled");
    }

    [Test]
    public void ReportInstanceStatus_Success_ShouldLogDebug()
    {
        // Arrange
        var instance = CreateTestInstance("inst-1", "test-service", 5001);

        // Act
        _selector.ReportInstanceStatus(instance, success: true, duration: TimeSpan.FromMilliseconds(100));

        // Assert - 验证不会抛出异常
        _loggerMock.Verify(l => l.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Test]
    public void ReportInstanceStatus_Failure_ShouldLogWarning()
    {
        // Arrange
        var instance = CreateTestInstance("inst-1", "test-service", 5001);
        var exception = new Exception("Connection failed");

        // Act
        _selector.ReportInstanceStatus(instance, success: false, duration: TimeSpan.FromMilliseconds(500), exception);

        // Assert - 验证不会抛出异常
        _loggerMock.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            exception,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
