using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Delay;
using JZVerse.MicroHuaxia.MessageQueue.Core.Delay;
using JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.MessageQueue.Tests.Unit;

/// <summary>
/// 时间轮测试
/// </summary>
[TestFixture]
public class TimeWheelTests
{
    private HierarchicalTimeWheel _timeWheel = null!;

    [SetUp]
    public void Setup()
    {
        var options = new TimeWheelOptions
        {
            TickDurationMs = 100,
            WheelSize = 64,
            MaxDelaySeconds = 3600
        };
        _timeWheel = new HierarchicalTimeWheel(
            NullLogger<HierarchicalTimeWheel>.Instance, options);
    }

    [TearDown]
    public void TearDown()
    {
        _timeWheel.Dispose();
    }

    [Test]
    public void Add_ShortDelay_ShouldAddToWheel()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "test-body");
        var entry = new DelayedMessageEntry
        {
            Message = message,
            DeliveryTime = DateTimeOffset.UtcNow.AddMilliseconds(500)
        };

        // Act
        var result = _timeWheel.Add(entry);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(_timeWheel.GetPendingCount(), Is.EqualTo(1));
    }

    [Test]
    public void Add_ExpiredMessage_ShouldReturnFalse()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "test-body");
        var entry = new DelayedMessageEntry
        {
            Message = message,
            DeliveryTime = DateTimeOffset.UtcNow.AddMilliseconds(-100) // 已过期
        };

        // Act
        var result = _timeWheel.Add(entry);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void AdvanceClock_ShouldReturnExpiredEntries()
    {
        // Arrange - 使用足够长的延迟确保消息进入时间轮
        var message = CreateTestMessage("test-topic", "test-body");
        var now = DateTimeOffset.UtcNow;
        var deliveryTime = now.AddSeconds(1); // 1秒后
        var entry = new DelayedMessageEntry
        {
            Message = message,
            DeliveryTime = deliveryTime
        };
        var added = _timeWheel.Add(entry);
        Assert.That(added, Is.True, "Message should be added to time wheel");
        Assert.That(_timeWheel.GetPendingCount(), Is.EqualTo(1), "Should have 1 pending message");

        // Act - 推进时间超过投递时间
        var futureTime = now.AddSeconds(2).ToUnixTimeMilliseconds();
        var expired = _timeWheel.AdvanceClock(futureTime);

        // Assert - 消息应该已到期
        Assert.That(expired, Has.Count.GreaterThanOrEqualTo(1), "Should have at least 1 expired entry");
        Assert.That(expired.Any(e => e.Message.MessageId == message.MessageId), Is.True, "Expired should contain our message");
    }

    [Test]
    public void Cancel_ShouldRemoveEntry()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "test-body");
        var entry = new DelayedMessageEntry
        {
            Message = message,
            DeliveryTime = DateTimeOffset.UtcNow.AddSeconds(10)
        };
        _timeWheel.Add(entry);

        // Act
        var result = _timeWheel.Cancel(message.MessageId);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void GetPendingCount_ShouldReturnCorrectCount()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            var message = CreateTestMessage($"topic-{i}", $"body-{i}");
            var entry = new DelayedMessageEntry
            {
                Message = message,
                DeliveryTime = DateTimeOffset.UtcNow.AddSeconds(i + 1)
            };
            _timeWheel.Add(entry);
        }

        // Act
        var count = _timeWheel.GetPendingCount();

        // Assert
        Assert.That(count, Is.EqualTo(5));
    }

    private static Abstractions.Models.IMessage CreateTestMessage(string topic, string body)
    {
        return MessageBuilder.Create()
            .Topic(topic)
            .Body(body)
            .Build();
    }
}

/// <summary>
/// 延迟消息存储测试
/// </summary>
[TestFixture]
public class DelayMessageStoreTests
{
    private MemoryDelayMessageStore _store = null!;

