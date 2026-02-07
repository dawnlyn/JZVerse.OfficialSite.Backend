using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Choreography;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using JZVerse.MicroHuaxia.Saga.Core.Compensation;
using JZVerse.MicroHuaxia.Saga.Core.Choreography;
using JZVerse.MicroHuaxia.Saga.Core.Orchestration;
using JZVerse.MicroHuaxia.Saga.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Saga.Tests.Unit;

/// <summary>
/// Saga 存储测试
/// </summary>
[TestFixture]
public class SagaStoreTests
{
    private MemorySagaStore _store = null!;

    [SetUp]
    public void Setup()
    {
        _store = new MemorySagaStore(NullLogger<MemorySagaStore>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public async Task SaveAsync_ShouldStoreSagaInstance()
    {
        // Arrange
        var instance = CreateTestInstance("test-saga-1");

        // Act
        await _store.SaveAsync(instance);
        var retrieved = await _store.GetAsync(instance.InstanceId);

        // Assert
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.InstanceId, Is.EqualTo(instance.InstanceId));
        Assert.That(retrieved.SagaId, Is.EqualTo(instance.SagaId));
    }

    [Test]
    public async Task SaveAsync_DuplicateId_ShouldThrow()
    {
        // Arrange
        var instance = CreateTestInstance("test-saga-2");
        await _store.SaveAsync(instance);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _store.SaveAsync(instance));
    }

    [Test]
    public async Task UpdateStatusAsync_ShouldChangeStatus()
    {
        // Arrange
        var instance = CreateTestInstance("test-saga-3");
        await _store.SaveAsync(instance);

        // Act
        await _store.UpdateStatusAsync(instance.InstanceId, SagaStatus.Executing);
        var retrieved = await _store.GetAsync(instance.InstanceId);

        // Assert
        Assert.That(retrieved!.Status, Is.EqualTo(SagaStatus.Executing));
    }

    [Test]
    public async Task UpdateStepStatusAsync_ShouldChangeStepStatus()
    {
        // Arrange
        var instance = CreateTestInstance("test-saga-4");
        instance.Steps.Add(new SagaStepInstance
        {
            StepId = "step-1",
            Name = "Test Step",
            Order = 0,
            Status = StepStatus.Pending
        });
        await _store.SaveAsync(instance);

        // Act
        await _store.UpdateStepStatusAsync(instance.InstanceId, "step-1", StepStatus.Completed);
        var retrieved = await _store.GetAsync(instance.InstanceId);

        // Assert
        Assert.That(retrieved!.Steps[0].Status, Is.EqualTo(StepStatus.Completed));
    }

    [Test]
    public async Task GetPendingInstancesAsync_ShouldReturnExecutingInstances()
    {
        // Arrange
        var instance1 = CreateTestInstance("pending-1");
        instance1.Status = SagaStatus.Executing;
        var instance2 = CreateTestInstance("pending-2");
        instance2.Status = SagaStatus.Completed;
        var instance3 = CreateTestInstance("pending-3");
        instance3.Status = SagaStatus.Compensating;

        await _store.SaveAsync(instance1);
        await _store.SaveAsync(instance2);
        await _store.SaveAsync(instance3);

        // Act
        var pending = await _store.GetPendingInstancesAsync();

        // Assert
        Assert.That(pending, Has.Count.EqualTo(2));
        Assert.That(pending.Any(i => i.InstanceId == instance1.InstanceId), Is.True);
        Assert.That(pending.Any(i => i.InstanceId == instance3.InstanceId), Is.True);
    }

