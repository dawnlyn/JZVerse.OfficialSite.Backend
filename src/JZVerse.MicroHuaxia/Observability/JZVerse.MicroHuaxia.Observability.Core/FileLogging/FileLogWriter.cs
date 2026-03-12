using System.Threading.Channels;
using FileOptions = JZVerse.MicroHuaxia.Observability.Core.Configuration.FileOptions;

namespace JZVerse.MicroHuaxia.Observability.Core.FileLogging;

/// <summary>
/// 异步文件日志写入器 — Channel 队列 + 单写者消费模型
/// </summary>
public sealed class FileLogWriter : IAsyncDisposable, IDisposable
{
    private readonly FileOptions _options;
    private readonly string _serviceName;
    private readonly Channel<string> _channel;
    private readonly Task _consumeTask;
    private readonly CancellationTokenSource _cts = new();

    private StreamWriter? _writer;
    private string _currentFilePath = string.Empty;
    private long _currentFileSize;
    private DateOnly _currentDate;
    private int _currentSegment;

    public FileLogWriter(FileOptions options, string serviceName)
    {
        _options = options;
        _serviceName = serviceName;
        _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        EnsureDirectory();
        _currentDate = DateOnly.FromDateTime(DateTime.Now);
        OpenNewFile();

        _consumeTask = Task.Run(ConsumeLoop);
    }

    /// <summary>
    /// 写入一行日志（非阻塞）
    /// </summary>
    public void WriteLine(string line)
    {
        _channel.Writer.TryWrite(line);
    }

    private async Task ConsumeLoop()
    {
        var flushInterval = TimeSpan.FromSeconds(Math.Max(1, _options.FlushIntervalSeconds));
        var reader = _channel.Reader;
        var token = _cts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                // 等待有数据可读或超时（用于定期 flush）
                if (await reader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    var lastFlush = DateTime.UtcNow;

                    while (reader.TryRead(out var line))
                    {
                        CheckRotation();
                        await WriteLineInternal(line).ConfigureAwait(false);

                        // 定期 flush
                        if (DateTime.UtcNow - lastFlush >= flushInterval)
                        {
                            await FlushInternal().ConfigureAwait(false);
                            lastFlush = DateTime.UtcNow;
                        }
                    }

                    await FlushInternal().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
        finally
        {
            // 消费剩余消息
            while (reader.TryRead(out var line))
            {
                CheckRotation();
                await WriteLineInternal(line).ConfigureAwait(false);
            }

            await FlushInternal().ConfigureAwait(false);
        }
    }

    private async Task WriteLineInternal(string line)
    {
        if (_writer is null) return;

        await _writer.WriteLineAsync(line).ConfigureAwait(false);
        _currentFileSize += System.Text.Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
    }

    private async Task FlushInternal()
    {
        if (_writer is not null)
        {
            await _writer.FlushAsync().ConfigureAwait(false);
        }
    }

    private void CheckRotation()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        // 按日期轮换
        if (today != _currentDate)
        {
            _currentDate = today;
            _currentSegment = 0;
            OpenNewFile();
            return;
        }

        // 按大小轮换
        if (_options.MaxFileSizeMB > 0 && _currentFileSize >= (long)_options.MaxFileSizeMB * 1024 * 1024)
        {
            _currentSegment++;
            OpenNewFile();
        }
    }

    private void OpenNewFile()
    {
        _writer?.Flush();
        _writer?.Dispose();

        _currentFilePath = BuildFilePath();
        _writer = new StreamWriter(_currentFilePath, append: true, encoding: System.Text.Encoding.UTF8)
        {
            AutoFlush = false,
        };
        _currentFileSize = new FileInfo(_currentFilePath).Length;
    }

    private string BuildFilePath()
    {
        var dir = GetLogDirectory();
        var dateStr = _currentDate.ToString("yyyy-MM-dd");
        var fileName = _options.FileNamePattern
            .Replace("{ServiceName}", _serviceName)
            .Replace("{Date}", dateStr);

        if (_currentSegment > 0)
        {
            var ext = Path.GetExtension(fileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            fileName = $"{nameWithoutExt}.{_currentSegment:D3}{ext}";
        }

        return Path.Combine(dir, fileName);
    }

    private string GetLogDirectory()
    {
        var dir = _options.Directory;
        if (!Path.IsPathRooted(dir))
        {
            dir = Path.Combine(AppContext.BaseDirectory, dir);
        }
        return dir;
    }

    private void EnsureDirectory()
    {
        var dir = GetLogDirectory();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _cts.CancelAsync();

        try
        {
            await _consumeTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }

        if (_writer is not null)
        {
            await _writer.FlushAsync();
            await _writer.DisposeAsync();
        }

        _cts.Dispose();
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();

        try
        {
            _consumeTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) { }

        _writer?.Flush();
        _writer?.Dispose();
        _cts.Dispose();
    }
}
