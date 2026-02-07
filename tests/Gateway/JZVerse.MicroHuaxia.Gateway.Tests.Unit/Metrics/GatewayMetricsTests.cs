using System.Diagnostics.Metrics;
using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Metrics;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Metrics;

[TestFixture]
public sealed class GatewayMetricsTests
{
    private MeterListener _listener = null!;
    private GatewayMetrics _metrics = null!;
    private List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> _recordedMetrics = null!;

    [SetUp]
    public void Setup()
    {
        _recordedMetrics = [];

        var meterFactory = new TestMeterFactory();
        _metrics = new GatewayMetrics(meterFactory);

        // Setup meter listener to capture metrics
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == GatewayMetricNames.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            _recordedMetrics.Add((instrument.Name, measurement, tags.ToArray()));
        });

        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            _recordedMetrics.Add((instrument.Name, measurement, tags.ToArray()));
        });

        _listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            _recordedMetrics.Add((instrument.Name, measurement, tags.ToArray()));
        });

        _listener.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _listener.Dispose();
    }

    [Test]
    public void RecordRequest_ShouldRecordRequestMetrics()
    {
        // Act
        _metrics.RecordRequest("route-1", "GET", 200, 150.5, 1024, 2048);

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.RequestsTotal);
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.RequestDuration);
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.RequestSize);
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.ResponseSize);
    }

    [Test]
    public void RecordRequest_With5xxStatus_ShouldRecordError()
    {
        // Act
        _metrics.RecordRequest("route-1", "GET", 500, 150.5);

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.ErrorsTotal);
    }

    [Test]
    public void RecordRequest_With4xxStatus_ShouldRecordError()
    {
        // Act
        _metrics.RecordRequest("route-1", "GET", 404, 50.0);

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.ErrorsTotal);
    }

    [Test]
    public void RecordRequest_With2xxStatus_ShouldNotRecordError()
    {
        // Act
        _metrics.RecordRequest("route-1", "GET", 200, 50.0);

        // Assert
        _recordedMetrics.Should().NotContain(m => m.Name == GatewayMetricNames.ErrorsTotal);
    }

    [Test]
    public void RecordAuthentication_Success_ShouldRecordAuthMetrics()
    {
        // Act
        _metrics.RecordAuthentication("route-1", "jwt", true, 25.5);

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.AuthAttempts);
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.AuthDuration);
        _recordedMetrics.Should().NotContain(m => m.Name == GatewayMetricNames.AuthFailures);
    }

    [Test]
    public void RecordAuthentication_Failure_ShouldRecordAuthFailure()
    {
        // Act
        _metrics.RecordAuthentication("route-1", "jwt", false, 10.0);

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.AuthAttempts);
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.AuthFailures);
    }

    [Test]
    public void RecordRateLimit_Allowed_ShouldRecordAllowed()
    {
        // Act
        _metrics.RecordRateLimit("route-1", "sliding_window", "ip", true, 100);

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.RateLimitAllowed);
        _recordedMetrics.Should().NotContain(m => m.Name == GatewayMetricNames.RateLimitBlocked);
    }

    [Test]
    public void RecordRateLimit_Blocked_ShouldRecordBlocked()
    {
        // Act
        _metrics.RecordRateLimit("route-1", "token_bucket", "ip", false, 0);

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.RateLimitBlocked);
        _recordedMetrics.Should().NotContain(m => m.Name == GatewayMetricNames.RateLimitAllowed);
    }

    [Test]
    public void RecordCacheAccess_Hit_ShouldRecordCacheHit()
    {
        // Act
        _metrics.RecordCacheAccess("route-1", true);

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.CacheHits);
        _recordedMetrics.Should().NotContain(m => m.Name == GatewayMetricNames.CacheMisses);
    }

    [Test]
    public void RecordCacheAccess_Miss_ShouldRecordCacheMiss()
    {
        // Act
        _metrics.RecordCacheAccess("route-1", false);

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.CacheMisses);
        _recordedMetrics.Should().NotContain(m => m.Name == GatewayMetricNames.CacheHits);
    }

    [Test]
    public void RecordCacheEviction_ShouldRecordEviction()
    {
        // Act
        _metrics.RecordCacheEviction("route-1");

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.CacheEvictions);
    }

    [Test]
    public void RecordForward_Success_ShouldRecordForwardMetrics()
    {
        // Act
        _metrics.RecordForward("route-1", "http", "localhost:8080", true, 50.0);

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.ForwardAttempts);
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.ForwardDuration);
        _recordedMetrics.Should().NotContain(m => m.Name == GatewayMetricNames.ForwardFailures);
    }

    [Test]
    public void RecordForward_Failure_ShouldRecordForwardFailure()
    {
        // Act
        _metrics.RecordForward("route-1", "http", "localhost:8080", false, 1000.0);

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.ForwardAttempts);
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.ForwardFailures);
    }

    [Test]
    public void RecordRetry_ShouldRecordRetryAttempt()
    {
        // Act
        _metrics.RecordRetry("route-1", 2, "connection_timeout");

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.RetryAttempts);
    }

    [Test]
    public void RecordCircuitBreakerTrip_ShouldRecordTrip()
    {
        // Act
        _metrics.RecordCircuitBreakerTrip("cb-service-1", "open");

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.CircuitBreakerTrips);
    }

    [Test]
    public void RecordBulkheadRejection_ShouldRecordRejection()
    {
        // Act
        _metrics.RecordBulkheadRejection("bulkhead-1");

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.BulkheadRejected);
    }

    [Test]
    public void RecordFallback_ShouldRecordFallbackExecution()
    {
        // Act
        _metrics.RecordFallback("route-1", "static", "circuit_breaker_open");

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.FallbackExecutions);
    }

    [Test]
    public void RecordTimeout_ShouldRecordTimeout()
    {
        // Act
        _metrics.RecordTimeout("route-1", "backend-service");

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.TimeoutsTotal);
    }

    [Test]
    public void SetActiveRequestsProvider_ShouldProvideActiveRequestsValue()
    {
        // Arrange
        _metrics.SetActiveRequestsProvider(() => 42);

        // Act - Trigger observable gauge collection
        _listener.RecordObservableInstruments();

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.ActiveRequests && (int)m.Value == 42);
    }

    [Test]
    public void SetCacheStatsProvider_ShouldProvideCacheSize()
    {
        // Arrange
        _metrics.SetCacheStatsProvider(() => (1024, 50));

        // Act - Trigger observable gauge collection
        _listener.RecordObservableInstruments();

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.CacheSize && (int)m.Value == 50);
    }

    [Test]
    public void SetCircuitBreakerStatsProvider_ShouldProvideCircuitBreakerStates()
    {
        // Arrange
        _metrics.SetCircuitBreakerStatsProvider(() => new Dictionary<string, int>
        {
            ["cb-service-1"] = 0, // Closed
            ["cb-service-2"] = 1, // Open
        });

        // Act - Trigger observable gauge collection
        _listener.RecordObservableInstruments();

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.CircuitBreakerState);
    }

    [Test]
    public void SetBulkheadStatsProvider_ShouldProvideBulkheadStats()
    {
        // Arrange
        _metrics.SetBulkheadStatsProvider(() => new Dictionary<string, (int concurrent, int queue)>
        {
            ["bulkhead-1"] = (10, 5),
        });

        // Act - Trigger observable gauge collection
        _listener.RecordObservableInstruments();

        // Assert
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.BulkheadConcurrent);
        _recordedMetrics.Should().Contain(m => m.Name == GatewayMetricNames.BulkheadQueueLength);
    }

    [Test]
    public void RecordRequest_WithNullRouteId_ShouldUseUnknown()
    {
        // Act
        _metrics.RecordRequest(null, "GET", 200, 100.0);

        // Assert
        var requestMetric = _recordedMetrics.FirstOrDefault(m => m.Name == GatewayMetricNames.RequestsTotal);
        requestMetric.Should().NotBe(default);
        var routeIdTag = requestMetric.Tags.FirstOrDefault(t => t.Key == GatewayMetricTags.RouteId);
        routeIdTag.Value.Should().NotBeNull();
        routeIdTag.Value!.ToString().Should().Be("unknown");
    }
}

/// <summary>
/// Test meter factory for unit testing
/// </summary>
internal sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(options.Name, options.Version);
        _meters.Add(meter);
        return meter;
    }

    public void Dispose()
    {
        foreach (var meter in _meters)
        {
            meter.Dispose();
        }
        _meters.Clear();
    }
}
