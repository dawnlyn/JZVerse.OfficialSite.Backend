using FluentAssertions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Logging;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Tests.Unit.Telemetry;

/// <summary>
/// InMemoryLogStore 单元测试
/// </summary>
[TestFixture]
public class InMemoryLogStoreTests
{
    private InMemoryLogStore _logStore = null!;

    [SetUp]
    public void SetUp()
    {
        _logStore = new InMemoryLogStore();
    }

    private LogEntry CreateTestLogEntry(
        LogLevel level = LogLevel.Information,
        string category = "TestCategory",
        string message = "Test message")
    {
        return new LogEntry
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Category = category,
            Message = message,
            ServiceName = "test-service"
        };
    }

    [Test]
    public void Add_ShouldStoreLogEntry()
    {
        // Arrange
        var entry = CreateTestLogEntry();

        // Act
        _logStore.Add(entry);
        var result = _logStore.Query(new LogQuery { Take = 10 });

        // Assert
        result.Logs.Should().ContainSingle();
        result.Logs[0].Message.Should().Be(entry.Message);
    }

    [Test]
    public void Add_ExceedMaxSize_ShouldRemoveOldest()
    {
        // Arrange
        var store = new InMemoryLogStore(_maxSize: 5);

        // Act
        for (int i = 0; i < 10; i++)
        {
            store.Add(CreateTestLogEntry(message: $"Message {i}"));
        }

        var result = store.Query(new LogQuery { Take = 100 });

        // Assert
        result.Logs.Should().HaveCount(5);
        result.Logs.Should().NotContain(e => e.Message == "Message 0");
    }

    [Test]
    public void Query_FilterByLevel_ShouldReturnMatchingEntries()
    {
        // Arrange
        _logStore.Add(CreateTestLogEntry(LogLevel.Information));
        _logStore.Add(CreateTestLogEntry(LogLevel.Warning));
        _logStore.Add(CreateTestLogEntry(LogLevel.Error));
        _logStore.Add(CreateTestLogEntry(LogLevel.Debug));

        // Act
        var result = _logStore.Query(new LogQuery { MinLevel = LogLevel.Warning });

        // Assert
        result.Logs.Should().HaveCount(2);
        result.Logs.Should().OnlyContain(e => e.Level >= LogLevel.Warning);
    }

    [Test]
    public void Query_FilterByCategory_ShouldReturnMatchingEntries()
    {
        // Arrange
        _logStore.Add(CreateTestLogEntry(category: "Category.A"));
        _logStore.Add(CreateTestLogEntry(category: "Category.B"));
        _logStore.Add(CreateTestLogEntry(category: "Other"));

        // Act
        var result = _logStore.Query(new LogQuery { Category = "Category" });

        // Assert
        result.Logs.Should().HaveCount(2);
    }

    [Test]
    public void Query_FilterBySearchText_ShouldReturnMatchingEntries()
    {
        // Arrange
        _logStore.Add(CreateTestLogEntry(message: "User logged in successfully"));
        _logStore.Add(CreateTestLogEntry(message: "Error occurred"));
        _logStore.Add(CreateTestLogEntry(message: "User logged out"));

        // Act
        var result = _logStore.Query(new LogQuery { SearchText = "logged" });

        // Assert
        result.Logs.Should().HaveCount(2);
    }

    [Test]
    public void Query_Pagination_ShouldWork()
    {
        // Arrange
        for (int i = 0; i < 25; i++)
        {
            _logStore.Add(CreateTestLogEntry(message: $"Message {i}"));
        }

        // Act - 使用 Skip/Take 代替 PageNumber/PageSize
        var page1 = _logStore.Query(new LogQuery { Skip = 0, Take = 10 });
        var page2 = _logStore.Query(new LogQuery { Skip = 10, Take = 10 });
        var page3 = _logStore.Query(new LogQuery { Skip = 20, Take = 10 });

        // Assert
        page1.Logs.Should().HaveCount(10);
        page2.Logs.Should().HaveCount(10);
        page3.Logs.Should().HaveCount(5);
        page1.TotalCount.Should().Be(25);
    }

    [Test]
    public void Query_FilterByTimeRange_ShouldReturnMatchingEntries()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        
        _logStore.Add(new LogEntry
        {
            Id = "1",
            Timestamp = now.AddHours(-2),
            Level = LogLevel.Information,
            Message = "Old entry",
            Category = "Test"
        });
        _logStore.Add(new LogEntry
        {
            Id = "2",
            Timestamp = now,
            Level = LogLevel.Information,
            Message = "Current entry",
            Category = "Test"
        });

        // Act
        var result = _logStore.Query(new LogQuery
        {
            StartTime = now.AddHours(-1),
            EndTime = now.AddHours(1)
        });

        // Assert
        result.Logs.Should().ContainSingle();
        result.Logs[0].Message.Should().Be("Current entry");
    }

    [Test]
    public void GetLatest_ShouldReturnMostRecentEntries()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _logStore.Add(CreateTestLogEntry(message: $"Message {i}"));
        }

        // Act
        var result = _logStore.GetLatest(5);

        // Assert
        result.Should().HaveCount(5);
    }

    [Test]
    public void GetStatistics_ShouldReturnCorrectCounts()
    {
        // Arrange
        _logStore.Add(CreateTestLogEntry(LogLevel.Debug));
        _logStore.Add(CreateTestLogEntry(LogLevel.Information));
        _logStore.Add(CreateTestLogEntry(LogLevel.Information));
        _logStore.Add(CreateTestLogEntry(LogLevel.Warning));
        _logStore.Add(CreateTestLogEntry(LogLevel.Error));

        // Act
        var stats = _logStore.GetStatistics();

        // Assert
        stats.TotalCount.Should().Be(5);
        // API 使用 ByLevel 字典，key 是字符串类型
        stats.ByLevel[LogLevel.Debug.ToString()].Should().Be(1);
        stats.ByLevel[LogLevel.Information.ToString()].Should().Be(2);
        stats.ByLevel[LogLevel.Warning.ToString()].Should().Be(1);
        stats.ByLevel[LogLevel.Error.ToString()].Should().Be(1);
    }

    [Test]
    public void Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            _logStore.Add(CreateTestLogEntry());
        }

        // Act
        _logStore.Clear();
        var result = _logStore.Query(new LogQuery());

        // Assert
        result.Logs.Should().BeEmpty();
    }
}