    [SetUp]
    public void Setup()
    {
        var options = new TimeWheelOptions
        {
            TickDurationMs = 50,
            WheelSize = 64,
            MaxDelaySeconds = 3600
        };
        _store = new MemoryDelayMessageStore(
            NullLogger<MemoryDelayMessageStore>.Instance,
            NullLogger<HierarchicalTimeWheel>.Instance,
            options);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public async Task AddAsync_ShouldStoreMessage()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "test-body");
        var deliveryTime = DateTimeOffset.UtcNow.AddSeconds(5);

        // Act
        await _store.AddAsync(message, deliveryTime);

        // Assert
        var count = await _store.GetPendingCountAsync();
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task GetDueMessagesAsync_ShouldReturnExpiredMessages()
    {
        // Arrange - 使用立即到期的消息测试立即队列
        var message = CreateTestMessage("test-topic", "due-message");
        // 使用非常短的延迟让消息进入立即队列
        var deliveryTime = DateTimeOffset.UtcNow.AddMilliseconds(10);
        await _store.AddAsync(message, deliveryTime);

        // Wait for message to become due
        await Task.Delay(100);

        // Act
        var dueMessages = await _store.GetDueMessagesAsync();

        // Assert - 消息应该在立即队列中被取出
        Assert.That(dueMessages, Has.Count.GreaterThanOrEqualTo(1), "Should have at least 1 due message");
        Assert.That(dueMessages.Any(m => m.MessageId == message.MessageId), Is.True, "Should contain our message");
    }

    [Test]
    public async Task RemoveAsync_ShouldCancelMessage()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "cancel-message");
        var deliveryTime = DateTimeOffset.UtcNow.AddSeconds(60);
        await _store.AddAsync(message, deliveryTime);

        // Act
        var result = await _store.RemoveAsync(message.MessageId);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task MultipleMessages_ShouldBeScheduledCorrectly()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            var message = CreateTestMessage($"topic-{i}", $"body-{i}");
            var deliveryTime = DateTimeOffset.UtcNow.AddMilliseconds(100 * (i + 1));
            await _store.AddAsync(message, deliveryTime);
        }

        // Act
        var count = await _store.GetPendingCountAsync();

        // Assert
        Assert.That(count, Is.EqualTo(10));
    }

    private static Abstractions.Models.IMessage CreateTestMessage(string topic, string body)
    {
        return MessageBuilder.Create()
            .Topic(topic)
            .Body(body)
            .Build();
    }
}

/// <summary>
/// 延迟级别测试
/// </summary>
[TestFixture]
public class DelayLevelTests
{
    [Test]
    [TestCase(DelayLevel.Second1, 1)]
    [TestCase(DelayLevel.Second5, 5)]
    [TestCase(DelayLevel.Second10, 10)]
    [TestCase(DelayLevel.Second30, 30)]
    [TestCase(DelayLevel.Minute1, 60)]
    [TestCase(DelayLevel.Minute5, 300)]
    [TestCase(DelayLevel.Hour1, 3600)]
    public void ToTimeSpan_ShouldReturnCorrectDuration(DelayLevel level, int expectedSeconds)
    {
        // Act
        var timeSpan = level.ToTimeSpan();

        // Assert
        Assert.That(timeSpan.TotalSeconds, Is.EqualTo(expectedSeconds));
    }

    [Test]
    [TestCase(0.5, DelayLevel.Second1)]
    [TestCase(3, DelayLevel.Second5)]
    [TestCase(8, DelayLevel.Second10)]
    [TestCase(45, DelayLevel.Minute1)]
    [TestCase(90, DelayLevel.Minute2)]
    [TestCase(7200, DelayLevel.Hour2)]
    public void FromTimeSpan_ShouldReturnClosestLevel(double seconds, DelayLevel expectedLevel)
    {
        // Act
        var level = DelayLevelExtensions.FromTimeSpan(TimeSpan.FromSeconds(seconds));

        // Assert
        Assert.That(level, Is.EqualTo(expectedLevel));
    }
}
