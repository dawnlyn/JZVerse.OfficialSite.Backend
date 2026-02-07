using FluentAssertions;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Tests.Unit.Resilience;

/// <summary>
/// 指数退避重试策略单元测试
/// </summary>
[TestFixture]
public class ExponentialBackoffRetryPolicyTests
{
    private Mock<ILogger<ExponentialBackoffRetryPolicy>> _loggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<ExponentialBackoffRetryPolicy>>();
    }

    private ExponentialBackoffRetryPolicy CreateRetryPolicy(RetryPolicyOptions? options = null)
    {
        var opts = options ?? new RetryPolicyOptions();
        return new ExponentialBackoffRetryPolicy(
            Options.Create(opts),
            _loggerMock.Object);
    }

    #region Basic Tests

    [Test]
    public void Name_ShouldReturnExponentialBackoff()
    {
        // Arrange
        var policy = CreateRetryPolicy();

        // Assert
        policy.Name.Should().Be("ExponentialBackoff");
    }

    [Test]
    public async Task ExecuteAsync_SuccessfulAction_ShouldReturnImmediately()
    {
        // Arrange
        var policy = CreateRetryPolicy();
        var executionCount = 0;

        // Act
        var result = await policy.ExecuteAsync(_ =>
        {
            executionCount++;
            return Task.FromResult(42);
        });

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(1);
    }

    [Test]
    public async Task ExecuteAsync_NoReturnValue_ShouldComplete()
    {
        // Arrange
        var policy = CreateRetryPolicy();
        var executed = false;

        // Act
        await policy.ExecuteAsync(_ =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        // Assert
        executed.Should().BeTrue();
    }

    #endregion

    #region Retry Tests

    [Test]
    public async Task ExecuteAsync_RetryableException_ShouldRetry()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            RetryableExceptions = [typeof(HttpRequestException)]
        };
        var policy = CreateRetryPolicy(options);
        var executionCount = 0;

        // Act
        var result = await policy.ExecuteAsync(_ =>
        {
            executionCount++;
            if (executionCount < 3)
                throw new HttpRequestException("Transient error");
            return Task.FromResult(42);
        });

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(3);
    }

    [Test]
    public async Task ExecuteAsync_NonRetryableException_ShouldNotRetry()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 3,
            RetryableExceptions = [typeof(HttpRequestException)]
        };
        var policy = CreateRetryPolicy(options);
        var executionCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync<int>(_ =>
        {
            executionCount++;
            throw new ArgumentException("Non-retryable error");
        });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        executionCount.Should().Be(1);
    }

    [Test]
    public async Task ExecuteAsync_MaxRetriesExceeded_ShouldThrow()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            RetryableExceptions = [typeof(HttpRequestException)]
        };
        var policy = CreateRetryPolicy(options);
        var executionCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync<int>(_ =>
        {
            executionCount++;
            throw new HttpRequestException("Persistent error");
        });

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        executionCount.Should().Be(3); // 初始 + 2 次重试
    }

    #endregion

    #region Timeout Exception Tests

    [Test]
    public async Task ExecuteAsync_TimeoutException_ShouldRetry()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromMilliseconds(1)
        };
        var policy = CreateRetryPolicy(options);
        var executionCount = 0;

        // Act
        var result = await policy.ExecuteAsync(_ =>
        {
            executionCount++;
            if (executionCount < 2)
                throw new TimeoutException("Timeout");
            return Task.FromResult(42);
        });

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(2);
    }

    #endregion

    #region Cancellation Tests

    [Test]
    public async Task ExecuteAsync_CancellationRequested_ShouldThrow()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            RetryableExceptions = [typeof(HttpRequestException)]
        };
        var policy = CreateRetryPolicy(options);
        var cts = new CancellationTokenSource();
        var executionCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(_ =>
        {
            executionCount++;
            if (executionCount == 1)
            {
                cts.Cancel();
                throw new HttpRequestException("Error");
            }
            return Task.FromResult(42);
        }, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region HTTP Status Code Tests

    [Test]
    public async Task ExecuteAsync_RetryableStatusCode_ShouldRetry()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            RetryableStatusCodes = [503, 502]
        };
        var policy = CreateRetryPolicy(options);
        var executionCount = 0;

        // Act
        var result = await policy.ExecuteAsync(_ =>
        {
            executionCount++;
            if (executionCount < 2)
                throw new HttpRequestException("Service Unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);
            return Task.FromResult(42);
        });

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(2);
    }

    [Test]
    public async Task ExecuteAsync_NonRetryableStatusCode_ShouldNotRetry()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 3,
            RetryableExceptions = new HashSet<Type>(), // 清空默认的可重试异常，只基于状态码判断
            RetryableStatusCodes = [503, 502]
        };
        var policy = CreateRetryPolicy(options);
        var executionCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync<int>(_ =>
        {
            executionCount++;
            throw new HttpRequestException("Not Found", null, System.Net.HttpStatusCode.NotFound);
        });

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        executionCount.Should().Be(1);
    }

    #endregion

    #region Delay Configuration Tests

    [Test]
    public async Task ExecuteAsync_ShouldRespectMaxDelay()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 5,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromMilliseconds(200),
            BackoffMultiplier = 10.0,
            UseJitter = false,
            RetryableExceptions = [typeof(HttpRequestException)]
        };
        var policy = CreateRetryPolicy(options);
        var executionCount = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        try
        {
            await policy.ExecuteAsync<int>(_ =>
            {
                executionCount++;
                throw new HttpRequestException("Error");
            });
        }
        catch (HttpRequestException) { }

        stopwatch.Stop();

        // Assert
        executionCount.Should().Be(6); // 1 初始 + 5 重试
        // 总延迟应该被 MaxDelay 限制
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
    }

    #endregion
}
