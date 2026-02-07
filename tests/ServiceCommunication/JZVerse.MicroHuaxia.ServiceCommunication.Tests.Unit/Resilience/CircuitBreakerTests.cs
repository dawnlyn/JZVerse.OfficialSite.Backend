using FluentAssertions;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Tests.Unit.Resilience;

/// <summary>
/// 熔断器单元测试
/// </summary>
[TestFixture]
public class CircuitBreakerTests
{
    private Mock<ILogger<CircuitBreaker>> _loggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<CircuitBreaker>>();
    }

    private CircuitBreaker CreateCircuitBreaker(CircuitBreakerOptions? options = null)
    {
        return new CircuitBreaker(
            "test-breaker",
            options ?? new CircuitBreakerOptions(),
            _loggerMock.Object);
    }

    #region State Tests

    [Test]
    public void InitialState_ShouldBeClosed()
    {
        // Arrange & Act
        var breaker = CreateCircuitBreaker();

        // Assert
        breaker.State.Should().Be(CircuitBreakerState.Closed);
    }

    [Test]
    public void Open_ShouldTransitionToOpenState()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();

        // Act
        breaker.Open();

        // Assert
        breaker.State.Should().Be(CircuitBreakerState.Open);
    }

    [Test]
    public void Close_ShouldTransitionToClosedState()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();
        breaker.Open();

        // Act
        breaker.Close();

        // Assert
        breaker.State.Should().Be(CircuitBreakerState.Closed);
    }

    [Test]
    public void Reset_ShouldResetAllCountersAndClose()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();
        breaker.Open();

        // Act
        breaker.Reset();

        // Assert
        breaker.State.Should().Be(CircuitBreakerState.Closed);
    }

    #endregion

    #region ExecuteAsync Tests

    [Test]
    public async Task ExecuteAsync_ClosedState_ShouldExecuteAction()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();
        var executed = false;

        // Act
        await breaker.ExecuteAsync(async _ =>
        {
            executed = true;
            await Task.CompletedTask;
        });

        // Assert
        executed.Should().BeTrue();
    }

    [Test]
    public async Task ExecuteAsync_OpenState_ShouldThrowBrokenCircuitException()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();
        breaker.Open();

        // Act
        var act = async () => await breaker.ExecuteAsync(_ => Task.FromResult(42));

        // Assert
        await act.Should().ThrowAsync<BrokenCircuitException>();
    }

    [Test]
    public async Task ExecuteAsync_WithReturnValue_ShouldReturnValue()
    {
        // Arrange
        var breaker = CreateCircuitBreaker();

        // Act
        var result = await breaker.ExecuteAsync(_ => Task.FromResult(42));

        // Assert
        result.Should().Be(42);
    }

    [Test]
    public async Task ExecuteAsync_ActionThrows_ShouldRethrow()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            HandledExceptions = [typeof(InvalidOperationException)]
        };
        var breaker = CreateCircuitBreaker(options);

        // Act
        var act = async () => await breaker.ExecuteAsync<int>(_ =>
            throw new InvalidOperationException("Test error"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Failure Threshold Tests

    [Test]
    public async Task ExecuteAsync_ExceedFailureThreshold_ShouldOpenCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            MinimumThroughput = 1,
            HandledExceptions = [typeof(InvalidOperationException)]
        };
        var breaker = CreateCircuitBreaker(options);

        // Act - Cause failures to exceed threshold
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await breaker.ExecuteAsync<int>(_ =>
                    throw new InvalidOperationException("Test failure"));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
            catch (BrokenCircuitException)
            {
                // Circuit is now open
                break;
            }
        }

        // Assert
        breaker.State.Should().Be(CircuitBreakerState.Open);
    }

    [Test]
    public async Task ExecuteAsync_Success_ShouldNotOpenCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            MinimumThroughput = 1
        };
        var breaker = CreateCircuitBreaker(options);

        // Act - Execute successful operations
        for (int i = 0; i < 10; i++)
        {
            await breaker.ExecuteAsync(_ => Task.FromResult(42));
        }

        // Assert
        breaker.State.Should().Be(CircuitBreakerState.Closed);
    }

    #endregion

    #region Half-Open State Tests

    [Test]
    public async Task HalfOpenState_SuccessfulRequest_ShouldClose()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            MinimumThroughput = 1,
            SuccessThreshold = 1,
            BreakDuration = TimeSpan.FromMilliseconds(10),
            HandledExceptions = [typeof(InvalidOperationException)]
        };
        var breaker = CreateCircuitBreaker(options);

        // Cause circuit to open
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await breaker.ExecuteAsync<int>(_ =>
                    throw new InvalidOperationException("Test failure"));
            }
            catch { }
        }

        breaker.State.Should().Be(CircuitBreakerState.Open);

        // Wait for break duration to pass
        await Task.Delay(20);

        // Act - Execute successful request (should transition to HalfOpen then Closed)
        await breaker.ExecuteAsync(_ => Task.FromResult(42));

        // Assert
        breaker.State.Should().Be(CircuitBreakerState.Closed);
    }

    [Test]
    public async Task HalfOpenState_FailedRequest_ShouldReopenCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            MinimumThroughput = 1,
            BreakDuration = TimeSpan.FromMilliseconds(10),
            HandledExceptions = [typeof(InvalidOperationException)]
        };
        var breaker = CreateCircuitBreaker(options);

        // Cause circuit to open
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await breaker.ExecuteAsync<int>(_ =>
                    throw new InvalidOperationException("Test failure"));
            }
            catch { }
        }

        // Wait for break duration to pass
        await Task.Delay(20);

        // Act - Execute failed request in half-open state
        try
        {
            await breaker.ExecuteAsync<int>(_ =>
                throw new InvalidOperationException("Test failure"));
        }
        catch (InvalidOperationException) { }

        // Assert
        breaker.State.Should().Be(CircuitBreakerState.Open);
    }

    #endregion
}
