namespace JZVerse.DataAccess.Abstractions.Caching;

/// <summary>
/// 全局缓存配置选项
/// </summary>
public sealed class CacheOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "DataAccess:Caching";

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 应用名称（用于缓存键前缀）
    /// </summary>
    public string ApplicationName { get; set; } = "jzverse";

    /// <summary>
    /// 内存缓存配置
    /// </summary>
    public MemoryCacheOptions Memory { get; set; } = new();

    /// <summary>
    /// Redis/Garnet 缓存配置
    /// </summary>
    public RedisCacheOptions Redis { get; set; } = new();

    /// <summary>
    /// 默认缓存策略
    /// </summary>
    public CacheStrategyOptions DefaultStrategy { get; set; } = new();

    /// <summary>
    /// 预热配置
    /// </summary>
    public WarmupOptions Warmup { get; set; } = new();
}

/// <summary>
/// 内存缓存配置选项
/// </summary>
public sealed class MemoryCacheOptions
{
    /// <summary>
    /// 是否启用内存缓存
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最大缓存大小（MB）
    /// </summary>
    public int MaxSizeMB { get; set; } = 512;

    /// <summary>
    /// 默认过期时间（秒）
    /// </summary>
    public int DefaultExpirationSeconds { get; set; } = 300;

    /// <summary>
    /// 压缩阈值（当缓存使用量超过此比例时触发压缩）
    /// </summary>
    public double CompactionPercentage { get; set; } = 0.25;
}

/// <summary>
/// Redis/Garnet 缓存配置选项
/// </summary>
public sealed class RedisCacheOptions
{
    /// <summary>
    /// 是否启用 Redis 缓存
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// 数据库编号
    /// </summary>
    public int Database { get; set; } = 0;

    /// <summary>
    /// 默认过期时间（秒）
    /// </summary>
    public int DefaultExpirationSeconds { get; set; } = 600;

    /// <summary>
    /// 实例名称前缀
    /// </summary>
    public string InstanceName { get; set; } = "jzverse:";

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 同步操作超时时间（毫秒）
    /// </summary>
    public int SyncTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 是否允许管理员操作
    /// </summary>
    public bool AllowAdmin { get; set; } = false;

    /// <summary>
    /// 失败时是否降级到直接查询数据库
    /// </summary>
    public bool FallbackOnFailure { get; set; } = true;
}

/// <summary>
/// 缓存预热配置选项
/// </summary>
public sealed class WarmupOptions
{
    /// <summary>
    /// 是否在启动时预热
    /// </summary>
    public bool EnableOnStartup { get; set; } = true;

    /// <summary>
    /// 并行预热度
    /// </summary>
    public int ParallelDegree { get; set; } = 4;

    /// <summary>
    /// 预热超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 预热失败是否阻止应用启动
    /// </summary>
    public bool FailOnError { get; set; } = false;

    /// <summary>
    /// 预热延迟启动时间（秒），等待应用完全启动后再预热
    /// </summary>
    public int DelayStartSeconds { get; set; } = 2;
}
