namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;

/// <summary>
/// 分片策略接口
/// </summary>
public interface IShardingStrategy
{
    /// <summary>
    /// 策略名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 计算分片索引
    /// </summary>
    /// <param name="shardingKey">分片键值</param>
    /// <param name="shardCount">分片总数</param>
    /// <returns>分片索引</returns>
    int CalculateShardIndex(object shardingKey, int shardCount);

    /// <summary>
    /// 生成实际表名
    /// </summary>
    /// <param name="logicalTableName">逻辑表名</param>
    /// <param name="shardingKey">分片键值</param>
    /// <param name="shardIndex">分片索引</param>
    /// <param name="format">表名格式模板</param>
    /// <returns>实际表名</returns>
    string GenerateTableName(string logicalTableName, object? shardingKey, int shardIndex, string? format = null);
}

/// <summary>
/// 哈希分片策略
/// </summary>
public class HashShardingStrategy : IShardingStrategy
{
    /// <inheritdoc />
    public string Name => "Hash";

    /// <inheritdoc />
    public int CalculateShardIndex(object shardingKey, int shardCount)
    {
        if (shardCount <= 1) return 0;

        var hashCode = GetHashCode(shardingKey);
        return Math.Abs(hashCode) % shardCount;
    }

    /// <inheritdoc />
    public string GenerateTableName(string logicalTableName, object? shardingKey, int shardIndex, string? format = null)
    {
        if (string.IsNullOrEmpty(format))
        {
            format = "{0}_{1:000}";
        }

        return string.Format(format, logicalTableName, shardIndex);
    }

    private static int GetHashCode(object key)
    {
        return key switch
        {
            null => 0,
            string s => s.GetHashCode(StringComparison.Ordinal),
            Guid g => g.GetHashCode(),
            int i => i,
            long l => l.GetHashCode(),
            _ => key.GetHashCode()
        };
    }
}

/// <summary>
/// 范围分片策略
/// </summary>
public class RangeShardingStrategy : IShardingStrategy
{
    /// <inheritdoc />
    public string Name => "Range";

    /// <summary>
    /// 范围配置
    /// </summary>
    public List<ShardRangeConfig> Ranges { get; set; } = new();

    /// <inheritdoc />
    public int CalculateShardIndex(object shardingKey, int shardCount)
    {
        var value = Convert.ToInt64(shardingKey);

        for (int i = 0; i < Ranges.Count; i++)
        {
            var range = Ranges[i];
            if (value >= range.StartValue && value < range.EndValue)
            {
                return i;
            }
        }

        // 默认返回最后一个分片
        return Math.Max(0, Ranges.Count - 1);
    }

    /// <inheritdoc />
    public string GenerateTableName(string logicalTableName, object? shardingKey, int shardIndex, string? format = null)
    {
        if (string.IsNullOrEmpty(format))
        {
            format = "{0}_{1:000}";
        }

        return string.Format(format, logicalTableName, shardIndex);
    }
}

/// <summary>
/// 时间分片策略
/// </summary>
public class TimeShardingStrategy : IShardingStrategy
{
    /// <inheritdoc />
    public string Name => "Time";

    /// <summary>
    /// 时间粒度
    /// </summary>
    public TimeShardingGranularity Granularity { get; set; } = TimeShardingGranularity.Month;

    /// <summary>
    /// 日期格式
    /// </summary>
    public string DateFormat { get; set; } = "yyyyMM";

    /// <inheritdoc />
    public int CalculateShardIndex(object shardingKey, int shardCount)
    {
        // 时间分片通常不需要索引计算，直接使用时间作为后缀
        return 0;
    }

    /// <inheritdoc />
    public string GenerateTableName(string logicalTableName, object? shardingKey, int shardIndex, string? format = null)
    {
        if (shardingKey == null)
        {
            return logicalTableName;
        }

        var dateTime = shardingKey switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            long ticks => new DateTime(ticks),
            string s => DateTime.TryParse(s, out var result) ? result : DateTime.UtcNow,
            _ => DateTime.UtcNow
        };

        var suffix = dateTime.ToString(DateFormat);
        return $"{logicalTableName}_{suffix}";
    }

    /// <summary>
    /// 获取指定日期范围涉及的所有表名
    /// </summary>
    public List<string> GetTableNamesForRange(string logicalTableName, DateTime startDate, DateTime endDate)
    {
        var tableNames = new List<string>();
        var currentDate = startDate;

        while (currentDate <= endDate)
        {
            var suffix = currentDate.ToString(DateFormat);
            tableNames.Add($"{logicalTableName}_{suffix}");

            currentDate = Granularity switch
            {
                TimeShardingGranularity.Day => currentDate.AddDays(1),
                TimeShardingGranularity.Week => currentDate.AddDays(7),
                TimeShardingGranularity.Month => currentDate.AddMonths(1),
                TimeShardingGranularity.Year => currentDate.AddYears(1),
                _ => currentDate.AddMonths(1)
            };
        }

        return tableNames.Distinct().ToList();
    }
}

/// <summary>
/// 时间分片粒度
/// </summary>
public enum TimeShardingGranularity
{
    Day,
    Week,
    Month,
    Year
}
