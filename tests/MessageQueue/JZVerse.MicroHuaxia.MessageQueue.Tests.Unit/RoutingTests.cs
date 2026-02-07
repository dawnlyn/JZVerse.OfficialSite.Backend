using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Routing;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Core;
using JZVerse.MicroHuaxia.MessageQueue.Core.Routing;
using JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.MessageQueue.Tests.Unit;

[TestFixture]
public class RoutingTests
{
    private ServiceProvider _serviceProvider = null!;
    private IExchangeManager _exchangeManager = null!;
    private IQueueManager _queueManager = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddMemoryMessageStorage();
        services.AddMessageQueueCore();

        _serviceProvider = services.BuildServiceProvider();
        _exchangeManager = _serviceProvider.GetRequiredService<IExchangeManager>();
        _queueManager = _serviceProvider.GetRequiredService<IQueueManager>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task DeclareExchange_ShouldCreateExchange()
    {
        // Act
        var exchange = await _exchangeManager.DeclareExchangeAsync(
            "test-exchange",
            ExchangeType.Direct,
            durable: true);

        // Assert
        Assert.That(exchange.Name, Is.EqualTo("test-exchange"));
        Assert.That(exchange.Type, Is.EqualTo(ExchangeType.Direct));
        Assert.That(exchange.Durable, Is.True);
    }

    [Test]
    public async Task DeclareExchange_Topic_ShouldWork()
    {
        // Act
        var exchange = await _exchangeManager.DeclareExchangeAsync(
            "topic-exchange",
            ExchangeType.Topic);

        // Assert
        Assert.That(exchange.Type, Is.EqualTo(ExchangeType.Topic));
    }

    [Test]
    public async Task GetExchange_ShouldReturnExchange()
    {
        // Arrange
        await _exchangeManager.DeclareExchangeAsync("get-test", ExchangeType.Fanout);

        // Act
        var exchange = await _exchangeManager.GetExchangeAsync("get-test");

        // Assert
        Assert.That(exchange, Is.Not.Null);
        Assert.That(exchange!.Name, Is.EqualTo("get-test"));
    }

    [Test]
    public async Task GetExchange_NotFound_ShouldReturnNull()
    {
        // Act
        var exchange = await _exchangeManager.GetExchangeAsync("non-existent");

        // Assert
        Assert.That(exchange, Is.Null);
    }

    [Test]
    public async Task DefaultExchanges_ShouldExist()
    {
        // Act
        var defaultExchange = await _exchangeManager.GetExchangeAsync("");
        var fanoutExchange = await _exchangeManager.GetExchangeAsync("amq.fanout");
        var topicExchange = await _exchangeManager.GetExchangeAsync("amq.topic");
        var headersExchange = await _exchangeManager.GetExchangeAsync("amq.headers");

        // Assert
        Assert.That(defaultExchange, Is.Not.Null);
        Assert.That(fanoutExchange, Is.Not.Null);
        Assert.That(topicExchange, Is.Not.Null);
        Assert.That(headersExchange, Is.Not.Null);
    }

    [Test]
    public async Task DeclareQueue_ShouldCreateQueue()
    {
        // Act
        var queue = await _queueManager.DeclareQueueAsync(
            "test-queue",
            durable: true,
            exclusive: false,
            autoDelete: false);

        // Assert
        Assert.That(queue.Name, Is.EqualTo("test-queue"));
        Assert.That(queue.Durable, Is.True);
        Assert.That(queue.Exclusive, Is.False);
        Assert.That(queue.AutoDelete, Is.False);
    }

    [Test]
    public async Task GetQueue_ShouldReturnQueue()
    {
        // Arrange
        await _queueManager.DeclareQueueAsync("get-queue-test");

        // Act
        var queue = await _queueManager.GetQueueAsync("get-queue-test");

        // Assert
        Assert.That(queue, Is.Not.Null);
        Assert.That(queue!.Name, Is.EqualTo("get-queue-test"));
    }

    [Test]
    public async Task DeleteQueue_ShouldRemoveQueue()
    {
        // Arrange
        await _queueManager.DeclareQueueAsync("delete-queue-test");

        // Act
        await _queueManager.DeleteQueueAsync("delete-queue-test");
        var queue = await _queueManager.GetQueueAsync("delete-queue-test");

        // Assert
        Assert.That(queue, Is.Null);
    }

    [Test]
    public async Task BindQueue_ShouldCreateBinding()
    {
        // Arrange
        var exchange = await _exchangeManager.DeclareExchangeAsync("bind-exchange", ExchangeType.Direct);
        await _queueManager.DeclareQueueAsync("bind-queue");

        // Act
        await exchange.BindQueueAsync("bind-queue", "routing-key");
        var bindings = await exchange.GetBindingsAsync();

        // Assert
        Assert.That(bindings.Count, Is.EqualTo(1));
        Assert.That(bindings[0].QueueName, Is.EqualTo("bind-queue"));
        Assert.That(bindings[0].RoutingKey, Is.EqualTo("routing-key"));
    }

    [Test]
    public async Task UnbindQueue_ShouldRemoveBinding()
    {
        // Arrange
        var exchange = await _exchangeManager.DeclareExchangeAsync("unbind-exchange", ExchangeType.Direct);
        await exchange.BindQueueAsync("unbind-queue", "routing-key");

        // Act
        await exchange.UnbindQueueAsync("unbind-queue", "routing-key");
        var bindings = await exchange.GetBindingsAsync();

        // Assert
        Assert.That(bindings.Count, Is.EqualTo(0));
    }
}
