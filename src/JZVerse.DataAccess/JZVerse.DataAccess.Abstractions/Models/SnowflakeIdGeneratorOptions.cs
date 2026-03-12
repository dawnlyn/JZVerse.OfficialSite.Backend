namespace JZVerse.DataAccess.Abstractions.Models;

/// <summary>
/// 雪花 ID 生成器配置选项
/// </summary>
public class SnowflakeIdGeneratorOptions
{
    /// <summary>
    /// 数据中心 ID (0-31)
    /// </summary>
    public int DatacenterId { get; set; } = 1;

    /// <summary>
    /// 机器 ID (0-31)
    /// </summary>
    public int WorkerId { get; set; } = 1;

    /// <summary>
    /// 时钟回拨容忍时间（毫秒）
    /// 当时钟回拨在此范围内时，会等待时钟追上
    /// </summary>
    public int ClockBackwardToleranceMs { get; set; } = 5;
}
