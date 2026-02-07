using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Configuration;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;

/// <summary>
/// 弹性管道工厂实现
/// </summary>
public sealed class ResiliencePipelineFactory(
    IOptions<ServiceCommunicationOptions> options,
    ICircuitBreakerFactory circuitBreakerFactory,
    IOptions<RetryPolicyOptions> retryOptions,
    ILoggerFactory loggerFactory) : IResiliencePipelineFactory
{
    private readonly ConcurrentDictionary<string, IResiliencePipeline> _pipelines = new();
    private readonly ServiceCommunicationOptions _options = options.Value;

    public IResiliencePipeline GetOrCreate(string serviceName)
    {
        return _pipelines.GetOrAdd(serviceName, name =>
        {
            var serviceOpts = _options.ServiceEndpoints.GetValueOrDefault(name);
            var circuitBreakerOpts = serviceOpts?.CircuitBreaker ?? _options.DefaultCircuitBreaker;
            var retryOpts = serviceOpts?.Retry ?? retryOptions.Value;
            var timeout = serviceOpts?.Timeout ?? _options.DefaultTimeout;

            var circuitBreaker = circuitBreakerFactory.GetOrCreate(name, circuitBreakerOpts);
            var retryPolicy = new ExponentialBackoffRetryPolicy(
                Options.Create(retryOpts),
                loggerFactory.CreateLogger<ExponentialBackoffRetryPolicy>());

            return new ResiliencePipelineBuilder()
                .WithRetryPolicy(retryPolicy)
                .WithCircuitBreaker(circuitBreaker)
                .WithTimeout(timeout)
                .WithLogger(loggerFactory.CreateLogger<ResiliencePipeline>())
                .Build();
        });
    }
}