    [Test]
    public async Task QueryAsync_ByStatus_ShouldReturnFilteredResults()
    {
        // Arrange
        var instance1 = CreateTestInstance("query-1");
        instance1.Status = SagaStatus.Completed;
        var instance2 = CreateTestInstance("query-2");
        instance2.Status = SagaStatus.Failed;
        var instance3 = CreateTestInstance("query-3");
        instance3.Status = SagaStatus.Completed;

        await _store.SaveAsync(instance1);
        await _store.SaveAsync(instance2);
        await _store.SaveAsync(instance3);

        // Act
        var completed = await _store.QueryAsync(status: SagaStatus.Completed);

        // Assert
        Assert.That(completed, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task DeleteAsync_ShouldRemoveInstance()
    {
        // Arrange
        var instance = CreateTestInstance("delete-1");
        await _store.SaveAsync(instance);

        // Act
        var deleted = await _store.DeleteAsync(instance.InstanceId);
        var retrieved = await _store.GetAsync(instance.InstanceId);

        // Assert
        Assert.That(deleted, Is.True);
        Assert.That(retrieved, Is.Null);
    }

    private static SagaInstance CreateTestInstance(string instanceId)
    {
        return new SagaInstance
        {
            InstanceId = instanceId,
            SagaId = "test-saga",
            Name = "Test Saga",
            Status = SagaStatus.Pending,
            Context = new SagaContext()
        };
    }
}

/// <summary>
/// Saga 状态机测试
/// </summary>
[TestFixture]
public class SagaStateMachineTests
{
    private SagaStateMachine _stateMachine = null!;

    [SetUp]
    public void Setup()
    {
        _stateMachine = new SagaStateMachine();
    }

    [Test]
    [TestCase(SagaStatus.Pending, SagaStatus.Executing, true)]
    [TestCase(SagaStatus.Executing, SagaStatus.Completed, true)]
    [TestCase(SagaStatus.Executing, SagaStatus.Compensating, true)]
    [TestCase(SagaStatus.Executing, SagaStatus.Failed, true)]
    [TestCase(SagaStatus.Compensating, SagaStatus.Compensated, true)]
    [TestCase(SagaStatus.Compensating, SagaStatus.Failed, true)]
    [TestCase(SagaStatus.Pending, SagaStatus.Completed, false)]
    [TestCase(SagaStatus.Completed, SagaStatus.Executing, false)]
    public void CanTransition_ShouldReturnCorrectResult(SagaStatus from, SagaStatus to, bool expected)
    {
        // Act
        var result = _stateMachine.CanTransition(from, to);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    [TestCase(StepStatus.Pending, StepStatus.Executing, true)]
    [TestCase(StepStatus.Executing, StepStatus.Completed, true)]
    [TestCase(StepStatus.Executing, StepStatus.Failed, true)]
    [TestCase(StepStatus.Completed, StepStatus.Compensating, true)]
    [TestCase(StepStatus.Compensating, StepStatus.Compensated, true)]
    [TestCase(StepStatus.Pending, StepStatus.Completed, false)]
    public void CanTransitionStep_ShouldReturnCorrectResult(StepStatus from, StepStatus to, bool expected)
    {
        // Act
        var result = _stateMachine.CanTransitionStep(from, to);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    [TestCase(SagaStatus.Completed, true)]
    [TestCase(SagaStatus.Compensated, true)]
    [TestCase(SagaStatus.Failed, true)]
    [TestCase(SagaStatus.TimedOut, true)]
    [TestCase(SagaStatus.Cancelled, true)]
    [TestCase(SagaStatus.Executing, false)]
    [TestCase(SagaStatus.Pending, false)]
    public void IsFinalState_ShouldReturnCorrectResult(SagaStatus status, bool expected)
    {
        // Act
        var result = _stateMachine.IsFinalState(status);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void NeedsCompensation_WithFailedStep_ShouldReturnTrue()
    {
        // Arrange
        var instance = new SagaInstance
        {
            InstanceId = "test",
            SagaId = "saga",
            Name = "Test",
            Steps =
            [
                new SagaStepInstance { StepId = "1", Name = "Step 1", Order = 0, Status = StepStatus.Completed, IsCompensable = true },
                new SagaStepInstance { StepId = "2", Name = "Step 2", Order = 1, Status = StepStatus.Failed, IsCompensable = true }
            ]
        };

        // Act
        var result = _stateMachine.NeedsCompensation(instance);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void NeedsCompensation_WithAllCompleted_ShouldReturnFalse()
    {
        // Arrange
        var instance = new SagaInstance
        {
            InstanceId = "test",
            SagaId = "saga",
            Name = "Test",
            Steps =
            [
                new SagaStepInstance { StepId = "1", Name = "Step 1", Order = 0, Status = StepStatus.Completed, IsCompensable = true },
                new SagaStepInstance { StepId = "2", Name = "Step 2", Order = 1, Status = StepStatus.Completed, IsCompensable = true }
            ]
        };

        // Act
        var result = _stateMachine.NeedsCompensation(instance);

        // Assert
        Assert.That(result, Is.False);
    }
}

/// <summary>
/// Saga 协调器测试
/// </summary>
[TestFixture]
public class SagaOrchestratorTests
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
    public async Task StartAsync_ShouldCreateSagaInstance()
    {
        // Arrange
        var saga = new TestSagaDefinition();
        var context = new SagaContext();
        context.Set("testKey", "testValue");

        // Act
        var instanceId = await _orchestrator.StartAsync(saga, context);

        // Assert
        Assert.That(instanceId, Is.Not.Null.And.Not.Empty);
        Assert.That(instanceId, Does.StartWith("SAGA"));

        // 等待一下让异步执行开始
        await Task.Delay(100);

        var instance = await _orchestrator.GetInstanceAsync(instanceId);
        Assert.That(instance, Is.Not.Null);
        Assert.That(instance!.SagaId, Is.EqualTo(saga.SagaId));
    }

    [Test]
    public async Task StartAsync_WithSuccessfulSteps_ShouldComplete()
    {
        // Arrange
        var saga = new TestSagaDefinition();

        // Act
        var instanceId = await _orchestrator.StartAsync(saga);

        // 等待执行完成
        await Task.Delay(500);

        // Assert
        var instance = await _orchestrator.GetInstanceAsync(instanceId);
        Assert.That(instance, Is.Not.Null);
        Assert.That(instance!.Status, Is.EqualTo(SagaStatus.Completed));
    }

    [Test]
    public async Task StartAsync_WithFailingStep_ShouldTriggerCompensation()
    {
        // Arrange
        var saga = new FailingSagaDefinition();

        // Act
        var instanceId = await _orchestrator.StartAsync(saga);

        // 等待执行和补偿完成
        await Task.Delay(1000);

        // Assert
        var instance = await _orchestrator.GetInstanceAsync(instanceId);
        Assert.That(instance, Is.Not.Null);
        Assert.That(instance!.Status, Is.EqualTo(SagaStatus.Compensated).Or.EqualTo(SagaStatus.Failed));
    }

    [Test]
    public async Task CancelAsync_ShouldSetCancelledStatus()
    {
        // Arrange
        var saga = new SlowSagaDefinition();
        var instanceId = await _orchestrator.StartAsync(saga);

        // Act
        var cancelled = await _orchestrator.CancelAsync(instanceId);

        // Assert
        Assert.That(cancelled, Is.True);
        var instance = await _orchestrator.GetInstanceAsync(instanceId);
        Assert.That(instance!.Status, Is.EqualTo(SagaStatus.Cancelled));
    }

    [Test]
    public async Task QueryInstancesAsync_ShouldReturnFilteredResults()
    {
        // Arrange
        var saga = new TestSagaDefinition();
        await _orchestrator.StartAsync(saga);
        await _orchestrator.StartAsync(saga);
        await Task.Delay(300);

        // Act
        var instances = await _orchestrator.QueryInstancesAsync(sagaId: saga.SagaId);

        // Assert
        Assert.That(instances, Has.Count.GreaterThanOrEqualTo(2));
    }

    // 测试 Saga 定义
    private class TestSagaDefinition : ISagaDefinition
    {
        public string SagaId => "test-saga";
        public string Name => "Test Saga";
        public TimeSpan? Timeout => TimeSpan.FromSeconds(30);
        public CompensationStrategy Strategy => CompensationStrategy.Backward;
        
        public IReadOnlyList<ISagaStep> Steps => new List<ISagaStep>
        {
            new DelegateSagaStep(
                "step-1", "Step 1", 0,
                (ctx, ct) => Task.FromResult(StepExecutionResult.Succeeded("Step 1 done")),
                (ctx, ct) => Task.FromResult(CompensationResult.Succeeded())),
            new DelegateSagaStep(
                "step-2", "Step 2", 1,
                (ctx, ct) => Task.FromResult(StepExecutionResult.Succeeded("Step 2 done")),
                (ctx, ct) => Task.FromResult(CompensationResult.Succeeded()))
        };
    }

    private class FailingSagaDefinition : ISagaDefinition
    {
        public string SagaId => "failing-saga";
        public string Name => "Failing Saga";
        public TimeSpan? Timeout => TimeSpan.FromSeconds(30);
        public CompensationStrategy Strategy => CompensationStrategy.Backward;
        
        public IReadOnlyList<ISagaStep> Steps => new List<ISagaStep>
        {
            new DelegateSagaStep(
                "step-1", "Step 1", 0,
                (ctx, ct) => Task.FromResult(StepExecutionResult.Succeeded("Step 1 done")),
                (ctx, ct) => Task.FromResult(CompensationResult.Succeeded())),
            new DelegateSagaStep(
                "step-2", "Step 2 (fails)", 1,
                (ctx, ct) => Task.FromResult(StepExecutionResult.Failed("Intentional failure")),
                null)
        };
    }

    private class SlowSagaDefinition : ISagaDefinition
    {
        public string SagaId => "slow-saga";
        public string Name => "Slow Saga";
        public TimeSpan? Timeout => TimeSpan.FromMinutes(5);
        public CompensationStrategy Strategy => CompensationStrategy.Backward;
        
        public IReadOnlyList<ISagaStep> Steps => new List<ISagaStep>
        {
            new DelegateSagaStep(
                "slow-step", "Slow Step", 0,
                async (ctx, ct) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), ct);
                    return StepExecutionResult.Succeeded();
                },
                null)
        };
    }
}

/// <summary>
/// Saga 上下文测试
/// </summary>
[TestFixture]
public class SagaContextTests
{
    [Test]
    public void Set_And_Get_ShouldWorkCorrectly()
    {
        // Arrange
        var context = new SagaContext();

        // Act
        context.Set("key1", "value1");
        context.Set("key2", 42);
        var value1 = context.Get<string>("key1");
        var value2 = context.Get<int>("key2");

        // Assert
        Assert.That(value1, Is.EqualTo("value1"));
        Assert.That(value2, Is.EqualTo(42));
    }

    [Test]
    public void SetStepResult_And_GetStepResult_ShouldWorkCorrectly()
    {
        // Arrange
        var context = new SagaContext();
        var result = new { OrderId = "123", Status = "Created" };

        // Act
        context.SetStepResult("create-order", result);
        var retrieved = context.GetStepResult<object>("create-order");

        // Assert
        Assert.That(retrieved, Is.Not.Null);
    }

    [Test]
    public void Get_NonExistentKey_ShouldReturnDefault()
    {
        // Arrange
        var context = new SagaContext();

        // Act
        var value = context.Get<string>("non-existent");

        // Assert
        Assert.That(value, Is.Null);
    }
}

/// <summary>
/// 执行结果测试
/// </summary>
[TestFixture]
public class ExecutionResultTests
{
    [Test]
    public void StepExecutionResult_Succeeded_ShouldSetCorrectProperties()
    {
        // Act
        var result = StepExecutionResult.Succeeded("test data");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Is.EqualTo("test data"));
        Assert.That(result.Error, Is.Null);
    }

    [Test]
    public void StepExecutionResult_Failed_ShouldSetCorrectProperties()
    {
        // Act
        var result = StepExecutionResult.Failed("error message", retryable: true);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo("error message"));
        Assert.That(result.Retryable, Is.True);
    }

