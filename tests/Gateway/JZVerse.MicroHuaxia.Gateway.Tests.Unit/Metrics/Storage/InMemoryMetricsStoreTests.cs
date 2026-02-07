using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Metrics;
using JZVerse.MicroHuaxia.Gateway.Metrics.Configuration;
using JZVerse.MicroHuaxia.Gateway.Metrics.Storage;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Metrics.Storage;

[TestFixture]
public sealed class InMemoryMetricsStoreTests
{
    private InMemoryMetricsStore _store = null!;

    [SetUp]
    public void Setup()
    {
        var options = Options.Create(
            new InMemoryMetricsStoreOptions
            {
                MaxDataPoints = 10000,
                RetentionMinutes = 60,
                BucketSizeSeconds = 10,
                CleanupIntervalMinutes = 5,
            }
        );

        _store = new InMemoryMetricsStore(options);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public void Record_ShouldStoreDataPoint()
    {
        // Arrange
        var name = GatewayMetricNames.RequestsTotal;
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };

        // Act
        _store.Record(name, 1, tags);

        // Assert
        var metricNames = _store.GetMetricNames();
        metricNames.Should().Contain(name);
    }

    [Test]
    public void Record_MultipleTimes_ShouldAccumulateValues()
    {
        // Arrange
        var name = GatewayMetricNames.RequestsTotal;
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };

        // Act
        for (int i = 0; i < 10; i++)
        {
            _store.Record(name, 1, tags);
        }

