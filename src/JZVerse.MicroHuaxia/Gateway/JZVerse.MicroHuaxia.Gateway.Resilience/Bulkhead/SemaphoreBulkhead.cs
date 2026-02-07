using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Resilience.Bulkhead;

/// <summary>
/// 基于信号量的舱壁隔离实现
/// </summary>
public sealed class SemaphoreBulkhead : IBulkhead, IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly BulkheadOptions _options;
    private readonly ILogger<SemaphoreBulkhead> _logger;
    private int _queueLength;
    private bool _disposed;

    public SemaphoreBulkhead(
        string name,
        BulkheadOptions options,
        ILogger<SemaphoreBulkhead> logger)
    {
        Name = name;
        _options = options;
        _logger = logger;
        _semaphore = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);
    }

    public string Name { get; }

    public int MaxConcurrency => _options.MaxConcurrency;

    public int CurrentConcurrency => _options.MaxConcurrency - _semaphore.CurrentCount;

    public int MaxQueueLength => _options.MaxQueueLength;

    public int CurrentQueueLength => _queueLength;

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // 检查队列是否已满
        var currentQueue = Interlocked.Increment(ref _queueLength);
        if (currentQueue > _options.MaxQueueLength)
        {
            Interlocked.Decrement(ref _queueLength);
            _logger.LogWarning(
                "Bulkhead '{Name}' rejected request: queue full ({QueueLength}/{MaxQueueLength})",
                Name, currentQueue - 1, _options.MaxQueueLength);
            throw new BulkheadRejectedException(Name,
                $"Bulkhead '{Name}' rejected the request: queue is full ({_options.MaxQueueLength})");
        }

        try
        {
            // 尝试获取信号量（带超时）
            var acquired = await _semaphore.WaitAsync(_options.QueueTimeout, cancellationToken);
            if (!acquired)
            {
                _logger.LogWarning(
                    "Bulkhead '{Name}' rejected request: queue timeout ({Timeout}ms)",
                    Name, _options.QueueTimeout.TotalMilliseconds);
                throw new BulkheadRejectedException(Name,
                    $"Bulkhead '{Name}' rejected the request: queue timeout ({_options.QueueTimeout.TotalMilliseconds}ms)");
            }

            // 获取成功，从队列中移出
            Interlocked.Decrement(ref _queueLength);

            try
            {
                _logger.LogDebug(
                    "Bulkhead '{Name}' executing: concurrency={CurrentConcurrency}/{MaxConcurrency}",
                    Name, CurrentConcurrency, MaxConcurrency);

                return await action(cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Decrement(ref _queueLength);
            throw;
        }
        catch (BulkheadRejectedException)
        {
            throw;
        }
        catch
        {
            // 如果在等待信号量时发生异常，确保队列计数正确
            if (_queueLength > 0)
            {
                Interlocked.Decrement(ref _queueLength);
            }
            throw;
        }
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async ct =>
        {
            await action(ct);
            return true;
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
    }
}

/// <summary>
/// 舱壁工厂实现
/// </summary>
public sealed class BulkheadFactory : IBulkheadFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreBulkhead> _bulkheads = new();
    private readonly BulkheadOptions _defaultOptions;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    public BulkheadFactory(
        ILoggerFactory loggerFactory,
        BulkheadOptions? defaultOptions = null)
    {
        _loggerFactory = loggerFactory;
        _defaultOptions = defaultOptions ?? new BulkheadOptions();
    }

    public IBulkhead GetOrCreate(string name, BulkheadOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _bulkheads.GetOrAdd(name, n =>
        {
            var opts = options ?? _defaultOptions;
            var logger = _loggerFactory.CreateLogger<SemaphoreBulkhead>();
            return new SemaphoreBulkhead(n, opts, logger);
        });
    }

    /// <summary>
    /// 获取所有舱壁的状态快照
    /// </summary>
    public IReadOnlyDictionary<string, BulkheadStatus> GetAllStatuses()
    {
        return _bulkheads.ToDictionary(
            kvp => kvp.Key,
            kvp => new BulkheadStatus
            {
                Name = kvp.Value.Name,
                MaxConcurrency = kvp.Value.MaxConcurrency,
                CurrentConcurrency = kvp.Value.CurrentConcurrency,
                MaxQueueLength = kvp.Value.MaxQueueLength,
                CurrentQueueLength = kvp.Value.CurrentQueueLength
            });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var bulkhead in _bulkheads.Values)
        {
            bulkhead.Dispose();
        }
        _bulkheads.Clear();
    }
}

/// <summary>
/// 舱壁状态快照
/// </summary>
public sealed record BulkheadStatus
{
    public required string Name { get; init; }
    public required int MaxConcurrency { get; init; }
    public required int CurrentConcurrency { get; init; }
    public required int MaxQueueLength { get; init; }
    public required int CurrentQueueLength { get; init; }

    public double ConcurrencyUtilization =>
        MaxConcurrency > 0 ? (double)CurrentConcurrency / MaxConcurrency : 0;

    public double QueueUtilization =>
        MaxQueueLength > 0 ? (double)CurrentQueueLength / MaxQueueLength : 0;
}
