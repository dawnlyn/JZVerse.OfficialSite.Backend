using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Logging.Storage;

[TestFixture]
public sealed class InMemoryLogStoreTests
{
    private InMemoryLogStore _store = null!;

    [SetUp]
    public void Setup()
    {
        var options = Options.Create(new InMemoryLogStoreOptions
        {
            MaxCapacity = 100,
            EnableIndexing = true,
            RetentionMinutes = 60
        });
        _store = new InMemoryLogStore(options, NullLogger<InMemoryLogStore>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public async Task Add_ShouldStoreLogEntry()
    {
        // Arrange
        var entry = CreateLogEntry("test-id", LogLevel.Information, "Test message");

        // Act
        await _store.AddAsync(entry);

        // Assert
        var result = await _store.GetByIdAsync("test-id");
        result.Should().NotBeNull();
        result!.Message.Should().Be("Test message");
    }

    [Test]
    public async Task AddBatch_ShouldStoreMultipleEntries()
    {
        // Arrange
        var entries = new[]
        {
            CreateLogEntry("id-1", LogLevel.Information, "Message 1"),
            CreateLogEntry("id-2", LogLevel.Warning, "Message 2"),
            CreateLogEntry("id-3", LogLevel.Error, "Message 3")
        };

        // Act
        await _store.AddBatchAsync(entries);

        // Assert
        var latest = await _store.GetLatestAsync(10);
        latest.Count.Should().Be(3);
    }

    [Test]
    public async Task Query_ByLevel_ShouldFilterCorrectly()
    {
        // Arrange
        await _store.AddBatchAsync([
            CreateLogEntry("id-1", LogLevel.Information, "Info message"),
            CreateLogEntry("id-2", LogLevel.Warning, "Warning message"),
            CreateLogEntry("id-3", LogLevel.Error, "Error message"),
            CreateLogEntry("id-4", LogLevel.Error, "Another error")
        ]);

        var query = new LogQuery { Levels = [LogLevel.Error] };

        // Act
        var result = await _store.QueryAsync(query);

        // Assert
        result.Entries.Should().HaveCount(2);
        result.Entries.Should().OnlyContain(e => e.Level == LogLevel.Error);
    }

    [Test]
    public async Task Query_ByMinLevel_ShouldFilterCorrectly()
    {
        // Arrange
        await _store.AddBatchAsync([
            CreateLogEntry("id-1", LogLevel.Debug, "Debug message"),
            CreateLogEntry("id-2", LogLevel.Information, "Info message"),
            CreateLogEntry("id-3", LogLevel.Warning, "Warning message"),
            CreateLogEntry("id-4", LogLevel.Error, "Error message")
        ]);

        var query = new LogQuery { MinLevel = LogLevel.Warning };

        // Act
        var result = await _store.QueryAsync(query);

        // Assert
        result.Entries.Should().HaveCount(2);
        result.Entries.Should().OnlyContain(e => e.Level >= LogLevel.Warning);
    }

    [Test]
    public async Task Query_ByTimeRange_ShouldFilterCorrectly()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        await _store.AddBatchAsync([
            CreateLogEntry("id-1", LogLevel.Information, "Old entry", now.AddHours(-2)),
            CreateLogEntry("id-2", LogLevel.Information, "Recent entry", now.AddMinutes(-30)),
            CreateLogEntry("id-3", LogLevel.Information, "Current entry", now)
        ]);

        var query = new LogQuery
        {
            StartTime = now.AddHours(-1),
            EndTime = now.AddMinutes(1)
        };

        // Act
        var result = await _store.QueryAsync(query);

        // Assert
        result.Entries.Should().HaveCount(2);
    }

    [Test]
    public async Task Query_BySearchText_ShouldFindMatches()
    {
        // Arrange
        await _store.AddBatchAsync([
            CreateLogEntry("id-1", LogLevel.Information, "User logged in"),
            CreateLogEntry("id-2", LogLevel.Information, "User logged out"),
            CreateLogEntry("id-3", LogLevel.Error, "Connection failed")
        ]);

        var query = new LogQuery { SearchText = "logged" };

        // Act
        var result = await _store.QueryAsync(query);

        // Assert
        result.Entries.Should().HaveCount(2);
        result.Entries.Should().OnlyContain(e => e.Message.Contains("logged"));
    }

    [Test]
    public async Task Query_ByTraceId_ShouldReturnRelatedEntries()
    {
        // Arrange
        var traceId = "trace-123";
        await _store.AddBatchAsync([
            CreateLogEntry("id-1", LogLevel.Information, "Start request", traceId: traceId),
            CreateLogEntry("id-2", LogLevel.Information, "Processing", traceId: traceId),
            CreateLogEntry("id-3", LogLevel.Information, "Unrelated", traceId: "other-trace")
        ]);

        // Act
        var result = await _store.GetByTraceIdAsync(traceId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(e => e.TraceId == traceId);
    }

    [Test]
    public async Task Query_ByServiceName_ShouldFilterCorrectly()
    {
        // Arrange
        await _store.AddBatchAsync([
            CreateLogEntry("id-1", LogLevel.Information, "Message 1", serviceName: "ServiceA"),
            CreateLogEntry("id-2", LogLevel.Information, "Message 2", serviceName: "ServiceB"),
            CreateLogEntry("id-3", LogLevel.Information, "Message 3", serviceName: "ServiceA")
        ]);

        var query = new LogQuery { ServiceName = "ServiceA" };

        // Act
        var result = await _store.QueryAsync(query);

        // Assert
        result.Entries.Should().HaveCount(2);
        result.Entries.Should().OnlyContain(e => e.ServiceName == "ServiceA");
    }

    [Test]
    public async Task Query_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        for (int i = 0; i < 50; i++)
        {
            await _store.AddAsync(CreateLogEntry($"id-{i}", LogLevel.Information, $"Message {i}"));
        }

        var query = new LogQuery { Skip = 10, Take = 20 };

        // Act
        var result = await _store.QueryAsync(query);

        // Assert
        result.Entries.Should().HaveCount(20);
        result.TotalCount.Should().Be(50);
        result.HasMore.Should().BeTrue();
    }

    [Test]
    public async Task GetStatistics_ShouldReturnCorrectCounts()
    {
        // Arrange
        await _store.AddBatchAsync([
            CreateLogEntry("id-1", LogLevel.Information, "Info", serviceName: "ServiceA"),
            CreateLogEntry("id-2", LogLevel.Warning, "Warning", serviceName: "ServiceA"),
            CreateLogEntry("id-3", LogLevel.Error, "Error", serviceName: "ServiceB"),
            CreateLogEntry("id-4", LogLevel.Error, "Error 2", serviceName: "ServiceB")
        ]);

        // Act
        var stats = await _store.GetStatisticsAsync();

        // Assert
        stats.CountByLevel[LogLevel.Information].Should().Be(1);
        stats.CountByLevel[LogLevel.Warning].Should().Be(1);
        stats.CountByLevel[LogLevel.Error].Should().Be(2);
        stats.CountByService["ServiceA"].Should().Be(2);
        stats.CountByService["ServiceB"].Should().Be(2);
    }

    [Test]
    public async Task Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        await _store.AddBatchAsync([
            CreateLogEntry("id-1", LogLevel.Information, "Message 1"),
            CreateLogEntry("id-2", LogLevel.Information, "Message 2")
        ]);

        // Act
        await _store.ClearAsync();

        // Assert
        var latest = await _store.GetLatestAsync(10);
        latest.Should().BeEmpty();
    }

    [Test]
    public async Task HealthCheck_ShouldReturnTrue()
    {
        // Act
        var isHealthy = await _store.HealthCheckAsync();

        // Assert
        isHealthy.Should().BeTrue();
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
}
