using JZVerse.DataAccess.Abstractions.Models;

namespace JZVerse.DataAccess.Abstractions;

/// <summary>
/// 雪花 ID 生成器接口
/// </summary>
public interface ISnowflakeIdGenerator
{
    /// <summary>
    /// 生成新的雪花 ID
    /// </summary>
    /// <returns>128 位雪花 ID</returns>
    SnowflakeId Generate();

    /// <summary>
    /// 生成新的 GUID 格式 ID
    /// </summary>
    /// <returns>GUID 格式的 ID</returns>
    Guid GenerateGuid();

    /// <summary>
    /// 批量生成雪花 ID
    /// </summary>
    /// <param name="count">生成数量</param>
    /// <returns>雪花 ID 列表</returns>
    IReadOnlyList<SnowflakeId> GenerateBatch(int count);

    /// <summary>
    /// 批量生成 GUID 格式 ID
    /// </summary>
    /// <param name="count">生成数量</param>
    /// <returns>GUID 列表</returns>
    IReadOnlyList<Guid> GenerateGuidBatch(int count);

    /// <summary>
    /// 从 GUID 转换为雪花 ID
    /// </summary>
    /// <param name="guid">GUID</param>
    /// <returns>雪花 ID</returns>
    SnowflakeId FromGuid(Guid guid);

    /// <summary>
    /// 获取当前数据中心 ID
    /// </summary>
    int DatacenterId { get; }

    /// <summary>
    /// 获取当前机器 ID
    /// </summary>
    int WorkerId { get; }
}
