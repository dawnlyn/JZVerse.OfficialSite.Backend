using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Tracing;
using JZVerse.MicroHuaxia.MessageQueue.Core.Tracing;
using JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.MessageQueue.Tests.Unit;

/// <summary>
/// 消息追踪存储测试
/// </summary>
[TestFixture]
public class MessageTraceStoreTests
{
    private MemoryMessageTraceStore _store = null!;

    [SetUp]
    public void Setup()
    {
        var options = new MessageTraceStoreOptions
        {
            MaxEntries = 1000,
            Retention = TimeSpan.FromHours(1),
            CleanupInterval = TimeSpan.FromMinutes(30)
        };
        _store = new MemoryMessageTraceStore(
            NullLogger<MemoryMessageTraceStore>.Instance, options);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public async Task StoreAsync_ShouldStoreTracePoint()
    {
        // Arrange
        var tracePoint = CreateTracePoint("msg-001", TracePointType.Born, "test-topic");

        // Act
        await _store.StoreAsync(tracePoint);

        // Assert
        var results = await _store.GetByMessageIdAsync("msg-001");
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].MessageId, Is.EqualTo("msg-001"));
        Assert.That(results[0].Type, Is.EqualTo(TracePointType.Born));
    }

    [Test]
    public async Task GetByMessageIdAsync_ShouldReturnOrderedByTimestamp()
    {
        // Arrange
        var tracePoint1 = CreateTracePoint("msg-002", TracePointType.Born, "test-topic");
        await Task.Delay(10);
        var tracePoint2 = CreateTracePoint("msg-002", TracePointType.Store, "test-topic");
        await Task.Delay(10);
        var tracePoint3 = CreateTracePoint("msg-002", TracePointType.Dispatch, "test-topic");

        await _store.StoreAsync(tracePoint1);
        await _store.StoreAsync(tracePoint2);
        await _store.StoreAsync(tracePoint3);

        // Act
        var results = await _store.GetByMessageIdAsync("msg-002");

        // Assert
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results[0].Type, Is.EqualTo(TracePointType.Born));
        Assert.That(results[1].Type, Is.EqualTo(TracePointType.Store));
        Assert.That(results[2].Type, Is.EqualTo(TracePointType.Dispatch));
    }

    [Test]
    public async Task QueryAsync_ByTopic_ShouldReturnMatchingTracePoints()
    {
        // Arrange
        await _store.StoreAsync(CreateTracePoint("msg-003", TracePointType.Born, "topic-A"));
        await _store.StoreAsync(CreateTracePoint("msg-004", TracePointType.Born, "topic-B"));
        await _store.StoreAsync(CreateTracePoint("msg-005", TracePointType.Store, "topic-A"));

        var startTime = DateTimeOffset.UtcNow.AddMinutes(-1);
        var endTime = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        var results = await _store.QueryAsync("topic-A", null, startTime, endTime);

        // Assert
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.Topic == "topic-A"), Is.True);
    }

    [Test]
    public async Task QueryAsync_ByConsumerGroup_ShouldReturnMatchingTracePoints()
    {
        // Arrange
        await _store.StoreAsync(CreateTracePoint("msg-006", TracePointType.Consume, "topic", "group-A"));
        await _store.StoreAsync(CreateTracePoint("msg-007", TracePointType.Consume, "topic", "group-B"));
        await _store.StoreAsync(CreateTracePoint("msg-008", TracePointType.Acknowledge, "topic", "group-A"));

        var startTime = DateTimeOffset.UtcNow.AddMinutes(-1);
        var endTime = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        var results = await _store.QueryAsync(null, "group-A", startTime, endTime);

        // Assert
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.ConsumerGroup == "group-A"), Is.True);
    }

    [Test]
    public async Task QueryAsync_ByTimeRange_ShouldFilterCorrectly()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var oldTracePoint = new MessageTracePoint
        {
            TracePointId = "TP-old",
            MessageId = "msg-old",
            Type = TracePointType.Born,
            NodeId = "test-node",
            Topic = "test-topic",
            Timestamp = now.AddHours(-2)
        };
        var newTracePoint = CreateTracePoint("msg-new", TracePointType.Born, "test-topic");

        await _store.StoreAsync(oldTracePoint);
        await _store.StoreAsync(newTracePoint);

        // Act - 查询最近1小时
        var results = await _store.QueryAsync(null, null, now.AddHours(-1), now.AddMinutes(1));

        // Assert
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].MessageId, Is.EqualTo("msg-new"));
    }

    [Test]
    public async Task CleanupAsync_ShouldRemoveExpiredTracePoints()
    {
        // Arrange - 创建已过期的追踪点
        var expiredTracePoint = new MessageTracePoint
        {
            TracePointId = "TP-expired",
            MessageId = "msg-expired",
            Type = TracePointType.Born,
            NodeId = "test-node",
            Topic = "test-topic",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-3)
        };
        var validTracePoint = CreateTracePoint("msg-valid", TracePointType.Born, "test-topic");

        await _store.StoreAsync(expiredTracePoint);
        await _store.StoreAsync(validTracePoint);

        // Act - 清理2小时前的数据
        await _store.CleanupAsync(TimeSpan.FromHours(2));

        // Assert
        var expiredResults = await _store.GetByMessageIdAsync("msg-expired");
        var validResults = await _store.GetByMessageIdAsync("msg-valid");

        Assert.That(expiredResults, Is.Empty);
        Assert.That(validResults, Has.Count.EqualTo(1));
    }

    private static MessageTracePoint CreateTracePoint(
        string messageId,
        TracePointType type,
        string? topic = null,
        string? consumerGroup = null)
    {
        return new MessageTracePoint
        {
            TracePointId = $"TP-{Guid.NewGuid():N}",
            MessageId = messageId,
            Type = type,
            NodeId = "test-node",
            Topic = topic,
            ConsumerGroup = consumerGroup,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// 消息追踪器测试
/// </summary>
[TestFixture]
public class MessageTracerTests
{
    private MemoryMessageTraceStore _store = null!;
    private MessageTracer _tracer = null!;

    [SetUp]
    public void Setup()
    {
        _store = new MemoryMessageTraceStore(
            NullLogger<MemoryMessageTraceStore>.Instance);
        _tracer = new MessageTracer(
            NullLogger<MessageTracer>.Instance,
            _store,
            new MessageTracerOptions { NodeId = "test-tracer" });
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public async Task RecordAsync_ShouldStoreTracePoint()
    {
        // Arrange
        var tracePoint = new MessageTracePoint
        {
            TracePointId = "TP-001",
            MessageId = "msg-trace-001",
            Type = TracePointType.Born,
            NodeId = "producer-1",
            Topic = "test-topic"
        };

        // Act
        await _tracer.RecordAsync(tracePoint);

        // Assert
        var trace = await _tracer.GetTraceAsync("msg-trace-001");
        Assert.That(trace, Is.Not.Null);
        Assert.That(trace!.TracePoints, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RecordAsync_WhenDisabled_ShouldNotStore()
    {
        // Arrange
        var disabledTracer = new MessageTracer(
            NullLogger<MessageTracer>.Instance,
            _store,
            new MessageTracerOptions { Enabled = false });

        var tracePoint = new MessageTracePoint
        {
            TracePointId = "TP-disabled",
            MessageId = "msg-disabled",
            Type = TracePointType.Born,
            NodeId = "test"
        };

        // Act
        await disabledTracer.RecordAsync(tracePoint);

        // Assert
        var trace = await disabledTracer.GetTraceAsync("msg-disabled");
        Assert.That(trace, Is.Null);
    }

    [Test]
    public async Task RecordBornAsync_ShouldCreateBornTracePoint()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "test-body");

        // Act
        await _tracer.RecordBornAsync(message);

        // Assert
        var trace = await _tracer.GetTraceAsync(message.MessageId);
        Assert.That(trace, Is.Not.Null);
        Assert.That(trace!.BornTime, Is.Not.Null);
        Assert.That(trace.TracePoints[0].Type, Is.EqualTo(TracePointType.Born));
    }

    [Test]
    public async Task RecordStoreAsync_ShouldCreateStoreTracePoint()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "test-body");

        // Act
        await _tracer.RecordStoreAsync(message, partition: 0, offset: 100, durationMs: 5);

        // Assert
        var trace = await _tracer.GetTraceAsync(message.MessageId);
        Assert.That(trace, Is.Not.Null);
        Assert.That(trace!.StoreTime, Is.Not.Null);
        Assert.That(trace.TracePoints[0].Partition, Is.EqualTo(0));
        Assert.That(trace.TracePoints[0].Offset, Is.EqualTo(100));
    }

    [Test]
    public async Task RecordConsumeAsync_ShouldRecordSuccessOrFailure()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "test-body");

        // Act - 记录成功消费
        await _tracer.RecordConsumeAsync(message, "consumer-group-1", durationMs: 10, success: true);

        // Assert
        var trace = await _tracer.GetTraceAsync(message.MessageId);
        Assert.That(trace, Is.Not.Null);
        Assert.That(trace!.TracePoints[0].Success, Is.True);
        Assert.That(trace.TracePoints[0].ConsumerGroup, Is.EqualTo("consumer-group-1"));
    }

    [Test]
    public async Task RecordRetryAsync_ShouldRecordRetryCount()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "test-body");

        // Act
        await _tracer.RecordRetryAsync(message, "consumer-group-1", retryCount: 3, error: "Processing failed");

        // Assert
        var trace = await _tracer.GetTraceAsync(message.MessageId);
        Assert.That(trace, Is.Not.Null);
        Assert.That(trace!.TracePoints[0].Type, Is.EqualTo(TracePointType.Retry));
        Assert.That(trace.TracePoints[0].Properties["RetryCount"], Is.EqualTo("3"));
    }

    [Test]
    public async Task RecordDeadLetterAsync_ShouldRecordReason()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "test-body");

        // Act
        await _tracer.RecordDeadLetterAsync(message, "consumer-group-1", "Max retries exceeded");

        // Assert
        var trace = await _tracer.GetTraceAsync(message.MessageId);
        Assert.That(trace, Is.Not.Null);
        Assert.That(trace!.IsDeadLettered, Is.True);
        Assert.That(trace.TracePoints[0].Error, Is.EqualTo("Max retries exceeded"));
    }

    [Test]
    public async Task GetTraceAsync_ShouldCalculateEndToEndLatency()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "test-body");
        await _tracer.RecordBornAsync(message);
        await Task.Delay(50); // 模拟延迟
        await _tracer.RecordConsumeAsync(message, "consumer-group-1", success: true);

        // Act
        var trace = await _tracer.GetTraceAsync(message.MessageId);

        // Assert
        Assert.That(trace, Is.Not.Null);
        Assert.That(trace!.EndToEndLatencyMs, Is.GreaterThanOrEqualTo(50));
    }

    [Test]
    public async Task QueryByTopicAsync_ShouldReturnMatchingTracePoints()
    {
        // Arrange
        var message1 = CreateTestMessage("topic-query", "body-1");
        var message2 = CreateTestMessage("topic-query", "body-2");
        await _tracer.RecordBornAsync(message1);
        await _tracer.RecordBornAsync(message2);

        var startTime = DateTimeOffset.UtcNow.AddMinutes(-1);
        var endTime = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        var results = await _tracer.QueryByTopicAsync("topic-query", startTime, endTime);

        // Assert
        Assert.That(results, Has.Count.EqualTo(2));
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
/// 追踪上下文测试
/// </summary>
[TestFixture]
public class TraceContextHolderTests
{
    [Test]
    public void CreateNew_ShouldGenerateValidContext()
    {
        // Act
        var context = TraceContextHolder.CreateNew();

        // Assert
        Assert.That(context.TraceId, Is.Not.Null.And.Not.Empty);
        Assert.That(context.SpanId, Is.Not.Null.And.Not.Empty);
        Assert.That(context.Sampled, Is.True);
    }

    [Test]
    public void CreateNew_WithParent_ShouldInheritTraceId()
    {
        // Arrange
        var parentTraceId = "parent-trace-id-123";
        var parentSpanId = "parent-span-456";

        // Act
        var context = TraceContextHolder.CreateNew(parentTraceId, parentSpanId);

        // Assert
        Assert.That(context.TraceId, Is.EqualTo(parentTraceId));
        Assert.That(context.ParentSpanId, Is.EqualTo(parentSpanId));
        Assert.That(context.SpanId, Is.Not.EqualTo(parentSpanId)); // 新的 SpanId
    }

    [Test]
    public void ExtractFromHeaders_WithValidHeaders_ShouldReturnContext()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            ["X-Trace-Id"] = "trace-123",
            ["X-Span-Id"] = "span-456",
            ["X-Parent-Span-Id"] = "parent-789",
            ["X-Sampled"] = "1"
        };

        // Act
        var context = TraceContextHolder.ExtractFromHeaders(headers);

        // Assert
        Assert.That(context, Is.Not.Null);
        Assert.That(context!.TraceId, Is.EqualTo("trace-123"));
        Assert.That(context.SpanId, Is.EqualTo("span-456"));
        Assert.That(context.ParentSpanId, Is.EqualTo("parent-789"));
        Assert.That(context.Sampled, Is.True);
    }

    [Test]
    public void ExtractFromHeaders_WithoutTraceId_ShouldReturnNull()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            ["X-Span-Id"] = "span-456"
        };

        // Act
        var context = TraceContextHolder.ExtractFromHeaders(headers);

        // Assert
        Assert.That(context, Is.Null);
    }

    [Test]
    public void InjectToHeaders_ShouldAddAllHeaders()
    {
        // Arrange
        var context = new TraceContext
        {
            TraceId = "inject-trace-id",
            SpanId = "inject-span-id",
            ParentSpanId = "inject-parent-span",
            Sampled = false
        };
        var headers = new Dictionary<string, string>();

        // Act
        TraceContextHolder.InjectToHeaders(context, headers);

        // Assert
        Assert.That(headers["X-Trace-Id"], Is.EqualTo("inject-trace-id"));
        Assert.That(headers["X-Span-Id"], Is.EqualTo("inject-span-id"));
        Assert.That(headers["X-Parent-Span-Id"], Is.EqualTo("inject-parent-span"));
        Assert.That(headers["X-Sampled"], Is.EqualTo("0"));
    }

    [Test]
    public void Current_ShouldBeAsyncLocal()
    {
        // Arrange
        TraceContextHolder.Current = null;

        // Act
        var context = TraceContextHolder.CreateNew();
        TraceContextHolder.Current = context;

        // Assert
        Assert.That(TraceContextHolder.Current, Is.SameAs(context));

        // Cleanup
        TraceContextHolder.Current = null;
    }
}