        // Assert
        var stats = _store.GetStatistics();
        stats.TotalDataPoints.Should().Be(10);
    }

    [Test]
    public void GetLatest_ShouldReturnLatestValue()
    {
        // Arrange
        var name = GatewayMetricNames.RequestsTotal;
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };

        _store.Record(name, 1, tags);
        _store.Record(name, 5, tags);
        _store.Record(name, 10, tags);

        // Act
        var latest = _store.GetLatest(name, tags);

        // Assert
        latest.Should().NotBeNull();
        latest!.Value.Should().Be(10);
        latest.Name.Should().Be(name);
    }

    [Test]
    public void GetLatest_WithNonExistentMetric_ShouldReturnNull()
    {
        // Act
        var latest = _store.GetLatest("non.existent.metric");

        // Assert
        latest.Should().BeNull();
    }

    [Test]
    public void GetTimeSeries_ShouldReturnDataPointsInTimeRange()
    {
        // Arrange
        var name = GatewayMetricNames.RequestDuration;
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };
        var now = DateTimeOffset.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            _store.Record(name, 100 + i * 10, tags);
        }

        // Act
        var timeSeries = _store.GetTimeSeries(name, now.AddMinutes(-1), now.AddMinutes(1), tags);

        // Assert
        timeSeries.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Test]
    public void GetTimeSeries_WithNonExistentMetric_ShouldReturnEmptyList()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var timeSeries = _store.GetTimeSeries("non.existent.metric", now.AddMinutes(-1), now.AddMinutes(1));

        // Assert
        timeSeries.Should().BeEmpty();
    }

    [Test]
    public void GetAggregations_ShouldReturnAggregatedData()
    {
        // Arrange
        var name = GatewayMetricNames.RequestDuration;
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };
        var now = DateTimeOffset.UtcNow;

        // Record some values
        _store.Record(name, 100, tags);
        _store.Record(name, 200, tags);
        _store.Record(name, 300, tags);
        _store.Record(name, 400, tags);
        _store.Record(name, 500, tags);

        // Act
        var aggregations = _store.GetAggregations(
            name,
            now.AddMinutes(-5),
            now.AddMinutes(5),
            TimeSpan.FromMinutes(10),
            tags
        );

        // Assert
        aggregations.Should().HaveCountGreaterThanOrEqualTo(1);
        if (aggregations.Count > 0)
        {
            var agg = aggregations[0];
            agg.Count.Should().Be(5);
            agg.Sum.Should().Be(1500);
            agg.Average.Should().Be(300);
            agg.Min.Should().Be(100);
            agg.Max.Should().Be(500);
        }
    }

    [Test]
    public void GetAggregations_WithNonExistentMetric_ShouldReturnEmptyList()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var aggregations = _store.GetAggregations(
            "non.existent.metric",
            now.AddMinutes(-1),
            now.AddMinutes(1),
            TimeSpan.FromMinutes(1)
        );

        // Assert
        aggregations.Should().BeEmpty();
    }

    [Test]
    public void GetMetricNames_ShouldReturnAllRecordedMetrics()
    {
        // Arrange
        _store.Record(GatewayMetricNames.RequestsTotal, 1);
        _store.Record(GatewayMetricNames.RequestDuration, 100);
        _store.Record(GatewayMetricNames.CacheHits, 1);

        // Act
        var names = _store.GetMetricNames();

        // Assert
        names.Should().HaveCount(3);
        names.Should().Contain(GatewayMetricNames.RequestsTotal);
        names.Should().Contain(GatewayMetricNames.RequestDuration);
        names.Should().Contain(GatewayMetricNames.CacheHits);
    }

    [Test]
    public void GetTagCombinations_ShouldReturnAllTagCombinations()
    {
        // Arrange
        var name = GatewayMetricNames.RequestsTotal;

        _store.Record(name, 1, new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" });
        _store.Record(name, 1, new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-2" });
        _store.Record(name, 1, new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-3" });

        // Act
        var combinations = _store.GetTagCombinations(name);

        // Assert
        combinations.Should().HaveCount(3);
    }

    [Test]
    public void GetTagCombinations_WithNonExistentMetric_ShouldReturnEmptyList()
    {
        // Act
        var combinations = _store.GetTagCombinations("non.existent.metric");

        // Assert
        combinations.Should().BeEmpty();
    }

    [Test]
    public void Clear_ShouldRemoveAllData()
    {
        // Arrange
        _store.Record(GatewayMetricNames.RequestsTotal, 1);
        _store.Record(GatewayMetricNames.RequestDuration, 100);

        // Act
        _store.Clear();

        // Assert
        var names = _store.GetMetricNames();
        names.Should().BeEmpty();

        var stats = _store.GetStatistics();
        stats.TotalDataPoints.Should().Be(0);
        stats.MetricCount.Should().Be(0);
    }

    [Test]
    public void GetStatistics_ShouldReturnCorrectCounts()
    {
        // Arrange
        var tags1 = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };
        var tags2 = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-2" };

        _store.Record(GatewayMetricNames.RequestsTotal, 1, tags1);
        _store.Record(GatewayMetricNames.RequestsTotal, 1, tags1);
        _store.Record(GatewayMetricNames.RequestsTotal, 1, tags2);
        _store.Record(GatewayMetricNames.RequestDuration, 100, tags1);

        // Act
        var stats = _store.GetStatistics();

        // Assert
        stats.TotalDataPoints.Should().Be(4);
        stats.MetricCount.Should().Be(2);
        stats.BucketCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Test]
    public void Record_WithDifferentTags_ShouldStoreSeparately()
    {
        // Arrange
        var name = GatewayMetricNames.RequestsTotal;
        var tags1 = new Dictionary<string, object?>
        {
            [GatewayMetricTags.RouteId] = "route-1",
            [GatewayMetricTags.HttpMethod] = "GET",
        };
        var tags2 = new Dictionary<string, object?>
        {
            [GatewayMetricTags.RouteId] = "route-1",
            [GatewayMetricTags.HttpMethod] = "POST",
        };

        // Act
        _store.Record(name, 10, tags1);
        _store.Record(name, 20, tags2);

        // Assert
        var latest1 = _store.GetLatest(name, tags1);
        var latest2 = _store.GetLatest(name, tags2);

        latest1.Should().NotBeNull();
        latest2.Should().NotBeNull();
        latest1!.Value.Should().Be(10);
        latest2!.Value.Should().Be(20);
    }

    [Test]
    public void Dispose_ShouldPreventFurtherRecording()
    {
        // Arrange
        _store.Dispose();

        // Act - Should not throw
        _store.Record(GatewayMetricNames.RequestsTotal, 1);

        // Assert - Data should not be recorded
        var stats = _store.GetStatistics();
        stats.TotalDataPoints.Should().Be(0);
    }
}

[TestFixture]
public sealed class MetricBucketTests
{
    [Test]
    public void MetricBucket_ShouldAggregateValuesCorrectly()
    {
        // Arrange
        var tags = new Dictionary<string, object?> { ["route.id"] = "test" };
        var bucket = new MetricBucket(
            "test.metric",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(10),
            tags
        );

        // Act
        bucket.AddValue(10);
        bucket.AddValue(20);
        bucket.AddValue(30);
        bucket.AddValue(40);
        bucket.AddValue(50);

        // Assert
        var aggregation = bucket.GetAggregation();
        aggregation.Count.Should().Be(5);
        aggregation.Sum.Should().Be(150);
        aggregation.Average.Should().Be(30);
        aggregation.Min.Should().Be(10);
        aggregation.Max.Should().Be(50);
    }

    [Test]
    public void MetricBucket_WithSingleValue_ShouldReturnCorrectAggregation()
    {
        // Arrange
        var bucket = new MetricBucket(
            "test.metric",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(10),
            new Dictionary<string, object?>()
        );

        // Act
        bucket.AddValue(100);

        // Assert
        var aggregation = bucket.GetAggregation();
        aggregation.Count.Should().Be(1);
        aggregation.Sum.Should().Be(100);
        aggregation.Average.Should().Be(100);
        aggregation.Min.Should().Be(100);
        aggregation.Max.Should().Be(100);
        aggregation.P50.Should().Be(100);
        aggregation.P99.Should().Be(100);
    }

    [Test]
    public void MetricBucket_GetValues_ShouldReturnAllAddedValues()
    {
        // Arrange
        var bucket = new MetricBucket(
            "test.metric",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(10),
            new Dictionary<string, object?>()
        );

        // Act
        bucket.AddValue(1);
        bucket.AddValue(2);
        bucket.AddValue(3);

        // Assert
        var values = bucket.GetValues();
        values.Should().HaveCount(3);
        values.Should().ContainInOrder(1, 2, 3);
    }
}
