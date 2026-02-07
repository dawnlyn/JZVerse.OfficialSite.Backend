using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Metrics;
using JZVerse.MicroHuaxia.Gateway.Metrics.Api;
using JZVerse.MicroHuaxia.Gateway.Metrics.Api.Models;
using JZVerse.MicroHuaxia.Gateway.Metrics.Configuration;
using JZVerse.MicroHuaxia.Gateway.Metrics.Storage;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Metrics.Api;

[TestFixture]
public sealed class GatewayMetricsProviderTests
{
    private InMemoryMetricsStore _store = null!;
    private GatewayMetricsProvider _provider = null!;

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
        _provider = new GatewayMetricsProvider(_store);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
    }

    [Test]
    public async Task GetSnapshotAsync_WithNoData_ShouldReturnEmptySnapshot()
    {
        // Act
        var snapshot = await _provider.GetSnapshotAsync();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.TotalRequests.Should().Be(0);
        snapshot.ErrorRate.Should().Be(0);
        snapshot.CacheHitRate.Should().Be(0);
    }

    [Test]
    public async Task GetSnapshotAsync_WithData_ShouldReturnCorrectSnapshot()
    {
        // Arrange
        var tags = new Dictionary<string, object?>
        {
            [GatewayMetricTags.RouteId] = "route-1",
            [GatewayMetricTags.StatusCodeGroup] = "2xx",
        };

        for (int i = 0; i < 10; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, tags);
            _store.Record(GatewayMetricNames.RequestDuration, 100 + i * 10, tags);
        }

        // Act
        var snapshot = await _provider.GetSnapshotAsync();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.TotalRequests.Should().Be(10);
        snapshot.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task GetSnapshotAsync_WithErrors_ShouldCalculateErrorRate()
    {
        // Arrange
        var successTags = new Dictionary<string, object?>
        {
            [GatewayMetricTags.RouteId] = "route-1",
            [GatewayMetricTags.StatusCodeGroup] = "2xx",
        };

        var errorTags = new Dictionary<string, object?>
        {
            [GatewayMetricTags.RouteId] = "route-1",
            [GatewayMetricTags.ErrorType] = "server_error",
        };

        for (int i = 0; i < 8; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, successTags);
        }

        for (int i = 0; i < 2; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, successTags);
            _store.Record(GatewayMetricNames.ErrorsTotal, 1, errorTags);
        }

        // Act
        var snapshot = await _provider.GetSnapshotAsync();

        // Assert
        snapshot.ErrorRate.Should().BeApproximately(0.2, 0.01); // 2 errors out of 10 requests
    }

    [Test]
    public async Task GetTimeSeriesAsync_ShouldReturnTimeSeries()
    {
        // Arrange
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };
        var now = DateTimeOffset.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, tags);
        }

        // Act
        var timeSeries = await _provider.GetTimeSeriesAsync(
            GatewayMetricNames.RequestsTotal,
            now.AddMinutes(-5),
            now.AddMinutes(5),
            new Dictionary<string, string> { ["route.id"] = "route-1" },
            60
        );

        // Assert
        timeSeries.Should().NotBeNull();
        timeSeries.MetricName.Should().Be(GatewayMetricNames.RequestsTotal);
    }

    [Test]
    public async Task GetStatisticsAsync_WithNoData_ShouldReturnEmptyStatistics()
    {
        // Act
        var stats = await _provider.GetStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.Requests.Total.Should().Be(0);
        stats.Auth.TotalAttempts.Should().Be(0);
        stats.RateLimit.Allowed.Should().Be(0);
        stats.Cache.Hits.Should().Be(0);
        stats.Forward.TotalAttempts.Should().Be(0);
        stats.Resilience.RetryAttempts.Should().Be(0);
    }

    [Test]
    public async Task GetStatisticsAsync_WithData_ShouldReturnCorrectStatistics()
    {
        // Arrange - Record various metrics
        var routeTags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };

        // Request metrics
        for (int i = 0; i < 100; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, routeTags);
            _store.Record(GatewayMetricNames.RequestDuration, 50 + i, routeTags);
        }

        // Error metrics
        for (int i = 0; i < 5; i++)
        {
            _store.Record(GatewayMetricNames.ErrorsTotal, 1, routeTags);
        }

        // Auth metrics
        for (int i = 0; i < 20; i++)
        {
            _store.Record(GatewayMetricNames.AuthAttempts, 1, routeTags);
        }
        for (int i = 0; i < 2; i++)
        {
            _store.Record(GatewayMetricNames.AuthFailures, 1, routeTags);
        }

        // Cache metrics
        for (int i = 0; i < 80; i++)
        {
            _store.Record(GatewayMetricNames.CacheHits, 1, routeTags);
        }
        for (int i = 0; i < 20; i++)
        {
            _store.Record(GatewayMetricNames.CacheMisses, 1, routeTags);
        }

        // Act
        var stats = await _provider.GetStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.Requests.Total.Should().Be(100);
        stats.Requests.Errors.Should().Be(5);
        stats.Auth.TotalAttempts.Should().Be(20);
        stats.Auth.Failures.Should().Be(2);
        stats.Cache.Hits.Should().Be(80);
        stats.Cache.Misses.Should().Be(20);
        stats.Cache.HitRate.Should().BeApproximately(0.8, 0.01);
    }

    [Test]
    public async Task GetTopRoutesAsync_ShouldReturnTopRoutesByMetric()
    {
        // Arrange
        var route1Tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };
        var route2Tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-2" };
        var route3Tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-3" };

        // Route-1: 100 requests
        for (int i = 0; i < 100; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, route1Tags);
        }

        // Route-2: 50 requests
        for (int i = 0; i < 50; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, route2Tags);
        }

        // Route-3: 25 requests
        for (int i = 0; i < 25; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, route3Tags);
        }

        // Act
        var topRoutes = await _provider.GetTopRoutesAsync(GatewayMetricNames.RequestsTotal, 10);

        // Assert
        topRoutes.Should().NotBeEmpty();
        topRoutes.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task GetRouteMetricsAsync_WithExistingRoute_ShouldReturnRouteMetrics()
    {
        // Arrange
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };

        for (int i = 0; i < 50; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, tags);
            _store.Record(GatewayMetricNames.RequestDuration, 100 + i, tags);
        }

        for (int i = 0; i < 5; i++)
        {
            _store.Record(GatewayMetricNames.ErrorsTotal, 1, tags);
        }

        // Act
        var routeMetrics = await _provider.GetRouteMetricsAsync("route-1");

        // Assert
        routeMetrics.Should().NotBeNull();
        routeMetrics!.RouteId.Should().Be("route-1");
        routeMetrics.TotalRequests.Should().Be(50);
        routeMetrics.Errors.Should().Be(5);
        routeMetrics.ErrorRate.Should().BeApproximately(0.1, 0.01);
    }

    [Test]
    public async Task GetRouteMetricsAsync_WithNonExistentRoute_ShouldReturnNull()
    {
        // Act
        var routeMetrics = await _provider.GetRouteMetricsAsync("non-existent-route");

        // Assert
        routeMetrics.Should().BeNull();
    }

    [Test]
    public async Task QueryAsync_WithMetricName_ShouldReturnFilteredResults()
    {
        // Arrange
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };

        for (int i = 0; i < 10; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, tags);
            _store.Record(GatewayMetricNames.RequestDuration, 100, tags);
        }

        var query = new MetricsQuery { MetricName = GatewayMetricNames.RequestsTotal, BucketSizeSeconds = 60 };

        // Act
        var results = await _provider.QueryAsync(query);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().OnlyContain(ts => ts.MetricName == GatewayMetricNames.RequestsTotal);
    }

    [Test]
    public async Task QueryAsync_WithRouteIdFilter_ShouldFilterByRoute()
    {
        // Arrange
        var route1Tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };
        var route2Tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-2" };

        for (int i = 0; i < 10; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, route1Tags);
        }
        for (int i = 0; i < 5; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, route2Tags);
        }

        var query = new MetricsQuery
        {
            MetricName = GatewayMetricNames.RequestsTotal,
            RouteId = "route-1",
            BucketSizeSeconds = 60,
        };

        // Act
        var results = await _provider.QueryAsync(query);

        // Assert
        results.Should().NotBeEmpty();
    }

    [Test]
    public async Task QueryAsync_WithNoMetricName_ShouldReturnAllMetrics()
    {
        // Arrange
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };

        _store.Record(GatewayMetricNames.RequestsTotal, 1, tags);
        _store.Record(GatewayMetricNames.RequestDuration, 100, tags);
        _store.Record(GatewayMetricNames.CacheHits, 1, tags);

        var query = new MetricsQuery { BucketSizeSeconds = 60 };

        // Act
        var results = await _provider.QueryAsync(query);

        // Assert
        results.Should().HaveCount(3);
    }

    [Test]
    public async Task GetMetricNamesAsync_ShouldReturnAllRecordedMetricNames()
    {
        // Arrange
        _store.Record(GatewayMetricNames.RequestsTotal, 1);
        _store.Record(GatewayMetricNames.RequestDuration, 100);
        _store.Record(GatewayMetricNames.CacheHits, 1);

        // Act
        var names = await _provider.GetMetricNamesAsync();

        // Assert
        names.Should().HaveCount(3);
        names.Should().Contain(GatewayMetricNames.RequestsTotal);
        names.Should().Contain(GatewayMetricNames.RequestDuration);
        names.Should().Contain(GatewayMetricNames.CacheHits);
    }

    [Test]
    public async Task GetStatisticsAsync_WithTimeRange_ShouldRespectTimeRange()
    {
        // Arrange
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };
        var now = DateTimeOffset.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            _store.Record(GatewayMetricNames.RequestsTotal, 1, tags);
        }

        // Act
        var stats = await _provider.GetStatisticsAsync(now.AddMinutes(-30), now.AddMinutes(30));

        // Assert
        stats.Should().NotBeNull();
        stats.StartTime.Should().BeCloseTo(now.AddMinutes(-30), TimeSpan.FromSeconds(1));
        stats.EndTime.Should().BeCloseTo(now.AddMinutes(30), TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task GetSnapshotAsync_CacheHitRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };

        // 80 hits, 20 misses = 80% hit rate
        for (int i = 0; i < 80; i++)
        {
            _store.Record(GatewayMetricNames.CacheHits, 1, tags);
        }
        for (int i = 0; i < 20; i++)
        {
            _store.Record(GatewayMetricNames.CacheMisses, 1, tags);
        }

        // Act
        var snapshot = await _provider.GetSnapshotAsync();

        // Assert
        snapshot.CacheHitRate.Should().BeApproximately(0.8, 0.01);
    }

    [Test]
    public async Task GetSnapshotAsync_RateLimitBlockedRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = "route-1" };

        // 90 allowed, 10 blocked = 10% blocked rate
        for (int i = 0; i < 90; i++)
        {
            _store.Record(GatewayMetricNames.RateLimitAllowed, 1, tags);
        }
        for (int i = 0; i < 10; i++)
        {
            _store.Record(GatewayMetricNames.RateLimitBlocked, 1, tags);
        }

        // Act
        var snapshot = await _provider.GetSnapshotAsync();

        // Assert
        snapshot.RateLimitBlockedRate.Should().BeApproximately(0.1, 0.01);
    }
}