/// <summary>
/// 消息轨迹记录完整性测试
/// </summary>
[TestFixture]
public class MessageTraceIntegrationTests
{
    private MemoryMessageTraceStore _store = null!;
    private MessageTracer _tracer = null!;

    [SetUp]
    public void Setup()
    {
        _store = new MemoryMessageTraceStore(
            NullLogger<MemoryMessageTraceStore>.Instance);
        _tracer = new MessageTracer(
            NullLogger<MessageTracer>.Instance,
            _store,
            new MessageTracerOptions { NodeId = "integration-test" });
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public async Task FullMessageLifecycle_ShouldRecordAllTracePoints()
    {
        // Arrange
        var message = MessageBuilder.Create()
            .Topic("lifecycle-topic")
            .Body("lifecycle-body")
            .Build();
        var consumerGroup = "test-consumer-group";

        // Act - 模拟完整的消息生命周期
        await _tracer.RecordBornAsync(message);
        await _tracer.RecordStoreAsync(message, partition: 0, offset: 1);
        await _tracer.RecordDispatchAsync(message, consumerGroup);
        await _tracer.RecordConsumeAsync(message, consumerGroup, durationMs: 15, success: true);
        await _tracer.RecordAcknowledgeAsync(message, consumerGroup, partition: 0, offset: 1);

        // Assert
        var trace = await _tracer.GetTraceAsync(message.MessageId);
        Assert.That(trace, Is.Not.Null);
        Assert.That(trace!.TracePoints, Has.Count.EqualTo(5));
        Assert.That(trace.IsConsumed, Is.True);
        Assert.That(trace.IsDeadLettered, Is.False);
        Assert.That(trace.RetryCount, Is.EqualTo(0));
    }

    [Test]
    public async Task FailedMessageWithRetries_ShouldRecordCorrectly()
    {
        // Arrange
        var message = MessageBuilder.Create()
            .Topic("retry-topic")
            .Body("retry-body")
            .Build();
        var consumerGroup = "retry-consumer-group";

        // Act - 模拟失败和重试场景
        await _tracer.RecordBornAsync(message);
        await _tracer.RecordStoreAsync(message, partition: 0, offset: 2);
        await _tracer.RecordDispatchAsync(message, consumerGroup);
        await _tracer.RecordConsumeAsync(message, consumerGroup, success: false, error: "First attempt failed");
        await _tracer.RecordRetryAsync(message, consumerGroup, retryCount: 1, error: "First attempt failed");
        await _tracer.RecordConsumeAsync(message, consumerGroup, success: false, error: "Second attempt failed");
        await _tracer.RecordRetryAsync(message, consumerGroup, retryCount: 2, error: "Second attempt failed");
        await _tracer.RecordDeadLetterAsync(message, consumerGroup, "Max retries exceeded");

        // Assert
        var trace = await _tracer.GetTraceAsync(message.MessageId);
        Assert.That(trace, Is.Not.Null);
        Assert.That(trace!.RetryCount, Is.EqualTo(2));
        Assert.That(trace.IsDeadLettered, Is.True);
        Assert.That(trace.IsConsumed, Is.False);
    }
}
