using System.Diagnostics;
using System.Text;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Providers;

/// <summary>
/// Gateway 日志记录器
/// </summary>
public sealed class GatewayLogger : ILogger
{
    private readonly string _categoryName;
    private readonly GatewayLoggerProvider _provider;
    private readonly ILogStore _logStore;
    private readonly GatewayLoggingOptions _options;
    private readonly Func<string, LogLevel, bool>? _filter;

    internal IExternalScopeProvider? ScopeProvider { get; set; }

    /// <summary>
    /// 创建 Gateway 日志记录器
    /// </summary>
    internal GatewayLogger(
        string categoryName,
        GatewayLoggerProvider provider,
        ILogStore logStore,
        GatewayLoggingOptions options,
        Func<string, LogLevel, bool>? filter)
    {
        _categoryName = categoryName;
        _provider = provider;
        _logStore = logStore;
        _options = options;
        _filter = filter;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel == LogLevel.None)
            return false;

        return _filter?.Invoke(_categoryName, logLevel) ?? true;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return ScopeProvider?.Push(state);
    }

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);

        // 收集 Scope 信息
        var scopes = CollectScopes();

        // 收集结构化属性
        var properties = CollectProperties(state);

        // 从 Activity.Current 提取追踪上下文
        string? traceId = null;
        string? spanId = null;
        string? parentSpanId = null;

        if (_options.EnrichWithTraceContext && Activity.Current is not null)
        {
            traceId = Activity.Current.TraceId.ToString();
            spanId = Activity.Current.SpanId.ToString();
            parentSpanId = Activity.Current.ParentSpanId.ToString();
        }

        // 构建日志条目
        var entry = new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = logLevel,
            Category = _categoryName,
            EventId = eventId.Id,
            EventName = eventId.Name,
            Message = message,
            Exception = FormatException(exception),
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            ServiceName = _provider.ServiceName ?? ExtractServiceName(_categoryName),
            ServiceInstanceId = _provider.ServiceInstanceId,
            HostName = _provider.HostName,
            Environment = _provider.Environment,
            Properties = properties,
            Scopes = scopes,
            Source = DetermineSource(_categoryName)
        };

        // 异步写入日志存储
        _ = _logStore.AddAsync(entry);
    }

    private IReadOnlyList<IReadOnlyDictionary<string, object?>>? CollectScopes()
    {
        if (!_options.IncludeScopes || ScopeProvider is null)
            return null;

        var scopes = new List<IReadOnlyDictionary<string, object?>>();

        ScopeProvider.ForEachScope((scope, state) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> kvps)
            {
                state.Add(new Dictionary<string, object?>(kvps));
            }
            else if (scope is not null)
            {
                state.Add(new Dictionary<string, object?> { ["Scope"] = scope.ToString() });
            }
        }, scopes);

        return scopes.Count > 0 ? scopes : null;
    }

    private static IReadOnlyDictionary<string, object?>? CollectProperties<TState>(TState state)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> kvps)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var kvp in kvps)
            {
                // 排除 {OriginalFormat} 键
                if (kvp.Key != "{OriginalFormat}")
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }
            return dict.Count > 0 ? dict : null;
        }

        return null;
    }

    private string? FormatException(Exception? exception)
    {
        if (exception is null)
            return null;

        if (!_options.IncludeStackTrace)
            return $"{exception.GetType().FullName}: {exception.Message}";

        var sb = new StringBuilder();
        FormatExceptionRecursive(exception, sb, 0);
        return sb.ToString();
    }

    private static void FormatExceptionRecursive(Exception exception, StringBuilder sb, int depth)
    {
        if (depth > 0)
        {
            sb.AppendLine();
            sb.Append("---> ");
        }

        sb.Append(exception.GetType().FullName);
        sb.Append(": ");
        sb.Append(exception.Message);

        if (exception.StackTrace is not null)
        {
            sb.AppendLine();
            sb.Append(exception.StackTrace);
        }

        if (exception.InnerException is not null && depth < 5)
        {
            FormatExceptionRecursive(exception.InnerException, sb, depth + 1);
        }
    }

    private static string ExtractServiceName(string categoryName)
    {
        // 从类别名称中提取服务名称
        // 例如: "JZVerse.MicroHuaxia.Gateway.Http.Middleware" -> "Gateway.Http"
        var parts = categoryName.Split('.');
        if (parts.Length >= 2)
        {
            // 查找 "Gateway" 或 "ServiceDiscovery" 或 "ConfigCenter"
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i] is "Gateway" or "ServiceDiscovery" or "ConfigCenter")
                {
                    return $"{parts[i]}.{parts[i + 1]}";
                }
            }
        }

        return parts.Length > 0 ? parts[^1] : categoryName;
    }

    private static LogSource DetermineSource(string categoryName)
    {
        if (categoryName.Contains("Middleware", StringComparison.OrdinalIgnoreCase))
            return LogSource.Middleware;

        if (categoryName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
            categoryName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            return LogSource.System;

        return LogSource.Application;
    }
}
