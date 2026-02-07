using System.Collections.Concurrent;
using System.Diagnostics;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Providers;

/// <summary>
/// Gateway 日志提供者
/// </summary>
[ProviderAlias("GatewayLogging")]
public sealed class GatewayLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly GatewayLoggingOptions _options;
    private readonly ILogStore _logStore;
    private readonly ConcurrentDictionary<string, GatewayLogger> _loggers = new();
    private readonly Func<string, LogLevel, bool>? _filter;
    private IExternalScopeProvider? _scopeProvider;
    private bool _disposed;

    /// <summary>
    /// 服务名称
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// 服务实例 ID
    /// </summary>
    public string? ServiceInstanceId { get; set; }

    /// <summary>
    /// 主机名
    /// </summary>
    public string HostName { get; } = System.Environment.MachineName;

    /// <summary>
    /// 环境名称
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// 创建 Gateway 日志提供者
    /// </summary>
    public GatewayLoggerProvider(
        IOptions<GatewayLoggingOptions> options,
        ILogStore logStore)
    {
        _options = options.Value;
        _logStore = logStore;
        _filter = (category, level) => level >= _options.MinimumLevel;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new GatewayLogger(
            name,
            this,
            _logStore,
            _options,
            _filter));
    }

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;

        foreach (var logger in _loggers.Values)
        {
            logger.ScopeProvider = _scopeProvider;
        }
    }

    internal IExternalScopeProvider? ScopeProvider => _scopeProvider;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _loggers.Clear();
    }
}
