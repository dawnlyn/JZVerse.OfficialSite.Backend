using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Routing;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Routing;

/// <summary>
/// 交换机实现
/// </summary>
public sealed class Exchange : IExchange
{
    private readonly ILogger _logger;
    private readonly IRoutingEngine _routingEngine;
    private readonly ConcurrentDictionary<string, QueueBinding> _bindings = new();

    public string Name { get; }
    public ExchangeType Type { get; }
    public bool Durable { get; }

    public Exchange(
        string name,
        ExchangeType type,
        bool durable,
        IRoutingEngine routingEngine,
        ILogger logger)
    {
        Name = name;
        Type = type;
        Durable = durable;
        _routingEngine = routingEngine;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task BindQueueAsync(
        string queueName,
        string routingKey,
        IDictionary<string, string>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var binding = new QueueBinding
        {
            QueueName = queueName,
            RoutingKey = routingKey,
            Arguments = arguments ?? new Dictionary<string, string>()
        };

        var key = $"{queueName}:{routingKey}";
        _bindings[key] = binding;

        _logger.LogDebug("Queue {QueueName} bound to exchange {ExchangeName} with routing key {RoutingKey}",
            queueName, Name, routingKey);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnbindQueueAsync(
        string queueName,
        string routingKey,
        CancellationToken cancellationToken = default)
    {
        var key = $"{queueName}:{routingKey}";
        _bindings.TryRemove(key, out _);

        _logger.LogDebug("Queue {QueueName} unbound from exchange {ExchangeName} with routing key {RoutingKey}",
            queueName, Name, routingKey);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        IMessage message,
        string routingKey,
        CancellationToken cancellationToken = default)
    {
        var matchedQueues = GetMatchedQueues(routingKey, message);

        foreach (var queueName in matchedQueues)
        {
            await _routingEngine.RouteToQueueAsync(queueName, message, cancellationToken);
        }

        _logger.LogDebug("Message {MessageId} published to {QueueCount} queues via exchange {ExchangeName}",
            message.MessageId, matchedQueues.Count, Name);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<QueueBinding>> GetBindingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<QueueBinding>>(_bindings.Values.ToList());
    }

    private List<string> GetMatchedQueues(string routingKey, IMessage message)
    {
        var matchedQueues = new List<string>();

        foreach (var binding in _bindings.Values)
        {
            var isMatch = Type switch
            {
                ExchangeType.Direct => MatchDirect(binding.RoutingKey, routingKey),
                ExchangeType.Topic => MatchTopic(binding.RoutingKey, routingKey),
                ExchangeType.Fanout => true,
                ExchangeType.Headers => MatchHeaders(binding.Arguments, message.Headers),
                _ => false
            };

            if (isMatch)
            {
                matchedQueues.Add(binding.QueueName);
            }
        }

        return matchedQueues;
    }

    private static bool MatchDirect(string bindingKey, string routingKey)
    {
        return string.Equals(bindingKey, routingKey, StringComparison.Ordinal);
    }

    private static bool MatchTopic(string bindingKey, string routingKey)
    {
        // 支持通配符: * (匹配一个词) 和 # (匹配零个或多个词)
        // 例如: "order.*" 匹配 "order.created", "order.#" 匹配 "order.created.v2"
        var pattern = "^" + Regex.Escape(bindingKey)
            .Replace("\\*", "[^.]+")
            .Replace("\\#", ".*") + "$";

        return Regex.IsMatch(routingKey, pattern);
    }

    private static bool MatchHeaders(IDictionary<string, string> bindingHeaders, IDictionary<string, string> messageHeaders)
    {
        if (bindingHeaders.Count == 0)
        {
            return true;
        }

        // x-match: all (所有头都匹配) 或 any (任一头匹配)
        var matchAll = !bindingHeaders.TryGetValue("x-match", out var matchMode) ||
                       matchMode.Equals("all", StringComparison.OrdinalIgnoreCase);

        var matchCount = 0;
        foreach (var (key, value) in bindingHeaders)
        {
            if (key == "x-match") continue;

            if (messageHeaders.TryGetValue(key, out var msgValue) &&
                string.Equals(value, msgValue, StringComparison.Ordinal))
            {
                matchCount++;
                if (!matchAll) return true; // any 模式下，一个匹配即可
            }
        }

        // all 模式下，需要所有非 x-match 的头都匹配
        var requiredCount = bindingHeaders.ContainsKey("x-match") ? bindingHeaders.Count - 1 : bindingHeaders.Count;
        return matchAll && matchCount == requiredCount;
    }
}
