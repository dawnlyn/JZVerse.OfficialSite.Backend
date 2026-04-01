using JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Routing;

/// <summary>
/// 数据库路由器实现
/// </summary>
public class DatabaseRouter : IDatabaseRouter
{
    private readonly DataAccessOptions _options;
    private readonly IEntityMetadataProvider _metadataProvider;
    private readonly ILogger<DatabaseRouter> _logger;
    private readonly Dictionary<string, IShardingStrategy> _strategies;

    public DatabaseRouter(
        IOptions<DataAccessOptions> options,
        IEntityMetadataProvider metadataProvider,
        ILogger<DatabaseRouter> logger)
    {
        _options = options.Value;
        _metadataProvider = metadataProvider;
        _logger = logger;

        // 初始化分片策略
        _strategies = new Dictionary<string, IShardingStrategy>
        {
            ["Hash"] = new HashShardingStrategy(),
            ["Range"] = new RangeShardingStrategy(),
            ["Time"] = new TimeShardingStrategy()
        };
    }

    /// <inheritdoc />
    public RoutingResult Route<T>(object? shardingKey = null, DataOperationType operationType = DataOperationType.Query)
    {
        return Route(typeof(T), shardingKey, operationType);
    }

    /// <inheritdoc />
    public RoutingResult Route(Type entityType, object? shardingKey = null, DataOperationType operationType = DataOperationType.Query)
    {
        try
        {
            var metadata = _metadataProvider.GetMetadata(entityType);

            // 获取集群配置
            if (!_options.Clusters.TryGetValue(metadata.ClusterName, out var clusterConfig))
            {
                if (metadata.ClusterName != "default" && _options.Clusters.TryGetValue("default", out var defaultConfig))
                {
                    clusterConfig = defaultConfig;
                    _logger.LogWarning("Cluster '{ClusterName}' not found, using default cluster", metadata.ClusterName);
                }
                else
                {
                    return RoutingResult.Failure($"Cluster '{metadata.ClusterName}' not found");
                }
            }

            // 确定节点索引
            int nodeIndex;
            DatabaseNodeConfig nodeConfig;

            if (!metadata.UseSharding || clusterConfig.Nodes.Count == 1)
            {
                // 不分片或只有一个节点，使用主节点
                nodeIndex = 0;
                nodeConfig = clusterConfig.Nodes.FirstOrDefault(n => n.IsMaster)
                    ?? clusterConfig.Nodes.First();
            }
            else
            {
                // 计算分片索引
                nodeIndex = CalculateShardIndex(metadata, shardingKey, clusterConfig);

                // 获取对应节点配置
                nodeConfig = clusterConfig.Nodes.FirstOrDefault(n => n.NodeIndex == nodeIndex)
                    ?? clusterConfig.Nodes.First(n => n.IsMaster);
            }

            // 计算实际表名
            var actualTableName = metadata.TableName;
            int? shardIndex = null;

            if (metadata.UseSharding && metadata.ShardingStrategy != ShardingStrategyType.None)
            {
                shardIndex = CalculateTableShardIndex(metadata, shardingKey);

                if (_strategies.TryGetValue(metadata.ShardingStrategy.ToString(), out var strategy))
                {
                    actualTableName = strategy.GenerateTableName(
                        metadata.TableName,
                        shardingKey,
                        shardIndex.Value,
                        metadata.ActualTableNameFormat);
                }
            }

            return RoutingResult.Success(
                clusterName: metadata.ClusterName,
                nodeName: nodeConfig.Name,
                nodeIndex: nodeIndex,
                logicalTableName: metadata.TableName,
                actualTableName: actualTableName,
                connectionString: nodeConfig.ConnectionString,
                shardIndex: shardIndex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing for entity type {EntityType}", entityType.Name);
            return RoutingResult.Failure($"Routing failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public List<string> GetAllShardTableNames<T>()
    {
        return GetAllShardTableNames(typeof(T));
    }

    private List<string> GetAllShardTableNames(Type entityType)
    {
        var metadata = _metadataProvider.GetMetadata(entityType);
        var tableNames = new List<string>();

        if (!metadata.UseSharding || metadata.ShardingStrategy == ShardingStrategyType.None)
        {
            tableNames.Add(metadata.TableName);
            return tableNames;
        }

        if (_strategies.TryGetValue(metadata.ShardingStrategy.ToString(), out var strategy))
        {
            for (int i = 0; i < metadata.ShardCount; i++)
            {
                tableNames.Add(strategy.GenerateTableName(metadata.TableName, null, i, metadata.ActualTableNameFormat));
            }
        }

        return tableNames;
    }

    /// <inheritdoc />
    public RoutingContext? ParseRouteFromSql(string sql, object? parameters = null)
    {
        // 简单的SQL解析，提取表名
        // 实际应用中可能需要更复杂的SQL解析器

        var context = new RoutingContext
        {
            OperationType = GetOperationType(sql)
        };

        // 尝试从SQL中提取表名（简化版）
        var tableName = ExtractTableName(sql);
        if (!string.IsNullOrEmpty(tableName))
        {
            context.LogicalTableName = tableName;
        }

        // 尝试从参数中提取分片键
        if (parameters != null)
        {
            context.ShardingKey = ExtractShardingKey(parameters);
        }

        return context;
    }

    private int CalculateShardIndex(EntityMetadata metadata, object? shardingKey, DatabaseClusterConfig clusterConfig)
    {
        if (!metadata.UseSharding || shardingKey == null)
        {
            return 0;
        }

        // 获取分片策略配置
        if (_options.ShardingRules.TryGetValue(metadata.TableName, out var ruleConfig))
        {
            if (_strategies.TryGetValue(ruleConfig.Strategy, out var strategy))
            {
                return strategy.CalculateShardIndex(shardingKey, clusterConfig.Nodes.Count);
            }
        }

        // 默认使用哈希分片
        var hashStrategy = new HashShardingStrategy();
        return hashStrategy.CalculateShardIndex(shardingKey, clusterConfig.Nodes.Count);
    }

    private int CalculateTableShardIndex(EntityMetadata metadata, object? shardingKey)
    {
        if (!metadata.UseSharding || shardingKey == null)
        {
            return 0;
        }

        if (_strategies.TryGetValue(metadata.ShardingStrategy.ToString(), out var strategy))
        {
            return strategy.CalculateShardIndex(shardingKey, metadata.ShardCount);
        }

        // 默认使用哈希分片
        var hashStrategy = new HashShardingStrategy();
        return hashStrategy.CalculateShardIndex(shardingKey, metadata.ShardCount);
    }

    private static DataOperationType GetOperationType(string sql)
    {
        var normalizedSql = sql.Trim().ToUpperInvariant();

        if (normalizedSql.StartsWith("SELECT"))
            return DataOperationType.Query;
        if (normalizedSql.StartsWith("INSERT"))
            return DataOperationType.Insert;
        if (normalizedSql.StartsWith("UPDATE"))
            return DataOperationType.Update;
        if (normalizedSql.StartsWith("DELETE"))
            return DataOperationType.Delete;

        return DataOperationType.Query;
    }

    private static string? ExtractTableName(string sql)
    {
        // 简化版SQL解析，实际应用可能需要更复杂的解析器
        var normalizedSql = sql.Trim().ToUpperInvariant();

        // SELECT ... FROM table_name
        var fromMatch = System.Text.RegularExpressions.Regex.Match(
            normalizedSql,
            @"FROM\s+(\w+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (fromMatch.Success)
        {
            return fromMatch.Groups[1].Value;
        }

        // INSERT INTO table_name
        var insertMatch = System.Text.RegularExpressions.Regex.Match(
            normalizedSql,
            @"INTO\s+(\w+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (insertMatch.Success)
        {
            return insertMatch.Groups[1].Value;
        }

        // UPDATE table_name
        var updateMatch = System.Text.RegularExpressions.Regex.Match(
            normalizedSql,
            @"UPDATE\s+(\w+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (updateMatch.Success)
        {
            return updateMatch.Groups[1].Value;
        }

        // DELETE FROM table_name
        var deleteMatch = System.Text.RegularExpressions.Regex.Match(
            normalizedSql,
            @"DELETE\s+FROM\s+(\w+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (deleteMatch.Success)
        {
            return deleteMatch.Groups[1].Value;
        }

        return null;
    }

    private static object? ExtractShardingKey(object parameters)
    {
        // 尝试从参数对象中提取可能的键值
        var properties = parameters.GetType().GetProperties();

        var idProperty = properties.FirstOrDefault(p =>
            p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

        return idProperty?.GetValue(parameters);
    }
}
