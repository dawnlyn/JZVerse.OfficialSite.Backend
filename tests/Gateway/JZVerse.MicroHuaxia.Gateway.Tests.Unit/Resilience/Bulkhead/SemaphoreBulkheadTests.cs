using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;
using JZVerse.MicroHuaxia.Gateway.Resilience.Bulkhead;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Resilience.Bulkhead;

[TestFixture]
public class SemaphoreBulkheadTests
{
    private Mock<ILogger<SemaphoreBulkhead>> _loggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<SemaphoreBulkhead>>();
    }

    [Test]
    public async Task ExecuteAsync_WithinConcurrencyLimit_ShouldExecuteSuccessfully()
    {
        // Arrange
        var options = new BulkheadOptions { MaxConcurrency = 2, MaxQueueLength = 2 };
        using var bulkhead = new SemaphoreBulkhead("test", options, _loggerMock.Object);

        // Act
        var result = await bulkhead.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return 42;
        });

        // Assert
        result.Should().Be(42);
        bulkhead.CurrentConcurrency.Should().Be(0);
    }

    [Test]
    public async Task ExecuteAsync_Concurrent_ShouldTrackConcurrency()
    {
        // Arrange
        var options = new BulkheadOptions { MaxConcurrency = 2, MaxQueueLength = 10 };
        using var bulkhead = new SemaphoreBulkhead("test", options, _loggerMock.Object);
        var tcs = new TaskCompletionSource();

        // Act
        var task1 = bulkhead.ExecuteAsync(async ct =>
        {
            await tcs.Task;
            return 1;
        });

        var task2 = bulkhead.ExecuteAsync(async ct =>
        {
            await tcs.Task;
            return 2;
        });

        // 等待任务开始执行
        await Task.Delay(50);

        // Assert - 两个任务正在执行
        bulkhead.CurrentConcurrency.Should().Be(2);

        // 完成任务
        tcs.SetResult();
        await Task.WhenAll(task1, task2);

        bulkhead.CurrentConcurrency.Should().Be(0);
    }

    [Test]
    public async Task ExecuteAsync_ExceedQueueLength_ShouldRejectImmediately()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            MaxConcurrency = 1,
            MaxQueueLength = 1,
            QueueTimeout = TimeSpan.FromSeconds(30)
        };
        using var bulkhead = new SemaphoreBulkhead("test", options, _loggerMock.Object);
        var tcs = new TaskCompletionSource();

        // Act - 启动一个长时间运行的任务占用并发槽
        var task1 = bulkhead.ExecuteAsync(async ct =>
        {
            await tcs.Task;
            return 1;
        });

        await Task.Delay(50);

        // 第二个任务会进入队列
        var task2Started = false;
        var task2 = Task.Run(async () =>
        {
            task2Started = true;
            return await bulkhead.ExecuteAsync(async ct =>
            {
                await Task.Delay(10, ct);
                return 2;
            });
        });

        await Task.Delay(50);
        task2Started.Should().BeTrue();

        // 第三个任务应该被立即拒绝（队列已满）
        var act = () => bulkhead.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return 3;
        });

        // Assert
        await act.Should().ThrowAsync<BulkheadRejectedException>()
            .Where(ex => ex.BulkheadName == "test");

        // 清理
        tcs.SetResult();
        await task1;
        await task2;
    }

    [Test]
    public async Task ExecuteAsync_QueueTimeout_ShouldThrowException()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            MaxConcurrency = 1,
            MaxQueueLength = 10,
            QueueTimeout = TimeSpan.FromMilliseconds(100)
        };
        using var bulkhead = new SemaphoreBulkhead("test", options, _loggerMock.Object);
        var tcs = new TaskCompletionSource();

        // Act - 启动一个长时间运行的任务占用并发槽
        var task1 = bulkhead.ExecuteAsync(async ct =>
        {
            await tcs.Task;
            return 1;
        });

        await Task.Delay(50);

        // 第二个任务将等待并超时
        var act = () => bulkhead.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return 2;
        });

        // Assert
        await act.Should().ThrowAsync<BulkheadRejectedException>()
            .Where(ex => ex.Message.Contains("timeout"));

        // 清理
        tcs.SetResult();
        await task1;
    }

    [Test]
    public void Properties_ShouldReflectConfiguration()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            MaxConcurrency = 10,
            MaxQueueLength = 20,
            QueueTimeout = TimeSpan.FromSeconds(5)
        };

        // Act
        using var bulkhead = new SemaphoreBulkhead("test-bulkhead", options, _loggerMock.Object);

        // Assert
        bulkhead.Name.Should().Be("test-bulkhead");
        bulkhead.MaxConcurrency.Should().Be(10);
        bulkhead.MaxQueueLength.Should().Be(20);
        bulkhead.CurrentConcurrency.Should().Be(0);
        bulkhead.CurrentQueueLength.Should().Be(0);
    }

    [Test]
    public async Task ExecuteAsync_VoidVersion_ShouldExecuteSuccessfully()
    {
        // Arrange
        var options = new BulkheadOptions { MaxConcurrency = 2, MaxQueueLength = 2 };
        using var bulkhead = new SemaphoreBulkhead("test", options, _loggerMock.Object);
        var executed = false;

        // Act
        await bulkhead.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            executed = true;
        });

        // Assert
        executed.Should().BeTrue();
    }

    [Test]
    public void Dispose_AfterDisposed_ShouldThrowOnExecute()
    {
        // Arrange
        var options = new BulkheadOptions { MaxConcurrency = 2, MaxQueueLength = 2 };
        var bulkhead = new SemaphoreBulkhead("test", options, _loggerMock.Object);

        // Act
        bulkhead.Dispose();

        // Assert
        var act = async () => await bulkhead.ExecuteAsync(async ct => await Task.FromResult(1));
        act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task ExecuteAsync_WithCancellation_ShouldCancel()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            MaxConcurrency = 1,
            MaxQueueLength = 10,
            QueueTimeout = TimeSpan.FromSeconds(30)
        };
        using var bulkhead = new SemaphoreBulkhead("test", options, _loggerMock.Object);
        var tcs = new TaskCompletionSource();
        using var cts = new CancellationTokenSource();

        // 启动一个长时间运行的任务占用并发槽
        var task1 = bulkhead.ExecuteAsync(async ct =>
        {
            await tcs.Task;
            return 1;
        });

        await Task.Delay(50);

        // Act - 第二个任务在队列中等待，然后被取消
        var task2 = bulkhead.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return 2;
        }, cts.Token);

        await Task.Delay(50);
        cts.Cancel();

        // Assert
        var act = async () => await task2;
        await act.Should().ThrowAsync<OperationCanceledException>();

        // 清理
        tcs.SetResult();
        await task1;
    }
}

