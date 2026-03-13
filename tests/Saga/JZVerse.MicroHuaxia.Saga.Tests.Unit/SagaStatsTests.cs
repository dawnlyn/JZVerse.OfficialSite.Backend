using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using JZVerse.MicroHuaxia.Saga.Core.Compensation;
using JZVerse.MicroHuaxia.Saga.Core.Orchestration;
using JZVerse.MicroHuaxia.Saga.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Saga.Tests.Unit;

/// <summary>
/// Saga 统计信息测试 - 验证 GetStats 端点所依赖的查询和聚合逻辑
/// </summary>
[TestFixture]
public class SagaStatsTests
{
    private MemorySagaStore _store = null!;
    private SagaOrchestrator _orchestrator = null!;

    [SetUp]
    public void Setup()
    {
        _store = new MemorySagaStore(NullLogger<MemorySagaStore>.Instance);
        var stateMachine = new SagaStateMachine();
        var compensationEngine = new CompensationEngine(
            NullLogger<CompensationEngine>.Instance, _store);

        _orchestrator = new SagaOrchestrator(
            NullLogger<SagaOrchestrator>.Instance,
            _store,
            stateMachine,
            compensationEngine,
            null!);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public async Task Stats_WithNoInstances_ShouldReturnZeroCounts()
    {
        // Act - mimic GetStats endpoint logic
        var instances = await _orchestrator.QueryInstancesAsync(limit: 10000);

        var stats = AggregateStats(instances);

        // Assert
        Assert.That(stats.TotalInstances, Is.EqualTo(0));
        Assert.That(stats.Executing, Is.EqualTo(0));
        Assert.That(stats.Completed, Is.EqualTo(0));
        Assert.That(stats.Failed, Is.EqualTo(0));
        Assert.That(stats.Compensating, Is.EqualTo(0));
        Assert.That(stats.Compensated, Is.EqualTo(0));
        Assert.That(stats.TimedOut, Is.EqualTo(0));
    }

    [Test]
    public async Task Stats_WithMixedStatusInstances_ShouldReturnCorrectCounts()
    {
        // Arrange - create instances with various statuses
        await SaveInstanceWithStatus("inst-exec-1", SagaStatus.Executing);
        await SaveInstanceWithStatus("inst-exec-2", SagaStatus.Executing);
        await SaveInstanceWithStatus("inst-comp-1", SagaStatus.Completed);
        await SaveInstanceWithStatus("inst-comp-2", SagaStatus.Completed);
        await SaveInstanceWithStatus("inst-comp-3", SagaStatus.Completed);
        await SaveInstanceWithStatus("inst-fail-1", SagaStatus.Failed);
        await SaveInstanceWithStatus("inst-compensating-1", SagaStatus.Compensating);
        await SaveInstanceWithStatus("inst-compensated-1", SagaStatus.Compensated);
        await SaveInstanceWithStatus("inst-compensated-2", SagaStatus.Compensated);
        await SaveInstanceWithStatus("inst-timeout-1", SagaStatus.TimedOut);

        // Act
        var instances = await _orchestrator.QueryInstancesAsync(limit: 10000);
        var stats = AggregateStats(instances);

        // Assert
        Assert.That(stats.TotalInstances, Is.EqualTo(10));
        Assert.That(stats.Executing, Is.EqualTo(2));
        Assert.That(stats.Completed, Is.EqualTo(3));
        Assert.That(stats.Failed, Is.EqualTo(1));
        Assert.That(stats.Compensating, Is.EqualTo(1));
        Assert.That(stats.Compensated, Is.EqualTo(2));
        Assert.That(stats.TimedOut, Is.EqualTo(1));
    }

    [Test]
    public async Task Stats_AllExecuting_ShouldCountCorrectly()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            await SaveInstanceWithStatus($"exec-{i}", SagaStatus.Executing);
        }

        // Act
        var instances = await _orchestrator.QueryInstancesAsync(limit: 10000);
        var stats = AggregateStats(instances);

