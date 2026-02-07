using System.Diagnostics;
using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Tracing.Propagation;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Tracing.Propagation;

[TestFixture]
public sealed class W3CTracePropagatorTests
{
    private W3CTracePropagator _propagator = null!;

    [SetUp]
    public void Setup()
    {
        _propagator = new W3CTracePropagator();
    }

    [Test]
    public void Name_ShouldBeW3C()
    {
        _propagator.Name.Should().Be("W3C");
    }

    [Test]
    public void Inject_WithValidActivity_ShouldSetTraceparentHeader()
    {
        // Arrange
        using var activity = new Activity("test");
        activity.Start();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        _propagator.Inject(activity, request);

        // Assert
        request.Headers.Contains(W3CTracePropagator.TraceParentHeader).Should().BeTrue();

        var traceparent = request.Headers.GetValues(W3CTracePropagator.TraceParentHeader).First();
        traceparent.Should().StartWith("00-");
        traceparent.Should().Contain(activity.TraceId.ToHexString());
        traceparent.Should().Contain(activity.SpanId.ToHexString());
    }

    [Test]
    public void Inject_WithNullActivity_ShouldNotThrow()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act & Assert
        _propagator.Invoking(p => p.Inject(null, request)).Should().NotThrow();
        request.Headers.Contains(W3CTracePropagator.TraceParentHeader).Should().BeFalse();
    }

    [Test]
    public void Extract_WithValidTraceparent_ShouldReturnValidContext()
    {
        // Arrange
        var traceId = ActivityTraceId.CreateRandom().ToHexString();
        var spanId = ActivitySpanId.CreateRandom().ToHexString();
        var traceparent = $"00-{traceId}-{spanId}-01";

        var headers = new HeaderDictionary { { W3CTracePropagator.TraceParentHeader, traceparent } };

        // Act
        var context = _propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeTrue();
        context.TraceId.ToHexString().Should().Be(traceId);
        context.SpanId.ToHexString().Should().Be(spanId);
        context.TraceFlags.Should().Be(ActivityTraceFlags.Recorded);
    }

    [Test]
    public void Extract_WithTracestate_ShouldIncludeTracestate()
    {
        // Arrange
        var traceId = ActivityTraceId.CreateRandom().ToHexString();
        var spanId = ActivitySpanId.CreateRandom().ToHexString();
        var traceparent = $"00-{traceId}-{spanId}-00";
        var tracestate = "vendor=value";

        var headers = new HeaderDictionary
        {
            { W3CTracePropagator.TraceParentHeader, traceparent },
            { W3CTracePropagator.TraceStateHeader, tracestate },
        };

        // Act
        var context = _propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeTrue();
        context.TraceState.Should().Be(tracestate);
    }

    [Test]
    public void Extract_WithMissingTraceparent_ShouldReturnInvalidContext()
    {
        // Arrange
        var headers = new HeaderDictionary();

        // Act
        var context = _propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeFalse();
    }

    [Test]
    public void Extract_WithInvalidTraceparent_ShouldReturnInvalidContext()
    {
        // Arrange
        var headers = new HeaderDictionary { { W3CTracePropagator.TraceParentHeader, "invalid" } };

        // Act
        var context = _propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeFalse();
    }

    [Test]
    public void Extract_WithZeroTraceId_ShouldReturnInvalidContext()
    {
        // Arrange
        var spanId = ActivitySpanId.CreateRandom().ToHexString();
        var traceparent = $"00-00000000000000000000000000000000-{spanId}-00";

        var headers = new HeaderDictionary { { W3CTracePropagator.TraceParentHeader, traceparent } };

        // Act
        var context = _propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeFalse();
    }

    [Test]
    public void Extract_WithZeroSpanId_ShouldReturnInvalidContext()
    {
        // Arrange
        var traceId = ActivityTraceId.CreateRandom().ToHexString();
        var traceparent = $"00-{traceId}-0000000000000000-00";

        var headers = new HeaderDictionary { { W3CTracePropagator.TraceParentHeader, traceparent } };

        // Act
        var context = _propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeFalse();
    }
}

[TestFixture]
public sealed class B3TracePropagatorTests
{
    [Test]
    public void Name_SingleFormat_ShouldBeB3Single()
    {
        var propagator = new B3TracePropagator(B3Format.Single);
        propagator.Name.Should().Be("B3-Single");
    }

    [Test]
    public void Name_MultiFormat_ShouldBeB3Multi()
    {
        var propagator = new B3TracePropagator(B3Format.Multi);
        propagator.Name.Should().Be("B3-Multi");
    }

    [Test]
    public void Inject_SingleFormat_ShouldSetB3Header()
    {
        // Arrange
        var propagator = new B3TracePropagator(B3Format.Single);
        using var activity = new Activity("test");
        activity.Start();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        propagator.Inject(activity, request);

        // Assert
        request.Headers.Contains(B3TracePropagator.B3Header).Should().BeTrue();

        var b3 = request.Headers.GetValues(B3TracePropagator.B3Header).First();
        b3.Should().Contain(activity.TraceId.ToHexString());
        b3.Should().Contain(activity.SpanId.ToHexString());
    }

