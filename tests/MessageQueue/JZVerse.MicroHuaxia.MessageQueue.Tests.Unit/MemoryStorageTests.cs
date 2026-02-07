using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.MessageQueue.Tests.Unit;

[TestFixture]
public class MemoryStorageTests
{
    private ServiceProvider _serviceProvider = null!;
    private IMessageStore _messageStore = null!;
    private IOffsetManager _offsetManager = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddMemoryMessageStorage(options =>
        {
            options.MaxMessagesPerPartition = 1000;
        });

        _serviceProvider = services.BuildServiceProvider();
        _messageStore = _serviceProvider.GetRequiredService<IMessageStore>();
        _offsetManager = _serviceProvider.GetRequiredService<IOffsetManager>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task AppendMessage_ShouldReturnOffset()
    {
        // Arrange
        var message = MessageBuilder.Create()
            .Topic("storage-test")
            .Body("Test message")
            .Build();

        // Act
        var offset = await _messageStore.AppendAsync(message, 0);

        // Assert
        Assert.That(offset, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task GetByOffset_ShouldReturnMessages()
    {
        // Arrange
        var topic = "offset-test";
        for (int i = 0; i < 10; i++)
        {
            var message = MessageBuilder.Create()
                .Topic(topic)
                .Body($"Message {i}")
                .Build();
            await _messageStore.AppendAsync(message, 0);
        }

        // Act
        var messages = await _messageStore.GetByOffsetAsync(topic, 0, 0, 5);

        // Assert
        Assert.That(messages.Count, Is.EqualTo(5));
    }

    [Test]
    public async Task GetById_ShouldReturnMessage()
    {
        // Arrange
        var message = MessageBuilder.Create()
            .Topic("id-test")
            .Body("Test message")
            .Build();
        await _messageStore.AppendAsync(message, 0);

        // Act
        var retrieved = await _messageStore.GetByIdAsync(message.MessageId);

        // Assert
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.MessageId, Is.EqualTo(message.MessageId));
    }

    [Test]
    public async Task GetById_NotFound_ShouldReturnNull()
    {
        // Act
        var message = await _messageStore.GetByIdAsync("non-existent-id");

        // Assert
        Assert.That(message, Is.Null);
    }

    [Test]
    public async Task Delete_ShouldMarkAsDeleted()
    {
        // Arrange
        var message = MessageBuilder.Create()
            .Topic("delete-test")
            .Body("Test message")
            .Build();
        await _messageStore.AppendAsync(message, 0);

        // Act
        var deleted = await _messageStore.DeleteAsync(message.MessageId);

        // Assert
        Assert.That(deleted, Is.True);
        
        // 删除后应该无法通过 ID 获取（逻辑删除）
        var retrieved = await _messageStore.GetByIdAsync(message.MessageId);
        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public async Task GetLatestOffset_ShouldReturnCorrectValue()
    {
        // Arrange
        var topic = "latest-offset-test";
        for (int i = 0; i < 5; i++)
        {
            var message = MessageBuilder.Create()
                .Topic(topic)
                .Body($"Message {i}")
                .Build();
            await _messageStore.AppendAsync(message, 0);
        }

        // Act
        var latestOffset = await _messageStore.GetLatestOffsetAsync(topic, 0);

        // Assert
        Assert.That(latestOffset, Is.EqualTo(5)); // 0-4 存储了5条消息，下一个偏移量是5
    }

    [Test]
    public async Task OffsetManager_CommitAndGet_ShouldWork()
    {
        // Arrange
        var consumerGroup = "test-group";
        var topic = "offset-manager-test";

        // Act
        await _offsetManager.CommitOffsetAsync(consumerGroup, topic, 0, 100);
        var offset = await _offsetManager.GetOffsetAsync(consumerGroup, topic, 0);

        // Assert
        Assert.That(offset, Is.EqualTo(100));
    }

    [Test]
    public async Task OffsetManager_GetLag_ShouldCalculateCorrectly()
    {
        // Arrange
        var consumerGroup = "lag-test-group";
        var topic = "lag-test";

        // 存储 10 条消息
        for (int i = 0; i < 10; i++)
        {
            var message = MessageBuilder.Create()
                .Topic(topic)
                .Body($"Message {i}")
                .Build();
            await _messageStore.AppendAsync(message, 0);
        }

        // 提交偏移量到 5
        await _offsetManager.CommitOffsetAsync(consumerGroup, topic, 0, 5);

        // Act
        var lag = await _offsetManager.GetLagAsync(consumerGroup, topic, 0);

        // Assert
        Assert.That(lag, Is.EqualTo(4)); // 10 - 5 - 1 = 4 (还有4条未消费)
    }
}
