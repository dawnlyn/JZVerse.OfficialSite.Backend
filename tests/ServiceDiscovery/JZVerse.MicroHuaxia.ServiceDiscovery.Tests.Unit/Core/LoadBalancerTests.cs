using FluentAssertions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using JZVerse.MicroHuaxia.ServiceDiscovery.Core.LoadBalancing;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Tests.Unit.Core;

/// <summary>
/// 负载均衡器单元测试
/// </summary>
[TestFixture]
public class LoadBalancerTests
{
    private static List<ServiceInstance> CreateTestInstances(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new ServiceInstance
            {
                InstanceId = $"instance-{i}",
                ServiceName = "test-service",
                Host = $"host-{i}",
                Port = 5000 + i,
                Health = HealthStatus.Healthy,
                Weight = i * 10  // 权重递增
            })
            .ToList();
    }

    #region RoundRobinLoadBalancer Tests

    [Test]
    public void RoundRobin_ShouldDistributeEvenly()
    {
        // Arrange
        var instances = CreateTestInstances(3);
        var loadBalancer = new RoundRobinLoadBalancer();

        // Act
        var results = new List<ServiceInstance>();
        for (int i = 0; i < 6; i++)
        {
            results.Add(loadBalancer.Select(instances)!);
        }

        // Assert
        results.Should().HaveCount(6);
        results.Count(r => r.InstanceId == "instance-1").Should().Be(2);
        results.Count(r => r.InstanceId == "instance-2").Should().Be(2);
        results.Count(r => r.InstanceId == "instance-3").Should().Be(2);
    }

    [Test]
    public void RoundRobin_EmptyList_ShouldReturnNull()
    {
        // Arrange
        var loadBalancer = new RoundRobinLoadBalancer();

        // Act
        var result = loadBalancer.Select([]);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void RoundRobin_SingleInstance_ShouldAlwaysReturnSame()
    {
        // Arrange
        var instances = CreateTestInstances(1);
        var loadBalancer = new RoundRobinLoadBalancer();

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            var result = loadBalancer.Select(instances);
            result!.InstanceId.Should().Be("instance-1");
        }
    }

    #endregion

    #region RandomLoadBalancer Tests

    [Test]
    public void Random_ShouldSelectFromAvailableInstances()
    {
        // Arrange
        var instances = CreateTestInstances(3);
        var loadBalancer = new RandomLoadBalancer();

        // Act
        var results = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            var result = loadBalancer.Select(instances);
            results.Add(result!.InstanceId);
        }

        // Assert - 在100次选择中应该覆盖所有实例
        results.Should().HaveCount(3);
    }

    [Test]
    public void Random_EmptyList_ShouldReturnNull()
    {
        // Arrange
        var loadBalancer = new RandomLoadBalancer();

        // Act
        var result = loadBalancer.Select([]);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region WeightedRoundRobinLoadBalancer Tests

    [Test]
    public void WeightedRoundRobin_ShouldRespectWeights()
    {
        // Arrange
        var instances = new List<ServiceInstance>
        {
            new() { InstanceId = "low-weight", ServiceName = "test", Host = "h1", Port = 1, Weight = 10 },
            new() { InstanceId = "high-weight", ServiceName = "test", Host = "h2", Port = 2, Weight = 30 }
        };
        var loadBalancer = new WeightedRoundRobinLoadBalancer();

        // Act
        var results = new Dictionary<string, int>();
        for (int i = 0; i < 100; i++)
        {
            var result = loadBalancer.Select(instances)!;
            results.TryGetValue(result.InstanceId, out var count);
            results[result.InstanceId] = count + 1;
        }

        // Assert - 高权重实例应该被选中更多次
        results["high-weight"].Should().BeGreaterThan(results["low-weight"]);
    }

    [Test]
    public void WeightedRoundRobin_EqualWeights_ShouldDistributeEvenly()
    {
        // Arrange
        var instances = new List<ServiceInstance>
        {
            new() { InstanceId = "inst-1", ServiceName = "test", Host = "h1", Port = 1, Weight = 100 },
            new() { InstanceId = "inst-2", ServiceName = "test", Host = "h2", Port = 2, Weight = 100 }
        };
        var loadBalancer = new WeightedRoundRobinLoadBalancer();

        // Act
        var results = new Dictionary<string, int>();
        for (int i = 0; i < 100; i++)
        {
            var result = loadBalancer.Select(instances)!;
            results.TryGetValue(result.InstanceId, out var count);
            results[result.InstanceId] = count + 1;
        }

        // Assert - 应该大致相等
        Math.Abs(results["inst-1"] - results["inst-2"]).Should().BeLessThan(20);
    }

    #endregion
}
