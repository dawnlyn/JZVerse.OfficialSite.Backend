using FluentAssertions;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Tests.Unit.LoadBalancing;

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
                Weight = i * 10
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

    [Test]
    public void RoundRobin_WithExcludedInstances_ShouldSkipExcluded()
    {
        // Arrange
        var instances = CreateTestInstances(3);
        var loadBalancer = new RoundRobinLoadBalancer();
        var context = new LoadBalancerContext
        {
            ExcludedInstanceIds = ["instance-2"]
        };

        // Act
        var results = new List<ServiceInstance>();
        for (int i = 0; i < 4; i++)
        {
            results.Add(loadBalancer.Select(instances, context)!);
        }

        // Assert
        results.Should().NotContain(r => r.InstanceId == "instance-2");
    }

    [Test]
    public void RoundRobin_WithPreferredVersion_ShouldFilterByVersion()
    {
        // Arrange
        var instances = new List<ServiceInstance>
        {
            new() { InstanceId = "v1-1", ServiceName = "test", Host = "h1", Port = 1, Version = "1.0.0" },
            new() { InstanceId = "v2-1", ServiceName = "test", Host = "h2", Port = 2, Version = "2.0.0" },
            new() { InstanceId = "v1-2", ServiceName = "test", Host = "h3", Port = 3, Version = "1.0.0" }
        };
        var loadBalancer = new RoundRobinLoadBalancer();
        var context = new LoadBalancerContext
        {
            PreferredVersion = "1.0.0"
        };

        // Act
        var results = new List<ServiceInstance>();
        for (int i = 0; i < 4; i++)
        {
            results.Add(loadBalancer.Select(instances, context)!);
        }

        // Assert
        results.Should().OnlyContain(r => r.Version == "1.0.0");
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

    [Test]
    public void Random_WithExcludedInstances_ShouldSkipExcluded()
    {
        // Arrange
        var instances = CreateTestInstances(3);
        var loadBalancer = new RandomLoadBalancer();
        var context = new LoadBalancerContext
        {
            ExcludedInstanceIds = ["instance-1", "instance-2"]
        };

        // Act
        var results = new List<ServiceInstance>();
        for (int i = 0; i < 10; i++)
        {
            results.Add(loadBalancer.Select(instances, context)!);
        }

        // Assert
        results.Should().OnlyContain(r => r.InstanceId == "instance-3");
    }

    #endregion

    #region WeightedRandomLoadBalancer Tests

    [Test]
    public void WeightedRandom_ShouldRespectWeights()
    {
        // Arrange
        var instances = new List<ServiceInstance>
        {
            new() { InstanceId = "low-weight", ServiceName = "test", Host = "h1", Port = 1, Weight = 10 },
            new() { InstanceId = "high-weight", ServiceName = "test", Host = "h2", Port = 2, Weight = 90 }
        };
        var loadBalancer = new WeightedRandomLoadBalancer();

        // Act
        var results = new Dictionary<string, int>();
        for (int i = 0; i < 1000; i++)
        {
            var result = loadBalancer.Select(instances)!;
            results.TryGetValue(result.InstanceId, out var count);
            results[result.InstanceId] = count + 1;
        }

        // Assert - 高权重实例应该被选中更多次
        results["high-weight"].Should().BeGreaterThan(results["low-weight"] * 2);
    }

    [Test]
    public void WeightedRandom_EmptyList_ShouldReturnNull()
    {
        // Arrange
        var loadBalancer = new WeightedRandomLoadBalancer();

        // Act
        var result = loadBalancer.Select([]);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void WeightedRandom_ZeroTotalWeight_ShouldReturnFirstInstance()
    {
        // Arrange
        var instances = new List<ServiceInstance>
        {
            new() { InstanceId = "first", ServiceName = "test", Host = "h1", Port = 1, Weight = 0 },
            new() { InstanceId = "second", ServiceName = "test", Host = "h2", Port = 2, Weight = 0 }
        };
        var loadBalancer = new WeightedRandomLoadBalancer();

        // Act
        var result = loadBalancer.Select(instances);

        // Assert
        result.Should().NotBeNull();
        result!.InstanceId.Should().Be("first");
    }

    #endregion

    #region ConsistentHashLoadBalancer Tests

    [Test]
    public void ConsistentHash_SameHashKey_ShouldReturnSameInstance()
    {
        // Arrange
        var instances = CreateTestInstances(5);
        var loadBalancer = new ConsistentHashLoadBalancer();
        var context = new LoadBalancerContext
        {
            HashKey = "user-123"
        };

        // Act
        var results = new List<ServiceInstance>();
        for (int i = 0; i < 10; i++)
        {
            results.Add(loadBalancer.Select(instances, context)!);
        }

        // Assert - 相同的 hash key 应该总是返回相同的实例
        var firstResult = results[0];
        results.Should().OnlyContain(r => r.InstanceId == firstResult.InstanceId);
    }

    [Test]
    public void ConsistentHash_DifferentHashKeys_ShouldDistribute()
    {
        // Arrange
        var instances = CreateTestInstances(3);
        var loadBalancer = new ConsistentHashLoadBalancer();

        // Act
        var results = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            var context = new LoadBalancerContext { HashKey = $"user-{i}" };
            var result = loadBalancer.Select(instances, context);
            results.Add(result!.InstanceId);
        }

        // Assert - 应该分布到多个实例
        results.Count.Should().BeGreaterThan(1);
    }

    [Test]
    public void ConsistentHash_EmptyList_ShouldReturnNull()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer();

        // Act
        var result = loadBalancer.Select([]);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void ConsistentHash_WithClientIdAsHashKey_ShouldBeConsistent()
    {
        // Arrange
        var instances = CreateTestInstances(5);
        var loadBalancer = new ConsistentHashLoadBalancer();
        var context = new LoadBalancerContext
        {
            ClientId = "client-abc"
        };

        // Act
        var results = new List<ServiceInstance>();
        for (int i = 0; i < 10; i++)
        {
            results.Add(loadBalancer.Select(instances, context)!);
        }

        // Assert
        var firstResult = results[0];
        results.Should().OnlyContain(r => r.InstanceId == firstResult.InstanceId);
    }

    #endregion
}