    [Test]
    public void Inject_MultiFormat_ShouldSetMultipleHeaders()
    {
        // Arrange
        var propagator = new B3TracePropagator(B3Format.Multi);
        using var activity = new Activity("test");
        activity.Start();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        propagator.Inject(activity, request);

        // Assert
        request.Headers.Contains(B3TracePropagator.TraceIdHeader).Should().BeTrue();
        request.Headers.Contains(B3TracePropagator.SpanIdHeader).Should().BeTrue();
        request.Headers.Contains(B3TracePropagator.SampledHeader).Should().BeTrue();
    }

    [Test]
    public void Extract_SingleFormat_ShouldReturnValidContext()
    {
        // Arrange
        var propagator = new B3TracePropagator(B3Format.Single);
        var traceId = ActivityTraceId.CreateRandom().ToHexString();
        var spanId = ActivitySpanId.CreateRandom().ToHexString();
        var b3 = $"{traceId}-{spanId}-1";

        var headers = new HeaderDictionary { { B3TracePropagator.B3Header, b3 } };

        // Act
        var context = propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeTrue();
        context.TraceId.ToHexString().Should().Be(traceId);
        context.SpanId.ToHexString().Should().Be(spanId);
        context.TraceFlags.Should().Be(ActivityTraceFlags.Recorded);
    }

    [Test]
    public void Extract_MultiFormat_ShouldReturnValidContext()
    {
        // Arrange
        var propagator = new B3TracePropagator(B3Format.Multi);
        var traceId = ActivityTraceId.CreateRandom().ToHexString();
        var spanId = ActivitySpanId.CreateRandom().ToHexString();

        var headers = new HeaderDictionary
        {
            { B3TracePropagator.TraceIdHeader, traceId },
            { B3TracePropagator.SpanIdHeader, spanId },
            { B3TracePropagator.SampledHeader, "1" },
        };

        // Act
        var context = propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeTrue();
        context.TraceId.ToHexString().Should().Be(traceId);
        context.SpanId.ToHexString().Should().Be(spanId);
    }

    [Test]
    public void Extract_Short16CharTraceId_ShouldPadAndReturnValidContext()
    {
        // Arrange
        var propagator = new B3TracePropagator(B3Format.Single);
        var shortTraceId = "1234567890abcdef"; // 16 chars
        var spanId = ActivitySpanId.CreateRandom().ToHexString();
        var b3 = $"{shortTraceId}-{spanId}-1";

        var headers = new HeaderDictionary { { B3TracePropagator.B3Header, b3 } };

        // Act
        var context = propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeTrue();
        context.TraceId.ToHexString().Should().EndWith(shortTraceId);
    }

    [Test]
    public void Extract_B3Zero_ShouldReturnInvalidContext()
    {
        // Arrange
        var propagator = new B3TracePropagator(B3Format.Single);
        var headers = new HeaderDictionary { { B3TracePropagator.B3Header, "0" } };

        // Act
        var context = propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeFalse();
    }
}

[TestFixture]
public sealed class CompositeTracePropagatorTests
{
    [Test]
    public void Name_ShouldBeComposite()
    {
        var propagator = CompositeTracePropagator.CreateDefault();
        propagator.Name.Should().Be("Composite");
    }

    [Test]
    public void CreateDefault_ShouldNotThrow()
    {
        var act = () => CompositeTracePropagator.CreateDefault();
        act.Should().NotThrow();
    }

    [Test]
    public void Constructor_WithEmptyPropagators_ShouldThrow()
    {
        var act = () => new CompositeTracePropagator([]);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Inject_ShouldInjectAllFormats()
    {
        // Arrange
        var propagator = CompositeTracePropagator.CreateDefault();
        using var activity = new Activity("test");
        activity.Start();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        propagator.Inject(activity, request);

        // Assert
        request.Headers.Contains(W3CTracePropagator.TraceParentHeader).Should().BeTrue();
        request.Headers.Contains(B3TracePropagator.B3Header).Should().BeTrue();
    }

    [Test]
    public void Extract_ShouldPreferW3CFormat()
    {
        // Arrange
        var propagator = CompositeTracePropagator.CreateDefault();
        var w3cTraceId = ActivityTraceId.CreateRandom().ToHexString();
        var w3cSpanId = ActivitySpanId.CreateRandom().ToHexString();
        var b3TraceId = ActivityTraceId.CreateRandom().ToHexString();
        var b3SpanId = ActivitySpanId.CreateRandom().ToHexString();

        var headers = new HeaderDictionary
        {
            { W3CTracePropagator.TraceParentHeader, $"00-{w3cTraceId}-{w3cSpanId}-01" },
            { B3TracePropagator.B3Header, $"{b3TraceId}-{b3SpanId}-1" },
        };

        // Act
        var context = propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeTrue();
        context.TraceId.ToHexString().Should().Be(w3cTraceId);
    }

    [Test]
    public void Extract_WhenW3CMissing_ShouldFallbackToB3()
    {
        // Arrange
        var propagator = CompositeTracePropagator.CreateDefault();
        var b3TraceId = ActivityTraceId.CreateRandom().ToHexString();
        var b3SpanId = ActivitySpanId.CreateRandom().ToHexString();

        var headers = new HeaderDictionary { { B3TracePropagator.B3Header, $"{b3TraceId}-{b3SpanId}-1" } };

        // Act
        var context = propagator.Extract(headers);

        // Assert
        context.IsValid.Should().BeTrue();
        context.TraceId.ToHexString().Should().Be(b3TraceId);
    }
}
