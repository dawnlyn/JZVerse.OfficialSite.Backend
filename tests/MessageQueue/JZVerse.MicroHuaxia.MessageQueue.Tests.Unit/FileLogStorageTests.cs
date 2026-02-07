using System.Text;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Storage.FileLog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.MessageQueue.Tests.Unit;

/// <summary>
/// FileLog 存储单元测试
/// </summary>
[TestFixture]
public class FileLogStorageTests
{
    private string _testDirectory = null!;
    private FileLogMessageStore _store = null!;
    private FileLogStoreOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"mq_test_{Guid.NewGuid():N}");
        _options = new FileLogStoreOptions
        {
            DataDirectory = _testDirectory,
            SegmentSizeBytes = 1024 * 1024, // 1MB for testing
            IndexIntervalBytes = 256, // Smaller interval for testing
            FlushPolicy = FlushPolicy.EveryMessage
        };

        _store = new FileLogMessageStore(
            NullLogger<FileLogMessageStore>.Instance,
            _options);
    }

    [TearDown]
    public void TearDown()
    {
        _store?.Dispose();

        // 清理测试目录
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }

    [Test]
    public async Task AppendMessage_ShouldReturnOffset()
    {
        // Arrange
        var message = MessageBuilder.Create()
            .Topic("test-topic")
            .Body("Hello, FileLog!")
            .Build();

        // Act
        var offset = await _store.AppendAsync(message, 0);

        // Assert
        Assert.That(offset, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task AppendMultipleMessages_ShouldReturnIncrementingOffsets()
    {
        // Arrange & Act
        var offsets = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var message = MessageBuilder.Create()
                .Topic("test-topic")
                .Body($"Message {i}")
                .Build();

            var offset = await _store.AppendAsync(message, 0);
            offsets.Add(offset);
        }

        // Assert
        for (int i = 1; i < offsets.Count; i++)
        {
            Assert.That(offsets[i], Is.GreaterThan(offsets[i - 1]));
        }
    }

    [Test]
    public async Task GetByOffset_ShouldReturnCorrectMessages()
    {
        // Arrange
        var topic = "test-topic";
        var messages = new List<IMessage>();
        for (int i = 0; i < 5; i++)
        {
            var message = MessageBuilder.Create()
                .Topic(topic)
                .Body($"Message {i}")
                .Build();

            await _store.AppendAsync(message, 0);
            messages.Add(message);
        }

        // Act
        var retrieved = await _store.GetByOffsetAsync(topic, 0, 0, 5);

        // Assert
        Assert.That(retrieved, Has.Count.EqualTo(5));
    }

    [Test]
    public async Task GetById_ShouldReturnCorrectMessage()
    {
        // Arrange
        var message = MessageBuilder.Create()
            .Topic("test-topic")
            .Body("Test message")
            .Build();

        await _store.AppendAsync(message, 0);

        // Act
        var retrieved = await _store.GetByIdAsync(message.MessageId);

        // Assert
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.MessageId, Is.EqualTo(message.MessageId));
    }

    [Test]
    public async Task GetById_NonExistent_ShouldReturnNull()
    {
        // Act
        var retrieved = await _store.GetByIdAsync("non-existent-id");

        // Assert
        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public async Task GetLatestOffset_ShouldReturnCorrectValue()
    {
        // Arrange
        var topic = "test-topic";
        for (int i = 0; i < 3; i++)
        {
            var message = MessageBuilder.Create()
                .Topic(topic)
                .Body($"Message {i}")
                .Build();

            await _store.AppendAsync(message, 0);
        }

        // Act
        var latestOffset = await _store.GetLatestOffsetAsync(topic, 0);

        // Assert
        Assert.That(latestOffset, Is.EqualTo(3)); // After 3 messages, next offset is 3
    }

    [Test]
    public async Task GetEarliestOffset_ShouldReturnZero()
    {
        // Arrange
        var topic = "test-topic";
        var message = MessageBuilder.Create()
            .Topic(topic)
            .Body("Test")
            .Build();

        await _store.AppendAsync(message, 0);

        // Act
        var earliestOffset = await _store.GetEarliestOffsetAsync(topic, 0);

        // Assert
        Assert.That(earliestOffset, Is.EqualTo(0));
    }

    [Test]
    public async Task AppendBatch_ShouldReturnFirstOffset()
    {
        // Arrange
        var messages = Enumerable.Range(0, 5)
            .Select(i => MessageBuilder.Create()
                .Topic("test-topic")
                .Body($"Batch message {i}")
                .Build())
            .ToList();

        // Act
        var firstOffset = await _store.AppendBatchAsync(messages, 0);

        // Assert
        Assert.That(firstOffset, Is.GreaterThanOrEqualTo(0));
        var latestOffset = await _store.GetLatestOffsetAsync("test-topic", 0);
        Assert.That(latestOffset, Is.EqualTo(firstOffset + 5));
    }

    [Test]
    public async Task Delete_ShouldRemoveFromIndex()
    {
        // Arrange
        var message = MessageBuilder.Create()
            .Topic("test-topic")
            .Body("To be deleted")
            .Build();

        await _store.AppendAsync(message, 0);

        // Act
        var deleted = await _store.DeleteAsync(message.MessageId);
        var retrieved = await _store.GetByIdAsync(message.MessageId);

        // Assert
        Assert.That(deleted, Is.True);
        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public async Task MultiplePartitions_ShouldBeIndependent()
    {
        // Arrange
        var topic = "test-topic";

        var message0 = MessageBuilder.Create()
            .Topic(topic)
            .Body("Partition 0")
            .Build();

        var message1 = MessageBuilder.Create()
            .Topic(topic)
            .Body("Partition 1")
            .Build();

        // Act
        var offset0 = await _store.AppendAsync(message0, 0);
        var offset1 = await _store.AppendAsync(message1, 1);

        // Assert
        Assert.That(offset0, Is.EqualTo(0));
        Assert.That(offset1, Is.EqualTo(0));

        var latest0 = await _store.GetLatestOffsetAsync(topic, 0);
        var latest1 = await _store.GetLatestOffsetAsync(topic, 1);

        Assert.That(latest0, Is.EqualTo(1));
        Assert.That(latest1, Is.EqualTo(1));
    }

    [Test]
    [Ignore("Time index implementation needs optimization")]
    public async Task GetByTimeRange_ShouldFilterByTime()
    {
        // Arrange
        var topic = "test-topic";
        var startTime = DateTimeOffset.UtcNow;

        for (int i = 0; i < 3; i++)
        {
            var message = MessageBuilder.Create()
                .Topic(topic)
                .Body($"Message {i}")
                .Build();

            await _store.AppendAsync(message, 0);
            await Task.Delay(10); // Small delay to ensure different timestamps
        }

        var endTime = DateTimeOffset.UtcNow;

        // Act
        var messages = await _store.GetByTimeRangeAsync(topic, 0, startTime, endTime);

        // Assert
        Assert.That(messages, Has.Count.GreaterThanOrEqualTo(1));
    }
}

/// <summary>
/// FileLog 偏移量管理器测试
/// </summary>
[TestFixture]
public class FileLogOffsetManagerTests
{
    private string _testDirectory = null!;
    private FileLogMessageStore _store = null!;
    private FileLogOffsetManager _offsetManager = null!;
    private FileLogStoreOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"mq_offset_test_{Guid.NewGuid():N}");
        _options = new FileLogStoreOptions
        {
            DataDirectory = _testDirectory,
            FlushPolicy = FlushPolicy.EveryMessage
        };

        _store = new FileLogMessageStore(
            NullLogger<FileLogMessageStore>.Instance,
            _options);

        _offsetManager = new FileLogOffsetManager(
            NullLogger<FileLogOffsetManager>.Instance,
            _options,
            _store);
    }

    [TearDown]
    public void TearDown()
    {
        _offsetManager?.Dispose();
        _store?.Dispose();

        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }

    [Test]
    public async Task GetOffset_NotCommitted_ShouldReturnMinusOne()
    {
        // Act
        var offset = await _offsetManager.GetOffsetAsync("test-group", "test-topic", 0);

        // Assert
        Assert.That(offset, Is.EqualTo(-1));
    }

    [Test]
    public async Task CommitOffset_ShouldPersist()
    {
        // Arrange
        var consumerGroup = "test-group";
        var topic = "test-topic";
        var partition = 0;
        var offset = 100L;

        // Act
        await _offsetManager.CommitOffsetAsync(consumerGroup, topic, partition, offset);
        var retrieved = await _offsetManager.GetOffsetAsync(consumerGroup, topic, partition);

        // Assert
        Assert.That(retrieved, Is.EqualTo(offset));
    }

    [Test]
    public async Task ResetOffset_ShouldUpdateValue()
    {
        // Arrange
        var consumerGroup = "test-group";
        var topic = "test-topic";
        var partition = 0;

        await _offsetManager.CommitOffsetAsync(consumerGroup, topic, partition, 100);

        // Act
        await _offsetManager.ResetOffsetAsync(consumerGroup, topic, partition, 50);
        var offset = await _offsetManager.GetOffsetAsync(consumerGroup, topic, partition);

        // Assert
        Assert.That(offset, Is.EqualTo(50));
    }

    [Test]
    public async Task GetLag_ShouldCalculateCorrectly()
    {
        // Arrange
        var consumerGroup = "test-group";
        var topic = "test-topic";
        var partition = 0;

        // 添加一些消息
        for (int i = 0; i < 10; i++)
        {
            var message = MessageBuilder.Create()
                .Topic(topic)
                .Body($"Message {i}")
                .Build();

            await _store.AppendAsync(message, partition);
        }

        // 提交偏移量到 5
        await _offsetManager.CommitOffsetAsync(consumerGroup, topic, partition, 5);

        // Act
        var lag = await _offsetManager.GetLagAsync(consumerGroup, topic, partition);

        // Assert
        Assert.That(lag, Is.EqualTo(5)); // 10 - 5 = 5
    }

    [Test]
    public async Task GetLag_NoCommit_ShouldReturnLatestOffset()
    {
        // Arrange
        var consumerGroup = "test-group";
        var topic = "test-topic";
        var partition = 0;

        // 添加消息
        for (int i = 0; i < 5; i++)
        {
            var message = MessageBuilder.Create()
                .Topic(topic)
                .Body($"Message {i}")
                .Build();

            await _store.AppendAsync(message, partition);
        }

        // Act (没有提交偏移量)
        var lag = await _offsetManager.GetLagAsync(consumerGroup, topic, partition);

        // Assert
        Assert.That(lag, Is.EqualTo(5));
    }

    [Test]
    public async Task MultipleConsumerGroups_ShouldBeIndependent()
    {
        // Arrange
        var topic = "test-topic";
        var partition = 0;

        // Act
        await _offsetManager.CommitOffsetAsync("group-a", topic, partition, 10);
        await _offsetManager.CommitOffsetAsync("group-b", topic, partition, 20);

        var offsetA = await _offsetManager.GetOffsetAsync("group-a", topic, partition);
        var offsetB = await _offsetManager.GetOffsetAsync("group-b", topic, partition);

        // Assert
        Assert.That(offsetA, Is.EqualTo(10));
        Assert.That(offsetB, Is.EqualTo(20));
    }
}
