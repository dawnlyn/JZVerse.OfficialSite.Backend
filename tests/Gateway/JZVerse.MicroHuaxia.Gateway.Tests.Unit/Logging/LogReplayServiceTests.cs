using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using JZVerse.MicroHuaxia.Gateway.Logging.Replay;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Logging;

[TestFixture]
public sealed class LogReplayServiceTests
{
    private InMemoryLogStore _store = null!;
    private LogReplayService _replayService = null!;

    [SetUp]
    public void Setup()
    {
        var storeOptions = Options.Create(new InMemoryLogStoreOptions
        {
            MaxCapacity = 1000,
            EnableIndexing = true
        });
        _store = new InMemoryLogStore(storeOptions, NullLogger<InMemoryLogStore>.Instance);
        _replayService = new LogReplayService(_store, NullLogger<LogReplayService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public async Task ReplayByTraceAsync_ShouldReturnEntriesInOrder()
    {
        // Arrange
        var traceId = "trace-123";
        var now = DateTimeOffset.UtcNow;

        await _store.AddBatchAsync([
            CreateLogEntry("1", LogLevel.Information, "Start", now, traceId, "ServiceA"),
            CreateLogEntry("2", LogLevel.Information, "Processing", now.AddMilliseconds(100), traceId, "ServiceB"),
            CreateLogEntry("3", LogLevel.Information, "End", now.AddMilliseconds(200), traceId, "ServiceA"),
            CreateLogEntry("4", LogLevel.Information, "Unrelated", now, "other-trace", "ServiceC")
        ]);

        // Act
        var result = await _replayService.ReplayByTraceAsync(traceId);

        // Assert
        result.TraceId.Should().Be(traceId);
        result.Entries.Should().HaveCount(3);
        result.Entries.Should().BeInAscendingOrder(e => e.Timestamp);
        result.Services.Should().Contain(["ServiceA", "ServiceB"]);
    }

    [Test]
    public async Task ReplayByTraceAsync_ShouldCalculateDuration()
    {
        // Arrange
        var traceId = "trace-456";
        var now = DateTimeOffset.UtcNow;

        await _store.AddBatchAsync([
            CreateLogEntry("1", LogLevel.Information, "Start", now, traceId, "ServiceA"),
            CreateLogEntry("2", LogLevel.Information, "End", now.AddMilliseconds(500), traceId, "ServiceA")
        ]);

        // Act
        var result = await _replayService.ReplayByTraceAsync(traceId);

        // Assert
        result.TotalDurationMs.Should().BeApproximately(500, 10);
    }

    [Test]
    public async Task ReplayByTraceAsync_WithErrors_ShouldSetHasErrors()
    {
        // Arrange
        var traceId = "trace-error";
        var now = DateTimeOffset.UtcNow;

        await _store.AddBatchAsync([
            CreateLogEntry("1", LogLevel.Information, "Start", now, traceId, "ServiceA"),
            CreateLogEntry("2", LogLevel.Error, "Something failed", now.AddMilliseconds(100), traceId, "ServiceA")
        ]);

        // Act
        var result = await _replayService.ReplayByTraceAsync(traceId);

        // Assert
        result.HasErrors.Should().BeTrue();
    }

    [Test]
    public async Task ReplayByTraceAsync_WithoutErrors_ShouldNotSetHasErrors()
    {
        // Arrange
        var traceId = "trace-success";
        var now = DateTimeOffset.UtcNow;

        await _store.AddBatchAsync([
            CreateLogEntry("1", LogLevel.Information, "Start", now, traceId, "ServiceA"),
            CreateLogEntry("2", LogLevel.Information, "End", now.AddMilliseconds(100), traceId, "ServiceA")
        ]);

        // Act
        var result = await _replayService.ReplayByTraceAsync(traceId);

        // Assert
        result.HasErrors.Should().BeFalse();
    }

    [Test]
    public async Task AnalyzeSlowQueriesAsync_ShouldFindSlowRequests()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        await _store.AddBatchAsync([
            CreateLogEntryWithDuration("1", "/api/slow", "GET", 1500, now),
            CreateLogEntryWithDuration("2", "/api/slow", "GET", 2000, now.AddSeconds(1)),
            CreateLogEntryWithDuration("3", "/api/fast", "GET", 50, now.AddSeconds(2)),
            CreateLogEntryWithDuration("4", "/api/slow", "GET", 1800, now.AddSeconds(3))
        ]);

        // Act
        var results = await _replayService.AnalyzeSlowQueriesAsync(
            thresholdMs: 1000,
            start: now.AddMinutes(-1),
            end: now.AddMinutes(1));

        // Assert
        results.Should().HaveCount(1);
        results[0].RequestPath.Should().Be("/api/slow");
        results[0].Count.Should().Be(3);
        results[0].AvgDurationMs.Should().BeApproximately(1766.67, 1);
    }

    [Test]
    public async Task AnalyzeSlowQueriesAsync_ShouldCalculatePercentiles()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Add 100 entries with increasing durations
        var entries = new List<LogEntry>();
        for (int i = 1; i <= 100; i++)
        {
            entries.Add(CreateLogEntryWithDuration($"{i}", "/api/test", "GET", i * 10, now.AddSeconds(i)));
        }
        await _store.AddBatchAsync(entries);

        // Act
        var results = await _replayService.AnalyzeSlowQueriesAsync(
            thresholdMs: 0,
            start: now.AddMinutes(-1),
            end: now.AddMinutes(5));

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.MinDurationMs.Should().Be(10);
        result.MaxDurationMs.Should().Be(1000);
        result.P95DurationMs.Should().BeApproximately(950, 50);
        result.P99DurationMs.Should().BeApproximately(990, 20);
    }

    [Test]
    public async Task ReplayAsync_ShouldReturnFilteredEntries()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        await _store.AddBatchAsync([
            CreateLogEntry("1", LogLevel.Information, "Info 1", now, serviceName: "ServiceA"),
            CreateLogEntry("2", LogLevel.Error, "Error 1", now.AddSeconds(1), serviceName: "ServiceA"),
            CreateLogEntry("3", LogLevel.Information, "Info 2", now.AddSeconds(2), serviceName: "ServiceB")
        ]);

        var query = new LogQuery
        {
            ServiceName = "ServiceA",
            StartTime = now.AddMinutes(-1),
            EndTime = now.AddMinutes(1)
        };

        // Act
        var result = await _replayService.ReplayAsync(query);

        // Assert
        result.Entries.Should().HaveCount(2);
        result.Entries.Should().OnlyContain(e => e.ServiceName == "ServiceA");
    }

    private static LogEntry CreateLogEntry(
        string id,
        LogLevel level,
        string message,
        DateTimeOffset? timestamp = null,
        string? traceId = null,
        string? serviceName = null)
    {
        return new LogEntry
        {
            Id = id,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            Level = level,
            Category = "TestCategory",
            Message = message,
            TraceId = traceId,
            ServiceName = serviceName ?? "TestService"
        };
    }

    private static LogEntry CreateLogEntryWithDuration(
        string id,
        string path,
        string method,
        double durationMs,
        DateTimeOffset timestamp)
    {
        return new LogEntry
        {
            Id = id,
            Timestamp = timestamp,
            Level = LogLevel.Information,
            Category = "TestCategory",
            Message = $"Request completed in {durationMs}ms",
            RequestPath = path,
            RequestMethod = method,
            DurationMs = durationMs,
            ServiceName = "TestService",
            TraceId = $"trace-{id}"
        };
    }
}
