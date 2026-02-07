using System.Diagnostics;
using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Tracing;
using JZVerse.MicroHuaxia.Gateway.Tracing.Configuration;
using JZVerse.MicroHuaxia.Gateway.Tracing.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Tracing.Storage;

[TestFixture]
public sealed class InMemoryTraceStoreTests
{
    private InMemoryTraceStore _store = null!;

    [SetUp]
    public void Setup()
    {
        var options = Options.Create(
            new GatewayTracingOptions
            {
                InMemoryStore = new InMemoryStoreOptions
                {
                    Enabled = true,
                    MaxSpans = 1000,
                    RetentionMinutes = 30,
                },
            }
        );

        _store = new InMemoryTraceStore(options, NullLogger<InMemoryTraceStore>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public void AddSpan_ShouldStoreSpan()
    {
        // Arrange
        var span = CreateTestSpan("trace-1", "span-1", "test-operation");

        // Act
        _store.AddSpan(span);

        // Assert
        var trace = _store.GetTrace("trace-1");
        trace.Should().HaveCount(1);
        trace[0].SpanId.Should().Be("span-1");
    }

    [Test]
    public void AddSpan_MultipleTimes_ShouldGroupByTraceId()
    {
        // Arrange
        var span1 = CreateTestSpan("trace-1", "span-1", "operation-1");
        var span2 = CreateTestSpan("trace-1", "span-2", "operation-2", parentSpanId: "span-1");

        // Act
        _store.AddSpan(span1);
        _store.AddSpan(span2);

        // Assert
        var trace = _store.GetTrace("trace-1");
        trace.Should().HaveCount(2);
    }

    [Test]
    public void GetTrace_WithNonExistentTraceId_ShouldReturnEmptyList()
    {
        // Act
        var trace = _store.GetTrace("non-existent");

        // Assert
        trace.Should().BeEmpty();
    }

    [Test]
    public void GetSpan_WithValidIds_ShouldReturnSpan()
    {
        // Arrange
        var span = CreateTestSpan("trace-1", "span-1", "test-operation");
        _store.AddSpan(span);

        // Act
        var result = _store.GetSpan("trace-1", "span-1");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("test-operation");
    }

    [Test]
    public void GetSpan_WithInvalidIds_ShouldReturnNull()
    {
        // Act
        var result = _store.GetSpan("non-existent", "span-1");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void GetLatestTraces_ShouldReturnTracesInDescendingOrder()
    {
        // Arrange
        var span1 = CreateTestSpan("trace-1", "span-1", "operation-1");
        var span2 = CreateTestSpan("trace-2", "span-2", "operation-2");
        var span3 = CreateTestSpan("trace-3", "span-3", "operation-3");

        _store.AddSpan(span1);
        _store.AddSpan(span2);
        _store.AddSpan(span3);

        // Act
        var traces = _store.GetLatestTraces(2);

        // Assert
        traces.Should().HaveCount(2);
    }

    [Test]
    public void GetStatistics_ShouldReturnCorrectCounts()
    {
        // Arrange
        var span1 = CreateTestSpan("trace-1", "span-1", "operation-1");
        var span2 = CreateTestSpan("trace-1", "span-2", "operation-2");
        var span3 = CreateTestSpan("trace-2", "span-3", "operation-3");

        _store.AddSpan(span1);
        _store.AddSpan(span2);
        _store.AddSpan(span3);

        // Act
        var stats = _store.GetStatistics();

        // Assert
        stats.TotalTraces.Should().Be(2);
        stats.TotalSpans.Should().Be(3);
    }

    [Test]
    public void Query_WithNoFilter_ShouldReturnAllTraces()
    {
        // Arrange
        _store.AddSpan(CreateTestSpan("trace-1", "span-1", "op-1"));
        _store.AddSpan(CreateTestSpan("trace-2", "span-2", "op-2"));
        _store.AddSpan(CreateTestSpan("trace-3", "span-3", "op-3"));

        // Act
        var result = _store.Query(new TraceQuery { Take = 10 });

        // Assert
        result.TotalCount.Should().Be(3);
        result.Traces.Should().HaveCount(3);
    }

    [Test]
    public void Query_WithServiceNameFilter_ShouldFilterTraces()
    {
        // Arrange
        _store.AddSpan(CreateTestSpan("trace-1", "span-1", "op-1", serviceName: "service-a"));
        _store.AddSpan(CreateTestSpan("trace-2", "span-2", "op-2", serviceName: "service-b"));

        // Act
        var result = _store.Query(new TraceQuery { ServiceName = "service-a", Take = 10 });

        // Assert
        result.TotalCount.Should().Be(1);
        result.Traces[0].Services.Should().Contain("service-a");
    }

    [Test]
    public void Query_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _store.AddSpan(CreateTestSpan($"trace-{i}", $"span-{i}", $"op-{i}"));
        }

        // Act
        var result = _store.Query(new TraceQuery { Skip = 3, Take = 3 });

        // Assert
        result.Traces.Should().HaveCount(3);
        result.TotalCount.Should().Be(10);
        result.HasMore.Should().BeTrue();
    }

    [Test]
    public void Clear_ShouldRemoveAllTraces()
    {
        // Arrange
        _store.AddSpan(CreateTestSpan("trace-1", "span-1", "op-1"));
        _store.AddSpan(CreateTestSpan("trace-2", "span-2", "op-2"));

        // Act
        _store.Clear();

        // Assert
        _store.GetTrace("trace-1").Should().BeEmpty();
        _store.GetTrace("trace-2").Should().BeEmpty();
        _store.GetStatistics().TotalSpans.Should().Be(0);
    }

    private static TraceSpan CreateTestSpan(
        string traceId,
        string spanId,
        string name,
        string? parentSpanId = null,
        string serviceName = "test-service"
    )
    {
        return new TraceSpan
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            Name = name,
            Kind = ActivityKind.Server,
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow.AddMilliseconds(100),
            Status = ActivityStatusCode.Ok,
            ServiceName = serviceName,
            Attributes = [],
            Events = [],
        };
    }
}

[TestFixture]
public sealed class TraceSpanTests
{
    [Test]
    public void FromActivity_ShouldCreateValidTraceSpan()
    {
        // Arrange
        using var activity = new Activity("test-operation");
        activity.SetTag("test-key", "test-value");
        activity.Start();
        activity.Stop();

        // Act
        var span = TraceSpan.FromActivity(activity);

        // Assert
        span.TraceId.Should().Be(activity.TraceId.ToHexString());
        span.SpanId.Should().Be(activity.SpanId.ToHexString());
        span.Name.Should().Be("test-operation");
        span.Attributes.Should().ContainKey("test-key");
    }

    [Test]
    public void Duration_ShouldCalculateCorrectly()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddSeconds(5);

        var span = new TraceSpan
        {
            TraceId = "trace-1",
            SpanId = "span-1",
            Name = "test",
            StartTime = startTime,
            EndTime = endTime,
        };

        // Assert
        span.Duration.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void Duration_WithNullEndTime_ShouldReturnNull()
    {
        // Arrange
        var span = new TraceSpan
        {
            TraceId = "trace-1",
            SpanId = "span-1",
            Name = "test",
            StartTime = DateTimeOffset.UtcNow,
            EndTime = null,
        };

        // Assert
        span.Duration.Should().BeNull();
    }
}
