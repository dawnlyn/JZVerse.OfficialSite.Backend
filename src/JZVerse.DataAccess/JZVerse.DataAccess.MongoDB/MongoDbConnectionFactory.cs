using System.Data;
using JZVerse.DataAccess.Abstractions;
using JZVerse.DataAccess.Abstractions.Models;
using JZVerse.DataAccess.Core.Dialects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JZVerse.DataAccess.MongoDB;

/// <summary>
/// MongoDB 连接配置选项
/// </summary>
public class MongoDbConnectionOptions
{
    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 数据库名称
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 最大连接池大小
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// 最小连接池大小
    /// </summary>
    public int MinPoolSize { get; set; } = 10;
}

/// <summary>
/// MongoDB 连接工厂实现
/// </summary>
/// <remarks>
/// MongoDB 不是关系型数据库，IDbConnection 接口仅用于统一抽象。
/// 实际使用时应直接使用 IMongoDatabase 接口。
/// </remarks>
public sealed class MongoDbConnectionFactory : IDbConnectionFactory
{
    private readonly MongoDbConnectionOptions _options;
    private readonly ILogger<MongoDbConnectionFactory> _logger;
    private readonly MongoDbDialect _dialect = new();
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;

    /// <summary>
    /// 创建 MongoDB 连接工厂
    /// </summary>
    public MongoDbConnectionFactory(
        IOptions<MongoDbConnectionOptions> options,
        ILogger<MongoDbConnectionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;

        var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
        settings.ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds);
        settings.MaxConnectionPoolSize = _options.MaxPoolSize;
        settings.MinConnectionPoolSize = _options.MinPoolSize;

        _client = new MongoClient(settings);
        _database = _client.GetDatabase(_options.DatabaseName);
    }

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.MongoDB;

    /// <inheritdoc />
    public IDbDialect Dialect => _dialect;

    /// <summary>
    /// 获取 MongoDB 数据库实例
    /// </summary>
    public IMongoDatabase Database => _database;

    /// <summary>
    /// 获取 MongoDB 客户端
    /// </summary>
    public IMongoClient Client => _client;

    /// <inheritdoc />
    public IDbConnection CreateConnection()
    {
        // MongoDB 不使用 IDbConnection，此方法仅为满足接口要求
        throw new NotSupportedException(
            "MongoDB does not use IDbConnection. Use the Database property to access IMongoDatabase instead.");
    }

    /// <inheritdoc />
    public Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "MongoDB does not use IDbConnection. Use the Database property to access IMongoDatabase instead.");
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 执行 ping 命令测试连接
            var result = await _database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);
            return result is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MongoDB connection test failed");
            return false;
        }
    }
}
