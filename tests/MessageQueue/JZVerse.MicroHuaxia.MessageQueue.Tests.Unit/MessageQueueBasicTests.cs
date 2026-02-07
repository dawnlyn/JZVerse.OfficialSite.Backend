using System.Text;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Core;
using JZVerse.MicroHuaxia.MessageQueue.Protocol.InProc;
using JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.MessageQueue.Tests.Unit;

[TestFixture]
public class MessageQueueBasicTests
{
    private ServiceProvider _serviceProvider = null!;
    private IMessageProducer _producer = null!;
    private IMessageConsumer _consumer = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddMemoryMessageStorage();
        services.AddMessageQueueCore();
        services.AddInProcMessageProtocol();

        _serviceProvider = services.BuildServiceProvider();
        _producer = _serviceProvider.GetRequiredService<IMessageProducer>();
        _consumer = _serviceProvider.GetRequiredService<IMessageConsumer>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task SendMessage_ShouldSucceed()
    {
        // Arrange
        var message = MessageBuilder.Create()
            .Topic("test-topic")
            .Body("Hello, World!")
            .Tag("test")
            .Build();

        // Act
        var result = await _producer.SendAsync(message);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.MessageId, Is.EqualTo(message.MessageId));
    }

    [Test]
    public async Task SendBatchMessages_ShouldSucceed()
    {
        // Arrange
        var messages = Enumerable.Range(0, 10)
            .Select(i => MessageBuilder.Create()
                .Topic("batch-topic")
                .Body($"Message {i}")
                .Build())
            .ToList();

        // Act
        var results = await _producer.SendBatchAsync(messages);

        // Assert
        Assert.That(results.Count, Is.EqualTo(10));
        Assert.That(results.All(r => r.Success), Is.True);
    }

    [Test]
    public async Task Subscribe_ShouldReceiveMessages()
    {
        // Arrange
        var topic = "subscribe-test";
        var receivedMessages = new List<IMessage>();
        var tcs = new TaskCompletionSource<bool>();

        await _consumer.SubscribeAsync(topic, async (envelope, ct) =>
        {
            receivedMessages.Add(envelope.Message);
            if (receivedMessages.Count >= 3)
            {
                tcs.TrySetResult(true);
            }
            return ConsumeResult.Success;
        }, new ConsumeOptions { ConsumerGroup = "test-group" });

        // Act
        for (int i = 0; i < 3; i++)
        {
            var message = MessageBuilder.Create()
                .Topic(topic)
                .Body($"Test message {i}")
                .Build();
            await _producer.SendAsync(message);
        }

        // Wait for messages to be received
        var received = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;

        // Assert
        Assert.That(received, Is.True, "Messages should be received within timeout");
        Assert.That(receivedMessages.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task PullMessages_ShouldReturnMessages()
    {
        // Arrange
        var topic = "pull-test";
        var messageStore = _serviceProvider.GetRequiredService<IMessageStore>();

        // 直接存储消息
        for (int i = 0; i < 5; i++)
        {
            var message = MessageBuilder.Create()
                .Topic(topic)
                .Body($"Pull message {i}")
                .Build();
            await messageStore.AppendAsync(message, 0);
        }

        // Act
        var messages = await _consumer.PullAsync(topic, 10);

        // Assert
        Assert.That(messages.Count, Is.EqualTo(5));
    }

    [Test]
    public void MessageBuilder_ShouldCreateMessage()
    {
        // Act
        var message = MessageBuilder.Create()
            .Topic("builder-test")
            .Tag("test-tag")
            .Body("Test body")
            .Header("key1", "value1")
            .PartitionKey("user-123")
            .Priority(8)
            .Delay(30)
            .Build();

        // Assert
        Assert.That(message.Topic, Is.EqualTo("builder-test"));
        Assert.That(message.Tag, Is.EqualTo("test-tag"));
        Assert.That(message.Body, Is.EqualTo(Encoding.UTF8.GetBytes("Test body")));
        Assert.That(message.Headers["key1"], Is.EqualTo("value1"));
        Assert.That(message.PartitionKey, Is.EqualTo("user-123"));
        Assert.That(message.Priority, Is.EqualTo(8));
        Assert.That(message.DelaySeconds, Is.EqualTo(30));
        Assert.That(message.MessageId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void MessageBuilder_WithoutTopic_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            MessageBuilder.Create()
                .Body("Test")
                .Build();
        });
    }

    [Test]
    public void MessageBuilder_WithoutBody_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            MessageBuilder.Create()
                .Topic("test")
                .Build();
        });
    }
}
