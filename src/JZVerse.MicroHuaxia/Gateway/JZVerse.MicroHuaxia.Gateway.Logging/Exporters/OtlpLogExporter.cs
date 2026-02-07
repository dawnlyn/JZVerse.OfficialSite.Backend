using System.Threading.Channels;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Exporters;

/// <summary>
/// OTLP 日志导出器（将日志导出到 Loki/ELK 等外部系统）
/// </summary>
public sealed class OtlpLogExporter : IDisposable
{
    private readonly OtlpLogExporterOptions _options;
    private readonly ILogger<OtlpLogExporter> _logger;
    private readonly Channel<LogEntry> _exportChannel;
    private readonly Timer _exportTimer;
    private readonly List<LogEntry> _batch = [];
    private readonly object _batchLock = new();
    private bool _disposed;

    /// <summary>
    /// 创建 OTLP 日志导出器
    /// </summary>
    public OtlpLogExporter(
        IOptions<OtlpLogExporterOptions> options,
        ILogger<OtlpLogExporter> logger)
    {
        _options = options.Value;
        _logger = logger;

        // 创建导出通道
        _exportChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(_options.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        // 启动导出任务
        _ = ProcessExportChannelAsync();

        // 定期导出
        _exportTimer = new Timer(
            _ => _ = FlushBatchAsync(),
            null,
            TimeSpan.FromSeconds(_options.ExportIntervalSeconds),
            TimeSpan.FromSeconds(_options.ExportIntervalSeconds));

        _logger.LogInformation("OTLP 日志导出器已启动，端点: {Endpoint}", _options.Endpoint);
    }

    /// <summary>
    /// 导出日志条目
    /// </summary>
    public void Export(LogEntry entry)
    {
        if (_disposed) return;

        _exportChannel.Writer.TryWrite(entry);
    }

    /// <summary>
    /// 批量导出日志条目
    /// </summary>
    public void ExportBatch(IEnumerable<LogEntry> entries)
    {
        if (_disposed) return;

        foreach (var entry in entries)
        {
            _exportChannel.Writer.TryWrite(entry);
        }
    }

    private async Task ProcessExportChannelAsync()
    {
        await foreach (var entry in _exportChannel.Reader.ReadAllAsync())
        {
            if (_disposed) break;

            lock (_batchLock)
            {
                _batch.Add(entry);
            }

            if (_batch.Count >= _options.BatchSize)
            {
                await FlushBatchAsync();
            }
        }
    }

    private async Task FlushBatchAsync()
    {
        List<LogEntry> entries;
        lock (_batchLock)
        {
            if (_batch.Count == 0)
                return;

            entries = [.. _batch];
            _batch.Clear();
        }

        try
        {
            await ExportToOtlpAsync(entries);
            _logger.LogDebug("已导出 {Count} 条日志到 OTLP", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出日志到 OTLP 失败，共 {Count} 条", entries.Count);

            if (_options.EnableRetry)
            {
                // 重试逻辑可以在这里实现
                // 为简化实现，暂时只记录错误
            }
        }
    }

    private async Task ExportToOtlpAsync(IReadOnlyList<LogEntry> entries)
    {
        // 使用 OpenTelemetry SDK 导出
        // 这里提供一个简化的 HTTP 导出实现
        // 生产环境建议使用 OpenTelemetry.Exporter.OpenTelemetryProtocol

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(_options.ExportTimeoutSeconds)
        };

        // 添加自定义头部
        foreach (var header in _options.Headers)
        {
            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        var endpoint = _options.Protocol == OtlpProtocol.HttpProtobuf
            ? $"{_options.Endpoint}/v1/logs"
            : _options.Endpoint;

        // 构建 OTLP 日志请求
        var request = BuildOtlpRequest(entries);

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await httpClient.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
    }

    private static object BuildOtlpRequest(IReadOnlyList<LogEntry> entries)
    {
        // 构建 OTLP 格式的日志请求
        // 这是一个简化的 JSON 格式，实际 OTLP 使用 Protobuf
        return new
        {
            resourceLogs = new[]
            {
                new
                {
                    resource = new
                    {
                        attributes = new[]
                        {
                            new { key = "service.name", value = new { stringValue = "gateway" } }
                        }
                    },
                    scopeLogs = new[]
                    {
                        new
                        {
                            scope = new { name = "gateway.logging" },
                            logRecords = entries.Select(e => new
                            {
                                timeUnixNano = e.Timestamp.ToUnixTimeMilliseconds() * 1_000_000,
                                severityNumber = GetSeverityNumber(e.Level),
                                severityText = e.Level.ToString(),
                                body = new { stringValue = e.Message },
                                attributes = GetAttributes(e),
                                traceId = e.TraceId,
                                spanId = e.SpanId
                            }).ToArray()
                        }
                    }
                }
            }
        };
    }

    private static int GetSeverityNumber(Microsoft.Extensions.Logging.LogLevel level)
    {
        return level switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => 1,
            Microsoft.Extensions.Logging.LogLevel.Debug => 5,
            Microsoft.Extensions.Logging.LogLevel.Information => 9,
            Microsoft.Extensions.Logging.LogLevel.Warning => 13,
            Microsoft.Extensions.Logging.LogLevel.Error => 17,
            Microsoft.Extensions.Logging.LogLevel.Critical => 21,
            _ => 0
        };
    }

    private static object[] GetAttributes(LogEntry entry)
    {
        var attributes = new List<object>();

        if (!string.IsNullOrEmpty(entry.ServiceName))
        {
            attributes.Add(new { key = "service.name", value = new { stringValue = entry.ServiceName } });
        }

        if (!string.IsNullOrEmpty(entry.Category))
        {
            attributes.Add(new { key = "log.category", value = new { stringValue = entry.Category } });
        }

        if (!string.IsNullOrEmpty(entry.RequestPath))
        {
            attributes.Add(new { key = "http.route", value = new { stringValue = entry.RequestPath } });
        }

        if (!string.IsNullOrEmpty(entry.RequestMethod))
        {
            attributes.Add(new { key = "http.method", value = new { stringValue = entry.RequestMethod } });
        }

        if (entry.StatusCode.HasValue)
        {
            attributes.Add(new { key = "http.status_code", value = new { intValue = entry.StatusCode.Value } });
        }

        if (!string.IsNullOrEmpty(entry.Exception))
        {
            attributes.Add(new { key = "exception.message", value = new { stringValue = entry.Exception } });
        }

        return [.. attributes];
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _exportChannel.Writer.Complete();
        _exportTimer.Dispose();

        // 刷新剩余的批量数据
        _ = FlushBatchAsync();
    }
}
