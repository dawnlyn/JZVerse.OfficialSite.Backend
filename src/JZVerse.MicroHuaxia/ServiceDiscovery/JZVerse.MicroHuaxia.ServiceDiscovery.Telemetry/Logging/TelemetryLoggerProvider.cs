using System.Diagnostics;
using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Logging;

/// <summary>
/// 可观测性日志提供者
/// </summary>
[ProviderAlias("ServiceDiscoveryTelemetry")]
public sealed class TelemetryLoggerProvider(InMemoryLogStore _logStore, IOptions<TelemetryOptions> options)
    : ILoggerProvider,
        ISupportExternalScope
{
    private readonly TelemetryOptions _options = options.Value;
    private IExternalScopeProvider? _scopeProvider;

    public ILogger CreateLogger(string categoryName) =>
        new TelemetryLogger(categoryName, _logStore, _options, _scopeProvider);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public void Dispose()
    {
        // 清理资源
    }
}

internal sealed class TelemetryLogger(
    string _categoryName,
    InMemoryLogStore _logStore,
    TelemetryOptions _options,
    IExternalScopeProvider? _scopeProvider
) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => _scopeProvider?.Push(state);

    public bool IsEnabled(LogLevel logLevel) =>
        _options.Logging.Enabled
        && logLevel != LogLevel.None
        && logLevel >= ParseLogLevel(_options.Logging.MinimumLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var activity = Activity.Current;
        var scopes = new List<Dictionary<string, object?>>();

        // 收集 scope 信息
        if (_options.Logging.IncludeScopes && _scopeProvider != null)
        {
            _scopeProvider.ForEachScope(
                (scope, list) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> scopeItems)
                    {
                        var scopeDict = new Dictionary<string, object?>();
                        foreach (var item in scopeItems)
                        {
                            scopeDict[item.Key] = item.Value;
                        }
                        list.Add(scopeDict);
                    }
                    else if (scope != null)
                    {
                        list.Add(new() { ["Scope"] = scope.ToString() });
                    }
                },
                scopes
            );
        }

        // 提取结构化属性
        var properties = new Dictionary<string, object?>();
        if (state is IEnumerable<KeyValuePair<string, object?>> stateItems)
        {
            foreach (var item in stateItems)
            {
                if (item.Key != "{OriginalFormat}")
                {
                    properties[item.Key] = item.Value;
                }
            }
        }

        // 尝试从 scope 或属性中提取服务信息
        string? serviceName = null;
        string? instanceId = null;

        if (properties.TryGetValue("ServiceName", out var sn))
        {
            serviceName = sn?.ToString();
        }
        if (properties.TryGetValue("InstanceId", out var iid))
        {
            instanceId = iid?.ToString();
        }

        var entry = new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = logLevel,
            Category = _categoryName,
            EventId = eventId.Id,
            Message = formatter(state, exception),
            Exception =
                _options.Logging.IncludeStackTrace && exception != null ? exception.ToString() : exception?.Message,
            TraceId = activity?.TraceId.ToString(),
            SpanId = activity?.SpanId.ToString(),
            ServiceName = serviceName,
            ServiceInstanceId = instanceId,
            Properties = properties,
            Scopes = scopes,
        };

        _logStore.Add(entry);
    }

    private static LogLevel ParseLogLevel(string level) =>
        level.ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" or "info" => LogLevel.Information,
            "warning" or "warn" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            _ => LogLevel.Information,
        };
}
