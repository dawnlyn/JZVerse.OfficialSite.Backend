using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Server;
using JZVerse.MicroHuaxia.MessageQueue.Server.Management;
using JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.MessageQueue.Tests.Unit;

/// <summary>
/// Broker 管理接口测试
/// </summary>
[TestFixture]
public class BrokerManagementTests
{
    private ServiceProvider _serviceProvider = null!;
    private IBrokerManagement _management = null!;
    private IMessageStore _messageStore = null!;
    private BrokerServer _brokerServer = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddMessageQueueBroker(opt =>
        {
            opt.StorageType = StorageType.Memory;
            opt.DefaultPartitions = 4;
        });

        _serviceProvider = services.BuildServiceProvider();
        _management = _serviceProvider.GetRequiredService<IBrokerManagement>();
        _messageStore = _serviceProvider.GetRequiredService<IMessageStore>();
        _brokerServer = _serviceProvider.GetRequiredService<BrokerServer>();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _serviceProvider.DisposeAsync();
    }

    [Test]
    public async Task GetStatsAsync_NoData_ShouldReturnZeroStats()
    {
        // Act
        var stats = await _management.GetStatsAsync();

        // Assert
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats.TotalTopics, Is.EqualTo(0));
        Assert.That(stats.TotalMessages, Is.EqualTo(0));
        Assert.That(stats.ConnectedClients, Is.EqualTo(0));
        Assert.That(stats.PendingMessages, Is.EqualTo(0));
    }

    [Test]
    public async Task GetTopicsAsync_NoData_ShouldReturnEmptyList()
    {
        // Act
        var topics = await _management.GetTopicsAsync();

        // Assert
        Assert.That(topics, Is.Not.Null);
        Assert.That(topics, Is.Empty);
    }

    [Test]
    public async Task GetTopicsAsync_WithMessagesAndSubscriptions_ShouldReturnTopics()
    {
        // Arrange - store messages
        var message = MessageBuilder.Create()
            .Topic("test-topic")
            .Body("Hello")
            .Build();
        await _messageStore.AppendAsync(message, 0);

        // Add topic subscription via reflection
        AddTopicSubscription("test-topic", "session-1");
        AddClientSession("session-1", "client-1", null);

        // Act
        var topics = await _management.GetTopicsAsync();

        // Assert
        Assert.That(topics, Has.Count.EqualTo(1));
        Assert.That(topics[0].Name, Is.EqualTo("test-topic"));
        Assert.That(topics[0].TotalMessages, Is.GreaterThanOrEqualTo(1));
        Assert.That(topics[0].SubscriberCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetTopicDetailAsync_NonExistentTopic_ShouldReturnNull()
    {
        // Act
        var detail = await _management.GetTopicDetailAsync("non-existent-topic");

        // Assert
        Assert.That(detail, Is.Null);
    }

    [Test]
    public async Task GetTopicDetailAsync_WithMessages_ShouldReturnDetail()
    {
        // Arrange
        var message = MessageBuilder.Create()
            .Topic("detail-topic")
            .Body("Test message")
            .Build();
        await _messageStore.AppendAsync(message, 0);

        AddTopicSubscription("detail-topic", "session-2");
        AddClientSession("session-2", "client-2", null);

        // Act
        var detail = await _management.GetTopicDetailAsync("detail-topic");

        // Assert
        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.Name, Is.EqualTo("detail-topic"));
        Assert.That(detail.Partitions, Is.Not.Empty);
        Assert.That(detail.Subscribers, Has.Count.EqualTo(1));
        Assert.That(detail.Subscribers[0], Is.EqualTo("client-2"));
    }

    [Test]
    public async Task GetTopicDetailAsync_Partitions_ShouldHaveCorrectOffsets()
    {
        // Arrange - store multiple messages
        for (int i = 0; i < 5; i++)
        {
            var msg = MessageBuilder.Create()
                .Topic("offset-topic")
                .Body($"Message {i}")
                .Build();
            await _messageStore.AppendAsync(msg, 0);
        }

        AddTopicSubscription("offset-topic", "session-x");
        AddClientSession("session-x", "client-x", null);

        // Act
        var detail = await _management.GetTopicDetailAsync("offset-topic");

        // Assert
        Assert.That(detail, Is.Not.Null);
        var partition0 = detail!.Partitions.First(p => p.PartitionId == 0);
        Assert.That(partition0.MessageCount, Is.EqualTo(5));
        Assert.That(partition0.LatestOffset, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetConsumerGroupsAsync_NoClients_ShouldReturnEmptyList()
    {
        // Act
        var groups = await _management.GetConsumerGroupsAsync();

        // Assert
        Assert.That(groups, Is.Not.Null);
        Assert.That(groups, Is.Empty);
    }

    [Test]
    public async Task GetConsumerGroupsAsync_WithClients_ShouldReturnGroups()
    {
        // Arrange
        AddClientSession("session-a", "client-a", "group-1", "topic-1", "topic-2");
        AddClientSession("session-b", "client-b", "group-1", "topic-1");
        AddClientSession("session-c", "client-c", "group-2", "topic-3");

        // Act
        var groups = await _management.GetConsumerGroupsAsync();

        // Assert
        Assert.That(groups, Has.Count.EqualTo(2));

        var group1 = groups.First(g => g.GroupName == "group-1");
        Assert.That(group1.SubscribedTopics, Does.Contain("topic-1"));
        Assert.That(group1.SubscribedTopics, Does.Contain("topic-2"));

        var group2 = groups.First(g => g.GroupName == "group-2");
        Assert.That(group2.SubscribedTopics, Has.Count.EqualTo(1));
        Assert.That(group2.SubscribedTopics, Does.Contain("topic-3"));
    }

    [Test]
    public async Task GetConsumerGroupsAsync_ClientWithNoGroup_ShouldBeExcluded()
    {
        // Arrange
        AddClientSession("session-no-group", "client-no-group", null);
        AddClientSession("session-with-group", "client-with-group", "my-group");

        // Act
        var groups = await _management.GetConsumerGroupsAsync();

        // Assert
        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].GroupName, Is.EqualTo("my-group"));
    }

    [Test]
    public async Task GetConnectionsAsync_NoClients_ShouldReturnEmptyList()
    {
        // Act
        var connections = await _management.GetConnectionsAsync();

        // Assert
        Assert.That(connections, Is.Not.Null);
        Assert.That(connections, Is.Empty);
    }

    [Test]
    public async Task GetConnectionsAsync_WithClients_ShouldReturnConnections()
    {
        // Arrange
        AddClientSession("session-1", "client-1", "group-a", "topic-x");
        AddClientSession("session-2", "client-2", null, "topic-y", "topic-z");

        // Act
        var connections = await _management.GetConnectionsAsync();

        // Assert
        Assert.That(connections, Has.Count.EqualTo(2));

        var conn1 = connections.First(c => c.ClientId == "client-1");
        Assert.That(conn1.SessionId, Is.EqualTo("session-1"));
        Assert.That(conn1.Topics, Does.Contain("topic-x"));
        Assert.That(conn1.ConnectedAt, Is.Not.EqualTo(default(DateTimeOffset)));

        var conn2 = connections.First(c => c.ClientId == "client-2");
        Assert.That(conn2.Topics, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetStatsAsync_WithData_ShouldReturnCorrectCounts()
    {
        // Arrange - store messages for 2 topics
        for (int i = 0; i < 3; i++)
        {
            var msg = MessageBuilder.Create()
                .Topic("stats-topic-1")
                .Body($"Msg {i}")
                .Build();
            await _messageStore.AppendAsync(msg, 0);
        }

        for (int i = 0; i < 7; i++)
        {
            var msg = MessageBuilder.Create()
                .Topic("stats-topic-2")
                .Body($"Msg {i}")
                .Build();
            await _messageStore.AppendAsync(msg, 0);
        }

        // Add subscriptions so topics are discovered
        AddTopicSubscription("stats-topic-1", "session-s1");
        AddTopicSubscription("stats-topic-2", "session-s2");
        AddClientSession("session-s1", "client-s1", "group-stats");
        AddClientSession("session-s2", "client-s2", "group-stats");

        // Act
        var stats = await _management.GetStatsAsync();

        // Assert
        Assert.That(stats.TotalTopics, Is.EqualTo(2));
        Assert.That(stats.TotalMessages, Is.EqualTo(10));
        Assert.That(stats.ConnectedClients, Is.EqualTo(2));
    }

    [Test]
    public async Task GetTopicsAsync_MultipleTopics_ShouldReturnAll()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
        {
            var msg = MessageBuilder.Create()
                .Topic($"multi-topic-{i}")
                .Body("Test")
                .Build();
            await _messageStore.AppendAsync(msg, 0);
            AddTopicSubscription($"multi-topic-{i}", $"session-m{i}");
            AddClientSession($"session-m{i}", $"client-m{i}", null);
        }

        // Act
        var topics = await _management.GetTopicsAsync();

        // Assert
        Assert.That(topics, Has.Count.EqualTo(3));
        Assert.That(topics.Select(t => t.Name), Does.Contain("multi-topic-0"));
        Assert.That(topics.Select(t => t.Name), Does.Contain("multi-topic-1"));
        Assert.That(topics.Select(t => t.Name), Does.Contain("multi-topic-2"));
    }

    // Helper: add a client session to BrokerServer via reflection (ClientSession is internal)
    private void AddClientSession(string sessionId, string clientId, string? consumerGroup, params string[] subscribedTopics)
    {
        var field = typeof(BrokerServer).GetField("_clientSessions",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var dict = (IDictionary)field!.GetValue(_brokerServer)!;

        // Create ClientSession instance via reflection (internal type)
        var sessionType = typeof(BrokerServer).Assembly.GetType("JZVerse.MicroHuaxia.MessageQueue.Server.ClientSession")!;
        var session = Activator.CreateInstance(sessionType)!;

        // Set init-only properties via backing fields
        SetBackingField(session, "SessionId", sessionId);
        SetBackingField(session, "ClientId", clientId);
        SetBackingField(session, "ConnectedAt", DateTimeOffset.UtcNow);
        SetBackingField(session, "LastHeartbeat", DateTimeOffset.UtcNow);

        // Set ConsumerGroup (has public setter)
        sessionType.GetProperty("ConsumerGroup")!.SetValue(session, consumerGroup);

        // Add subscribed topics
        var topicsProperty = sessionType.GetProperty("SubscribedTopics")!;
        var topicsSet = (HashSet<string>)topicsProperty.GetValue(session)!;
        foreach (var topic in subscribedTopics)
        {
            topicsSet.Add(topic);
        }

        dict[sessionId] = session;
    }

    // Helper: add a topic subscription mapping
    private void AddTopicSubscription(string topic, string sessionId)
    {
        var field = typeof(BrokerServer).GetField("_topicSubscriptions",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var dict = (IDictionary)field!.GetValue(_brokerServer)!;

        if (!dict.Contains(topic))
        {
            dict[topic] = new HashSet<string>();
        }

        var sessions = (HashSet<string>)dict[topic]!;
        lock (sessions)
        {
            sessions.Add(sessionId);
        }
    }

    // Helper: set init-only property via its compiler-generated backing field
    private static void SetBackingField(object obj, string propertyName, object value)
    {
        var backingField = obj.GetType().GetField($"<{propertyName}>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (backingField != null)
        {
            backingField.SetValue(obj, value);
        }
        else
        {
            // Fallback to property setter
            obj.GetType().GetProperty(propertyName)!.SetValue(obj, value);
        }
    }
}
