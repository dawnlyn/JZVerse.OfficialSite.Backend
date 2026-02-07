using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Routing;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Routing;

/// <summary>
/// 交换机管理器实现
/// </summary>
public sealed class ExchangeManager : IExchangeManager
{
    private readonly ILogger<ExchangeManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IRoutingEngine _routingEngine;
    private readonly ConcurrentDictionary<string, IExchange> _exchanges = new();

    public ExchangeManager(
        ILogger<ExchangeManager> logger,
        ILoggerFactory loggerFactory,
        IRoutingEngine routingEngine)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _routingEngine = routingEngine;

        // 创建默认交换机
        InitializeDefaultExchanges();
    }

    private void InitializeDefaultExchanges()
    {
        // 默认 Direct 交换机
        _exchanges[""] = new Exchange(
            "",
            ExchangeType.Direct,
            true,
            _routingEngine,
            _loggerFactory.CreateLogger<Exchange>());

        // 默认 Fanout 交换机
        _exchanges["amq.fanout"] = new Exchange(
            "amq.fanout",
            ExchangeType.Fanout,
            true,
            _routingEngine,
            _loggerFactory.CreateLogger<Exchange>());

        // 默认 Topic 交换机
        _exchanges["amq.topic"] = new Exchange(
            "amq.topic",
            ExchangeType.Topic,
            true,
            _routingEngine,
            _loggerFactory.CreateLogger<Exchange>());

        // 默认 Headers 交换机
        _exchanges["amq.headers"] = new Exchange(
            "amq.headers",
            ExchangeType.Headers,
            true,
            _routingEngine,
            _loggerFactory.CreateLogger<Exchange>());

        _logger.LogInformation("Default exchanges initialized");
    }

    /// <inheritdoc />
    public Task<IExchange> DeclareExchangeAsync(
        string name,
        ExchangeType type,
        bool durable = true,
        CancellationToken cancellationToken = default)
    {
        var exchange = _exchanges.GetOrAdd(name, _ =>
        {
            var newExchange = new Exchange(
                name,
                type,
                durable,
                _routingEngine,
                _loggerFactory.CreateLogger<Exchange>());

            _logger.LogInformation("Exchange {ExchangeName} declared with type {Type}",
                name, type);

            return newExchange;
        });

        return Task.FromResult(exchange);
    }

    /// <inheritdoc />
    public Task<IExchange?> GetExchangeAsync(string name, CancellationToken cancellationToken = default)
    {
        _exchanges.TryGetValue(name, out var exchange);
        return Task.FromResult(exchange);
    }

    /// <inheritdoc />
    public Task DeleteExchangeAsync(string name, CancellationToken cancellationToken = default)
    {
        // 不允许删除默认交换机
        if (string.IsNullOrEmpty(name) || name.StartsWith("amq."))
        {
            _logger.LogWarning("Cannot delete default exchange {ExchangeName}", name);
            return Task.CompletedTask;
        }

        if (_exchanges.TryRemove(name, out _))
        {
            _logger.LogInformation("Exchange {ExchangeName} deleted", name);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IExchange>> GetAllExchangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<IExchange>>(_exchanges.Values.ToList());
    }
}
