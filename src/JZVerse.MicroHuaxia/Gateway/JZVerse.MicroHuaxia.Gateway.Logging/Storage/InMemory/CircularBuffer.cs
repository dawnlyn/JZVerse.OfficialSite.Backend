using System.Collections;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage.InMemory;

/// <summary>
/// 高性能线程安全的循环缓冲区
/// </summary>
/// <typeparam name="T">元素类型</typeparam>
public sealed class CircularBuffer<T> : IEnumerable<T>
{
    private readonly T[] _buffer;
    private readonly object _lock = new();
    private int _head;
    private int _tail;
    private int _count;

    /// <summary>
    /// 缓冲区容量
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 当前元素数量
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// 缓冲区是否已满
    /// </summary>
    public bool IsFull
    {
        get
        {
            lock (_lock)
            {
                return _count == Capacity;
            }
        }
    }

    /// <summary>
    /// 创建循环缓冲区
    /// </summary>
    /// <param name="capacity">容量</param>
    public CircularBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
        _buffer = new T[capacity];
    }

    /// <summary>
    /// 添加元素，如果缓冲区已满则覆盖最旧的元素
    /// </summary>
    /// <param name="item">要添加的元素</param>
    /// <returns>被覆盖的旧元素（如果有）</returns>
    public T? Add(T item)
    {
        lock (_lock)
        {
            T? overwritten = default;

            if (_count == Capacity)
            {
                // 缓冲区已满，覆盖最旧的元素
                overwritten = _buffer[_head];
                _head = (_head + 1) % Capacity;
            }
            else
            {
                _count++;
            }

            _buffer[_tail] = item;
            _tail = (_tail + 1) % Capacity;

            return overwritten;
        }
    }

    /// <summary>
    /// 批量添加元素
    /// </summary>
    /// <param name="items">要添加的元素</param>
    public void AddRange(IEnumerable<T> items)
    {
        lock (_lock)
        {
            foreach (var item in items)
            {
                if (_count == Capacity)
                {
                    _head = (_head + 1) % Capacity;
                }
                else
                {
                    _count++;
                }

                _buffer[_tail] = item;
                _tail = (_tail + 1) % Capacity;
            }
        }
    }

    /// <summary>
    /// 获取所有元素（按添加顺序，从旧到新）
    /// </summary>
    public IReadOnlyList<T> ToList()
    {
        lock (_lock)
        {
            var result = new List<T>(_count);
            for (var i = 0; i < _count; i++)
            {
                var index = (_head + i) % Capacity;
                result.Add(_buffer[index]);
            }
            return result;
        }
    }

    /// <summary>
    /// 获取最新的 N 个元素（按添加顺序，从旧到新）
    /// </summary>
    public IReadOnlyList<T> GetLatest(int count)
    {
        lock (_lock)
        {
            var takeCount = Math.Min(count, _count);
            var result = new List<T>(takeCount);
            var startIndex = _count - takeCount;

            for (var i = 0; i < takeCount; i++)
            {
                var index = (_head + startIndex + i) % Capacity;
                result.Add(_buffer[index]);
            }

            return result;
        }
    }

    /// <summary>
    /// 根据条件筛选元素
    /// </summary>
    public IReadOnlyList<T> Where(Func<T, bool> predicate)
    {
        lock (_lock)
        {
            var result = new List<T>();
            for (var i = 0; i < _count; i++)
            {
                var index = (_head + i) % Capacity;
                var item = _buffer[index];
                if (predicate(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// 清空缓冲区
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _tail = 0;
            _count = 0;
        }
    }

    /// <summary>
    /// 移除满足条件的元素
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <returns>移除的元素数量</returns>
    public int RemoveWhere(Func<T, bool> predicate)
    {
        lock (_lock)
        {
            var remaining = new List<T>(_count);
            for (var i = 0; i < _count; i++)
            {
                var index = (_head + i) % Capacity;
                var item = _buffer[index];
                if (!predicate(item))
                {
                    remaining.Add(item);
                }
            }

            var removed = _count - remaining.Count;

            if (removed > 0)
            {
                // 重建缓冲区
                Array.Clear(_buffer, 0, _buffer.Length);
                _head = 0;
                _tail = 0;
                _count = 0;

                foreach (var item in remaining)
                {
                    _buffer[_tail] = item;
                    _tail = (_tail + 1) % Capacity;
                    _count++;
                }
            }

            return removed;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        List<T> snapshot;
        lock (_lock)
        {
            snapshot = new List<T>(_count);
            for (var i = 0; i < _count; i++)
            {
                var index = (_head + i) % Capacity;
                snapshot.Add(_buffer[index]);
            }
        }

        foreach (var item in snapshot)
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
