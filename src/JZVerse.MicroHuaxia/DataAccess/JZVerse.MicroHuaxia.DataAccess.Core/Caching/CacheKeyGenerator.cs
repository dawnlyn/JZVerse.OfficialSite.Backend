using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Caching;

/// <summary>
/// 默认缓存键生成器实现
/// </summary>
public sealed class CacheKeyGenerator : ICacheKeyGenerator
{
    private readonly CacheOptions _options;
    private readonly ICacheableEntityScanner _scanner;

    /// <summary>
    /// 键格式：{appName}:entity:{entityName}:id:{primaryKeyValue}
    /// </summary>
    private const string EntityKeyFormat = "{0}:entity:{1}:id:{2}";

    /// <summary>
    /// 整表键格式：{appName}:entity:{entityName}:all
    /// </summary>
    private const string TableKeyFormat = "{0}:entity:{1}:all";

    /// <summary>
    /// 查询键格式：{appName}:query:{entityName}:{queryHash}
    /// </summary>
    private const string QueryKeyFormat = "{0}:query:{1}:{2}";

    /// <summary>
    /// 实体前缀格式：{appName}:entity:{entityName}:
    /// </summary>
    private const string EntityPrefixFormat = "{0}:entity:{1}:";

    public CacheKeyGenerator(IOptions<CacheOptions> options, ICacheableEntityScanner scanner)
    {
        _options = options.Value;
        _scanner = scanner;
    }

    /// <inheritdoc />
    public string GenerateKey<TEntity>(object? primaryKey = null)
    {
        return GenerateKey(typeof(TEntity), primaryKey);
    }

    /// <inheritdoc />
    public string GenerateKey(Type entityType, object? primaryKey = null)
    {
        var entityName = GetEntityName(entityType);

        if (primaryKey is null)
        {
            // 整表缓存键
            return string.Format(TableKeyFormat, _options.ApplicationName, entityName);
        }

        // 单条记录缓存键
        var keyValue = FormatPrimaryKey(primaryKey);
        return string.Format(EntityKeyFormat, _options.ApplicationName, entityName, keyValue);
    }

    /// <inheritdoc />
    public string GenerateQueryKey<TEntity>(string queryIdentifier, object? parameters = null)
    {
        var entityName = GetEntityName(typeof(TEntity));
        var queryHash = ComputeQueryHash(queryIdentifier, parameters);
        return string.Format(QueryKeyFormat, _options.ApplicationName, entityName, queryHash);
    }

    /// <inheritdoc />
    public string GenerateEntityPrefix<TEntity>()
    {
        return GenerateEntityPrefix(typeof(TEntity));
    }

    /// <inheritdoc />
    public string GenerateEntityPrefix(Type entityType)
    {
        var entityName = GetEntityName(entityType);
        return string.Format(EntityPrefixFormat, _options.ApplicationName, entityName);
    }

    private string GetEntityName(Type entityType)
    {
        var metadata = _scanner.GetMetadata(entityType);
        return metadata?.TableName ?? entityType.Name;
    }

    private static string FormatPrimaryKey(object primaryKey)
    {
        // 处理复合主键（元组或匿名类型）
        if (primaryKey is ITuple tuple)
        {
            var parts = new string[tuple.Length];
            for (var i = 0; i < tuple.Length; i++)
            {
                parts[i] = tuple[i]?.ToString() ?? string.Empty;
            }
            return string.Join("_", parts);
        }

        // 处理匿名类型
        var type = primaryKey.GetType();
        if (type.IsClass && type.Name.StartsWith("<>"))
        {
            var properties = type.GetProperties();
            var parts = properties.Select(p => p.GetValue(primaryKey)?.ToString() ?? string.Empty);
            return string.Join("_", parts);
        }

        return primaryKey.ToString() ?? string.Empty;
    }

    private static string ComputeQueryHash(string queryIdentifier, object? parameters)
    {
        var input = queryIdentifier;

        if (parameters is not null)
        {
            // 简单地将参数序列化为字符串
            input += "|" + System.Text.Json.JsonSerializer.Serialize(parameters);
        }

        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
