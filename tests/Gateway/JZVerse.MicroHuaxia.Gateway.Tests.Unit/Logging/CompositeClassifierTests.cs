using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Logging.Classification;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Logging;

[TestFixture]
public sealed class CompositeClassifierTests
{
    [Test]
    public void Classify_ServiceStrategy_ShouldReturnServiceName()
    {
        // Arrange
        var options = Options.Create(new GatewayLoggingOptions
        {
            ClassificationStrategy = ClassificationStrategy.Service
        });
        var classifier = new CompositeClassifier(options);
        var entry = CreateLogEntry("GatewayHttp", LogLevel.Information);

        // Act
        var classification = classifier.Classify(entry);

        // Assert
        classification.Should().Be("GatewayHttp");
    }

    [Test]
    public void Classify_LevelStrategy_ShouldReturnLevel()
    {
        // Arrange
        var options = Options.Create(new GatewayLoggingOptions
        {
            ClassificationStrategy = ClassificationStrategy.Level
        });
        var classifier = new CompositeClassifier(options);
        var entry = CreateLogEntry("TestService", LogLevel.Error);

        // Act
        var classification = classifier.Classify(entry);

        // Assert
        classification.Should().Be("Error");
    }

    [Test]
    public void Classify_TimeStrategy_ShouldReturnDatePath()
    {
        // Arrange
        var options = Options.Create(new GatewayLoggingOptions
        {
            ClassificationStrategy = ClassificationStrategy.Time
        });
        var classifier = new CompositeClassifier(options);
        var entry = CreateLogEntryWithTimestamp("TestService", LogLevel.Information,
            new DateTimeOffset(2026, 2, 7, 10, 30, 0, TimeSpan.Zero));

        // Act
        var classification = classifier.Classify(entry);

        // Assert
        classification.Should().Be("2026/02/07");
    }

    [Test]
    public void Classify_CompositeStrategy_ShouldReturnCombinedPath()
    {
        // Arrange
        var options = Options.Create(new GatewayLoggingOptions
        {
            ClassificationStrategy = ClassificationStrategy.Composite
        });
        var classifier = new CompositeClassifier(options);
        var entry = CreateLogEntryWithTimestamp("GatewayHttp", LogLevel.Error,
            new DateTimeOffset(2026, 2, 7, 10, 30, 0, TimeSpan.Zero));

        // Act
        var classification = classifier.Classify(entry);

        // Assert
        classification.Should().Be("GatewayHttp/Error/2026-02-07");
    }

    [Test]
    public void Classify_WithNullServiceName_ShouldUseDefault()
    {
        // Arrange
        var options = Options.Create(new GatewayLoggingOptions
        {
            ClassificationStrategy = ClassificationStrategy.Service
        });
        var classifier = new CompositeClassifier(options);
        var entry = CreateLogEntry(null, LogLevel.Information);

        // Act
        var classification = classifier.Classify(entry);

        // Assert
        classification.Should().Be("Unknown");
    }

    [Test]
    public void Classify_WithEmptyServiceName_ShouldReturnEmpty()
    {
        // Arrange
        var options = Options.Create(new GatewayLoggingOptions
        {
            ClassificationStrategy = ClassificationStrategy.Service
        });
        var classifier = new CompositeClassifier(options);
        var entry = CreateLogEntry("", LogLevel.Information);

        // Act
        var classification = classifier.Classify(entry);

        // Assert - 空字符串经过 SanitizePath 后仍为空
        classification.Should().BeEmpty();
    }

    private static LogEntry CreateLogEntry(string? serviceName, LogLevel level)
    {
        return new LogEntry
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Category = "TestCategory",
            Message = "Test message",
            ServiceName = serviceName
        };
    }

    private static LogEntry CreateLogEntryWithTimestamp(string? serviceName, LogLevel level, DateTimeOffset timestamp)
    {
        return new LogEntry
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = timestamp,
            Level = level,
            Category = "TestCategory",
            Message = "Test message",
            ServiceName = serviceName
        };
    }
}