    [Test]
    public void CompensationResult_Succeeded_ShouldBeSuccess()
    {
        // Act
        var result = CompensationResult.Succeeded();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Error, Is.Null);
    }

    [Test]
    public void RetryPolicy_GetDelay_ShouldUseExponentialBackoff()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromSeconds(30)
        };

        // Act
        var delay0 = policy.GetDelay(0);
        var delay1 = policy.GetDelay(1);
        var delay2 = policy.GetDelay(2);
        var delay10 = policy.GetDelay(10); // 应该被限制在 MaxDelay

        // Assert
        Assert.That(delay0.TotalSeconds, Is.EqualTo(1));
        Assert.That(delay1.TotalSeconds, Is.EqualTo(2));
        Assert.That(delay2.TotalSeconds, Is.EqualTo(4));
        Assert.That(delay10.TotalSeconds, Is.EqualTo(30)); // MaxDelay
    }
}

/// <summary>
/// 编排式 Saga 事件总线测试
/// </summary>
[TestFixture]
public class InMemorySagaEventBusTests
{
    private InMemorySagaEventBus _eventBus = null!;
    private ServiceProvider _serviceProvider = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestEventHandler>();
        _serviceProvider = services.BuildServiceProvider();
        
        _eventBus = new InMemorySagaEventBus(
            NullLogger<InMemorySagaEventBus>.Instance,
            _serviceProvider);
    }

    [TearDown]
    public void TearDown()
    {
        _eventBus.Dispose();
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task PublishAsync_WithSubscriber_ShouldInvokeHandler()
    {
        // Arrange
        var handlerCalled = false;
        var receivedEvent = default(TestCommandEvent);
        
        _eventBus.Subscribe<TestCommandEvent>((evt, ct) =>
        {
            handlerCalled = true;
            receivedEvent = evt;
            return Task.CompletedTask;
        });

        var testEvent = new TestCommandEvent
        {
            SagaInstanceId = "test-instance",
            TargetService = "OrderService",
            OrderId = "ORDER-123"
        };

        // Act
        await _eventBus.PublishAsync(testEvent);

        // Assert
        Assert.That(handlerCalled, Is.True);
        Assert.That(receivedEvent, Is.Not.Null);
        Assert.That(receivedEvent!.OrderId, Is.EqualTo("ORDER-123"));
    }

    [Test]
    public async Task PublishAsync_WithMultipleSubscribers_ShouldInvokeAllHandlers()
    {
        // Arrange
        var callCount = 0;
        
        _eventBus.Subscribe<TestCommandEvent>((evt, ct) =>
        {
            Interlocked.Increment(ref callCount);
            return Task.CompletedTask;
        });
        
        _eventBus.Subscribe<TestCommandEvent>((evt, ct) =>
        {
            Interlocked.Increment(ref callCount);
            return Task.CompletedTask;
        });

        var testEvent = new TestCommandEvent
        {
            SagaInstanceId = "test-instance",
            TargetService = "OrderService"
        };

        // Act
        await _eventBus.PublishAsync(testEvent);

        // Assert
        Assert.That(callCount, Is.EqualTo(2));
    }

    [Test]
    public async Task SendCommandAsync_ShouldPublishCommand()
    {
        // Arrange
        SagaCommandEvent? receivedCommand = null;
        
        _eventBus.Subscribe<TestCommandEvent>((evt, ct) =>
        {
            receivedCommand = evt;
            return Task.CompletedTask;
        });

        var command = new TestCommandEvent
        {
            SagaInstanceId = "test-instance",
            TargetService = "InventoryService",
            SourceService = "OrderService"
        };

        // Act
        await _eventBus.SendCommandAsync(command);

        // Assert
        Assert.That(receivedCommand, Is.Not.Null);
        Assert.That(receivedCommand!.TargetService, Is.EqualTo("InventoryService"));
    }

    [Test]
    public async Task PublishAsync_WithNoSubscribers_ShouldNotThrow()
    {
        // Arrange
        var testEvent = new TestCommandEvent
        {
            SagaInstanceId = "test-instance",
            TargetService = "NoOneListening"
        };

        // Act & Assert - should not throw
        await _eventBus.PublishAsync(testEvent);
        Assert.Pass();
    }

    [Test]
    public async Task PublishAsync_WithFailingHandler_ShouldContinueToNextHandler()
    {
        // Arrange
        var secondHandlerCalled = false;
        
        _eventBus.Subscribe<TestCommandEvent>((evt, ct) =>
        {
            throw new InvalidOperationException("Handler failed!");
        });
        
        _eventBus.Subscribe<TestCommandEvent>((evt, ct) =>
        {
            secondHandlerCalled = true;
            return Task.CompletedTask;
        });

        var testEvent = new TestCommandEvent
        {
            SagaInstanceId = "test-instance",
            TargetService = "OrderService"
        };

        // Act
        await _eventBus.PublishAsync(testEvent);

        // Assert
        Assert.That(secondHandlerCalled, Is.True);
    }

    [Test]
    public async Task Subscribe_TypedHandler_ShouldResolveFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var handlerInstance = new TestEventHandler();
        services.AddSingleton(handlerInstance);
        using var sp = services.BuildServiceProvider();
        
        using var eventBus = new InMemorySagaEventBus(
            NullLogger<InMemorySagaEventBus>.Instance, sp);
        
        eventBus.Subscribe<TestCommandEvent, TestEventHandler>();

        var testEvent = new TestCommandEvent
        {
            SagaInstanceId = "test-instance",
            TargetService = "OrderService"
        };

        // Act
        await eventBus.PublishAsync(testEvent);

        // Assert
        Assert.That(handlerInstance.HandledEvents, Has.Count.EqualTo(1));
    }

    // 测试事件类型
    private class TestEventHandler : ISagaEventHandler<TestCommandEvent>
    {
        public List<TestCommandEvent> HandledEvents { get; } = [];
        
        public Task HandleAsync(TestCommandEvent @event, CancellationToken cancellationToken = default)
        {
            HandledEvents.Add(@event);
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// 编排式 Saga 协调器测试
/// </summary>
[TestFixture]
public class ChoreographySagaCoordinatorTests
{
    private MemorySagaStore _store = null!;
    private InMemorySagaEventBus _eventBus = null!;
    private ChoreographySagaCoordinator _coordinator = null!;
    private ServiceProvider _serviceProvider = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        _serviceProvider = services.BuildServiceProvider();
        
        _store = new MemorySagaStore(NullLogger<MemorySagaStore>.Instance);
        _eventBus = new InMemorySagaEventBus(
            NullLogger<InMemorySagaEventBus>.Instance, _serviceProvider);
        _coordinator = new ChoreographySagaCoordinator(
            NullLogger<ChoreographySagaCoordinator>.Instance,
            _eventBus,
            _store);
    }

    [TearDown]
    public void TearDown()
    {
        _eventBus.Dispose();
        _store.Dispose();
        _serviceProvider.Dispose();
    }

    [Test]
    public void RegisterDefinition_ShouldStoreDefinition()
    {
        // Arrange
        var definition = new TestChoreographySagaDefinition();

        // Act
        _coordinator.RegisterDefinition(definition);

        // Assert - no exception means success
        Assert.Pass();
    }

    [Test]
    public async Task StartAsync_ShouldCreateInstanceAndPublishEvent()
    {
        // Arrange
        var definition = new TestChoreographySagaDefinition();
        _coordinator.RegisterDefinition(definition);
        
        SagaEvent? publishedEvent = null;
        _eventBus.Subscribe<TestCommandEvent>((evt, ct) =>
        {
            publishedEvent = evt;
            return Task.CompletedTask;
        });

        var startEvent = new TestCommandEvent
        {
            SagaInstanceId = "",
            TargetService = "OrderService"
        };

        // Act
        var instanceId = await _coordinator.StartAsync(definition.SagaId, startEvent);

        // Assert
        Assert.That(instanceId, Is.Not.Null.And.Not.Empty);
        Assert.That(instanceId, Does.StartWith("CSAGA"));
        Assert.That(publishedEvent, Is.Not.Null);
    }

    [Test]
    public async Task HandleAsync_SagaCompletedEvent_Success_ShouldProgressToNextParticipant()
    {
        // Arrange
        var definition = new TestChoreographySagaDefinition();
        _coordinator.RegisterDefinition(definition);
        
        var startEvent = new TestCommandEvent
        {
            SagaInstanceId = "",
            TargetService = "OrderService"
        };
        var instanceId = await _coordinator.StartAsync(definition.SagaId, startEvent);
        
        // Act
        var completedEvent = new SagaCompletedEvent
        {
            SagaInstanceId = instanceId,
            Success = true,
            Result = "Step 1 completed"
        };
        await _coordinator.HandleAsync(completedEvent);
        
        // Assert
        var instance = _coordinator.GetInstance(instanceId);
        Assert.That(instance, Is.Not.Null);
        Assert.That(instance!.CurrentParticipantIndex, Is.EqualTo(1));
        Assert.That(instance.CompletedParticipants, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task HandleAsync_AllParticipantsCompleted_ShouldMarkSagaAsCompleted()
    {
        // Arrange
        var definition = new SingleParticipantSagaDefinition();
        _coordinator.RegisterDefinition(definition);
        
        var startEvent = new TestCommandEvent
        {
            SagaInstanceId = "",
            TargetService = "SingleService"
        };
        var instanceId = await _coordinator.StartAsync(definition.SagaId, startEvent);
        
        // Act - complete the only participant
        var completedEvent = new SagaCompletedEvent
        {
            SagaInstanceId = instanceId,
            Success = true
        };
        await _coordinator.HandleAsync(completedEvent);
        
        // Assert
        var instance = _coordinator.GetInstance(instanceId);
        Assert.That(instance!.Status, Is.EqualTo(SagaStatus.Completed));
        Assert.That(instance.CompletedAt, Is.Not.Null);
    }

    [Test]
    public async Task HandleAsync_SagaCompletedEvent_Failure_ShouldTriggerCompensation()
    {
        // Arrange
        var definition = new TestChoreographySagaDefinition();
        _coordinator.RegisterDefinition(definition);
        
        SagaFailedEvent? failedEventPublished = null;
        _eventBus.Subscribe<SagaFailedEvent>((evt, ct) =>
        {
            failedEventPublished = evt;
            return Task.CompletedTask;
        });
        
        var startEvent = new TestCommandEvent
        {
            SagaInstanceId = "",
            TargetService = "OrderService"
        };
        var instanceId = await _coordinator.StartAsync(definition.SagaId, startEvent);
        
        // Complete first step
        await _coordinator.HandleAsync(new SagaCompletedEvent
        {
            SagaInstanceId = instanceId,
            Success = true
        });
        
        // Act - fail on second step
        var failedEvent = new SagaCompletedEvent
        {
            SagaInstanceId = instanceId,
            Success = false,
            Error = "Payment failed"
        };
        await _coordinator.HandleAsync(failedEvent);
        
        // Assert
        var instance = _coordinator.GetInstance(instanceId);
        Assert.That(instance!.Status, Is.EqualTo(SagaStatus.Compensating));
        Assert.That(instance.Error, Is.EqualTo("Payment failed"));
        Assert.That(failedEventPublished, Is.Not.Null);
        Assert.That(failedEventPublished!.StepsToCompensate, Has.Count.EqualTo(1)); // First participant
    }

    [Test]
    public async Task HandleAsync_SagaFailedEvent_ShouldTriggerCompensation()
    {
        // Arrange
        var definition = new TestChoreographySagaDefinition();
        _coordinator.RegisterDefinition(definition);
        
        var startEvent = new TestCommandEvent
        {
            SagaInstanceId = "",
            TargetService = "OrderService"
        };
        var instanceId = await _coordinator.StartAsync(definition.SagaId, startEvent);
        
        // Complete first step
        await _coordinator.HandleAsync(new SagaCompletedEvent
        {
            SagaInstanceId = instanceId,
            Success = true
        });
        
        // Act
        var failedEvent = new SagaFailedEvent
        {
            SagaInstanceId = instanceId,
            FailedStep = "PaymentService",
            Error = "External payment gateway error"
        };
        await _coordinator.HandleAsync(failedEvent);
        
        // Assert
        var instance = _coordinator.GetInstance(instanceId);
        Assert.That(instance!.Status, Is.EqualTo(SagaStatus.Compensating));
    }

    [Test]
    public void GetInstance_NonExistent_ShouldReturnNull()
    {
        // Act
        var instance = _coordinator.GetInstance("non-existent-id");
        
        // Assert
        Assert.That(instance, Is.Null);
    }

    [Test]
    public async Task StartAsync_WithUnregisteredSaga_ShouldThrow()
    {
        // Arrange
        var startEvent = new TestCommandEvent
        {
            SagaInstanceId = "",
            TargetService = "OrderService"
        };

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _coordinator.StartAsync("non-existent-saga", startEvent));
    }

    // 测试定义
    private class TestChoreographySagaDefinition : IChoreographySagaDefinition
    {
        public string SagaId => "test-choreography-saga";
        public string Name => "Test Choreography Saga";
        public TimeSpan? Timeout => TimeSpan.FromMinutes(5);
        
        public IReadOnlyList<SagaParticipantInfo> Participants =>
        [
            new SagaParticipantInfo
            {
                Name = "OrderService",
                Order = 0,
                TriggerEventType = typeof(TestCommandEvent),
                CompletedEventType = typeof(SagaCompletedEvent),
                CompensationEventType = typeof(TestCommandEvent),
                IsCompensable = true
            },
            new SagaParticipantInfo
            {
                Name = "PaymentService",
                Order = 1,
                TriggerEventType = typeof(TestCommandEvent),
                CompletedEventType = typeof(SagaCompletedEvent),
                CompensationEventType = typeof(TestCommandEvent),
                IsCompensable = true
            }
        ];
        
        public Type StartEventType => typeof(TestCommandEvent);
        public Type CompletedEventType => typeof(SagaCompletedEvent);
    }

    private class SingleParticipantSagaDefinition : IChoreographySagaDefinition
    {
        public string SagaId => "single-participant-saga";
        public string Name => "Single Participant Saga";
        public TimeSpan? Timeout => TimeSpan.FromMinutes(1);
        
        public IReadOnlyList<SagaParticipantInfo> Participants =>
        [
            new SagaParticipantInfo
            {
                Name = "SingleService",
                Order = 0,
                TriggerEventType = typeof(TestCommandEvent),
                CompletedEventType = typeof(SagaCompletedEvent),
                IsCompensable = false
            }
        ];
        
        public Type StartEventType => typeof(TestCommandEvent);
        public Type CompletedEventType => typeof(SagaCompletedEvent);
    }
}

/// <summary>
/// 测试用命令事件
/// </summary>
public record TestCommandEvent : SagaCommandEvent
{
    public string? OrderId { get; init; }
    public decimal? Amount { get; init; }
}