[TestFixture]
public class BulkheadFactoryTests
{
    private Mock<ILoggerFactory> _loggerFactoryMock = null!;
    private Mock<ILogger<SemaphoreBulkhead>> _bulkheadLoggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _bulkheadLoggerMock = new Mock<ILogger<SemaphoreBulkhead>>();
        _loggerFactoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(_bulkheadLoggerMock.Object);
    }

    [Test]
    public void GetOrCreate_ShouldReturnSameBulkheadForSameName()
    {
        // Arrange
        using var factory = new BulkheadFactory(_loggerFactoryMock.Object);

        // Act
        var bulkhead1 = factory.GetOrCreate("test");
        var bulkhead2 = factory.GetOrCreate("test");

        // Assert
        bulkhead1.Should().BeSameAs(bulkhead2);
    }

    [Test]
    public void GetOrCreate_ShouldReturnDifferentBulkheadForDifferentName()
    {
        // Arrange
        using var factory = new BulkheadFactory(_loggerFactoryMock.Object);

        // Act
        var bulkhead1 = factory.GetOrCreate("test1");
        var bulkhead2 = factory.GetOrCreate("test2");

        // Assert
        bulkhead1.Should().NotBeSameAs(bulkhead2);
        bulkhead1.Name.Should().Be("test1");
        bulkhead2.Name.Should().Be("test2");
    }

    [Test]
    public void GetOrCreate_WithCustomOptions_ShouldApplyOptions()
    {
        // Arrange
        using var factory = new BulkheadFactory(_loggerFactoryMock.Object);
        var customOptions = new BulkheadOptions
        {
            MaxConcurrency = 50,
            MaxQueueLength = 100
        };

        // Act
        var bulkhead = factory.GetOrCreate("custom", customOptions);

        // Assert
        bulkhead.MaxConcurrency.Should().Be(50);
        bulkhead.MaxQueueLength.Should().Be(100);
    }

    [Test]
    public void GetOrCreate_WithDefaultOptions_ShouldApplyDefaults()
    {
        // Arrange
        var defaultOptions = new BulkheadOptions
        {
            MaxConcurrency = 25,
            MaxQueueLength = 50
        };
        using var factory = new BulkheadFactory(_loggerFactoryMock.Object, defaultOptions);

        // Act
        var bulkhead = factory.GetOrCreate("test");

        // Assert
        bulkhead.MaxConcurrency.Should().Be(25);
        bulkhead.MaxQueueLength.Should().Be(50);
    }

    [Test]
    public void GetAllStatuses_ShouldReturnAllBulkheadStatuses()
    {
        // Arrange
        using var factory = new BulkheadFactory(_loggerFactoryMock.Object);
        factory.GetOrCreate("bulkhead1", new BulkheadOptions { MaxConcurrency = 10 });
        factory.GetOrCreate("bulkhead2", new BulkheadOptions { MaxConcurrency = 20 });

        // Act
        var statuses = factory.GetAllStatuses();

        // Assert
        statuses.Should().HaveCount(2);
        statuses.Should().ContainKey("bulkhead1");
        statuses.Should().ContainKey("bulkhead2");
        statuses["bulkhead1"].MaxConcurrency.Should().Be(10);
        statuses["bulkhead2"].MaxConcurrency.Should().Be(20);
    }

    [Test]
    public void Dispose_ShouldDisposeAllBulkheads()
    {
        // Arrange
        var factory = new BulkheadFactory(_loggerFactoryMock.Object);
        var bulkhead = factory.GetOrCreate("test");

        // Act
        factory.Dispose();

        // Assert - 后续操作应该失败
        var act = () => factory.GetOrCreate("new");
        act.Should().Throw<ObjectDisposedException>();
    }
}

[TestFixture]
public class BulkheadStatusTests
{
    [Test]
    public void ConcurrencyUtilization_ShouldCalculateCorrectly()
    {
        // Arrange
        var status = new BulkheadStatus
        {
            Name = "test",
            MaxConcurrency = 100,
            CurrentConcurrency = 75,
            MaxQueueLength = 50,
            CurrentQueueLength = 25
        };

        // Assert
        status.ConcurrencyUtilization.Should().Be(0.75);
        status.QueueUtilization.Should().Be(0.5);
    }

    [Test]
    public void ConcurrencyUtilization_WithZeroMax_ShouldReturnZero()
    {
        // Arrange
        var status = new BulkheadStatus
        {
            Name = "test",
            MaxConcurrency = 0,
            CurrentConcurrency = 0,
            MaxQueueLength = 0,
            CurrentQueueLength = 0
        };

        // Assert
        status.ConcurrencyUtilization.Should().Be(0);
        status.QueueUtilization.Should().Be(0);
    }
}