        // Assert
        Assert.That(stats.TotalInstances, Is.EqualTo(5));
        Assert.That(stats.Executing, Is.EqualTo(5));
        Assert.That(stats.Completed, Is.EqualTo(0));
        Assert.That(stats.Failed, Is.EqualTo(0));
    }

    [Test]
    public async Task Stats_AllCompleted_ShouldCountCorrectly()
    {
        // Arrange
        for (int i = 0; i < 8; i++)
        {
            await SaveInstanceWithStatus($"comp-{i}", SagaStatus.Completed);
        }

        // Act
        var instances = await _orchestrator.QueryInstancesAsync(limit: 10000);
        var stats = AggregateStats(instances);

        // Assert
        Assert.That(stats.TotalInstances, Is.EqualTo(8));
        Assert.That(stats.Completed, Is.EqualTo(8));
        Assert.That(stats.Executing, Is.EqualTo(0));
    }

    [Test]
    public async Task Stats_QueryByStatus_ShouldFilterCorrectly()
    {
        // Arrange
        await SaveInstanceWithStatus("f1", SagaStatus.Failed);
        await SaveInstanceWithStatus("f2", SagaStatus.Failed);
        await SaveInstanceWithStatus("c1", SagaStatus.Completed);

        // Act - query only failed instances
        var failedInstances = await _orchestrator.QueryInstancesAsync(status: SagaStatus.Failed);

        // Assert
        Assert.That(failedInstances, Has.Count.EqualTo(2));
        Assert.That(failedInstances.All(i => i.Status == SagaStatus.Failed), Is.True);
    }

    [Test]
    public async Task Stats_WithCancelledInstances_ShouldNotCountInOtherCategories()
    {
        // Arrange
        await SaveInstanceWithStatus("cancelled-1", SagaStatus.Cancelled);
        await SaveInstanceWithStatus("cancelled-2", SagaStatus.Cancelled);
        await SaveInstanceWithStatus("exec-1", SagaStatus.Executing);

        // Act
        var instances = await _orchestrator.QueryInstancesAsync(limit: 10000);
        var stats = AggregateStats(instances);

        // Assert
        Assert.That(stats.TotalInstances, Is.EqualTo(3));
        Assert.That(stats.Executing, Is.EqualTo(1));
        Assert.That(stats.Completed, Is.EqualTo(0));
        Assert.That(stats.Failed, Is.EqualTo(0));
        Assert.That(stats.Compensating, Is.EqualTo(0));
        Assert.That(stats.Compensated, Is.EqualTo(0));
        Assert.That(stats.TimedOut, Is.EqualTo(0));
    }

    // Helper: save a saga instance with given status
    private async Task SaveInstanceWithStatus(string instanceId, SagaStatus status)
    {
        var instance = new SagaInstance
        {
            InstanceId = instanceId,
            SagaId = "test-saga",
            Name = "Test Saga",
            Status = status,
            Context = new SagaContext()
        };
        await _store.SaveAsync(instance);
    }

    // Helper: aggregate stats - mirrors the SagaController.GetStats logic
    private static SagaStatsResult AggregateStats(IReadOnlyList<SagaInstance> instances)
    {
        return new SagaStatsResult
        {
            TotalInstances = instances.Count,
            Executing = instances.Count(i => i.Status == SagaStatus.Executing),
            Completed = instances.Count(i => i.Status == SagaStatus.Completed),
            Failed = instances.Count(i => i.Status == SagaStatus.Failed),
            Compensating = instances.Count(i => i.Status == SagaStatus.Compensating),
            Compensated = instances.Count(i => i.Status == SagaStatus.Compensated),
            TimedOut = instances.Count(i => i.Status == SagaStatus.TimedOut)
        };
    }

    // Stats result DTO mirroring SagaController.GetStats response
    private sealed class SagaStatsResult
    {
        public int TotalInstances { get; set; }
        public int Executing { get; set; }
        public int Completed { get; set; }
        public int Failed { get; set; }
        public int Compensating { get; set; }
        public int Compensated { get; set; }
        public int TimedOut { get; set; }
    }
}
