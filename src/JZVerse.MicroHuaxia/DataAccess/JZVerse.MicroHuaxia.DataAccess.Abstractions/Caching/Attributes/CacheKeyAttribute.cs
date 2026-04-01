namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;

/// <summary>
/// 标记实体类的主键字段
/// </summary>
/// <remarks>
/// 用于生成缓存键。支持复合主键，通过 Order 属性指定顺序。
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class CacheKeyAttribute : Attribute
{
    /// <summary>
    /// 复合主键的顺序（从 0 开始）
    /// </summary>
    /// <remarks>
    /// 复合主键的各部分将按 Order 升序排列后用下划线连接
    /// </remarks>
    public int Order { get; set; } = 0;

    /// <summary>
    /// 自定义键前缀（可选）
    /// </summary>
    public string? Prefix { get; set; }
}
