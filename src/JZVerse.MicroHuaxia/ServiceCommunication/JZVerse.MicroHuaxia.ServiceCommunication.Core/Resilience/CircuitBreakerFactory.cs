using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;

/// <summary>
/// 熔断器工厂实现
/// </summary>
public sealed class CircuitBreakerFactory(
    IOptions<CircuitBreakerOptions> defaultOptions,
    ILoggerFactory loggerFactory) : ICircuitBreakerFactory
{
    private readonly ConcurrentDictionary<string, ICircuitBreaker> _circuitBreakers = new();

    public ICircuitBreaker GetOrCreate(string name, CircuitBreakerOptions? options = null)
    {
        return _circuitBreakers.GetOrAdd(name, n =>
        {
            var opts = options ?? defaultOptions.Value;
            var logger = loggerFactory.CreateLogger<CircuitBreaker>();
            return new CircuitBreaker(n, opts, logger);
        });
    }
}
