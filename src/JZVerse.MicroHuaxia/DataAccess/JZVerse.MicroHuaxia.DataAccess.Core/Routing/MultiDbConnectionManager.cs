using System.Data;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Routing;

/// <summary>
/// 多数据库连接管理器实现
/// </summary>
public class MultiDbConnectionManager : IMultiDbConnectionManager
{
    private readonly DataAccessOptions _options;
    private readonly ILogger<MultiDbConnectionManager> _logger;
    private readonly Dictionary<string, IDbConnectionFactory> _connectionFactories;
    private readonly Random _random = new();

    public MultiDbConnectionManager(
        IOptions<DataAccessOptions> options,
        ILogger<MultiDbConnectionManager> logger,
        IEnumerable<IDbConnectionFactory> connectionFactories)
    {
        _options = options.Value;
        _logger = logger;

        // 按数据库类型组织连接工厂
        _connectionFactories = connectionFactories.ToDictionary(
            f => f.DatabaseType.ToString(),
            f => f,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IDbConnection GetConnection(string clusterName, int nodeIndex = 0)
    {
        var clusterConfig = GetClusterConfig(clusterName);
        if (clusterConfig == null)
        {
            throw new InvalidOperationException($"Cluster '{clusterName}' not found");
        }

        var nodeConfig = clusterConfig.Nodes.FirstOrDefault(n => n.NodeIndex == nodeIndex)
            ?? throw new InvalidOperationException($"Node index {nodeIndex} not found in cluster '{clusterName}'");

        return CreateConnection(clusterConfig.DatabaseType, nodeConfig.ConnectionString);
    }

    /// <inheritdoc />
    public async Task<IDbConnection> GetOpenConnectionAsync(string clusterName, int nodeIndex = 0, CancellationToken cancellationToken = default)
    {
        var connection = GetConnection(clusterName, nodeIndex);

        if (connection is System.Data.Common.DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else
        {
            connection.Open();
        }

        return connection;
    }

    /// <inheritdoc />
    public IDbConnection GetConnectionByRoute(RoutingResult routingResult)
    {
        if (!routingResult.IsSuccess)
        {
            throw new InvalidOperationException($"Routing failed: {routingResult.ErrorMessage}");
        }

        var clusterConfig = GetClusterConfig(routingResult.ClusterName);
        if (clusterConfig == null)
        {
            throw new InvalidOperationException($"Cluster '{routingResult.ClusterName}' not found");
        }

        return CreateConnection(clusterConfig.DatabaseType, routingResult.ConnectionString);
    }

    /// <inheritdoc />
    public async Task<IDbConnection> GetOpenConnectionByRouteAsync(RoutingResult routingResult, CancellationToken cancellationToken = default)
    {
        var connection = GetConnectionByRoute(routingResult);

        if (connection is System.Data.Common.DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else
        {
            connection.Open();
        }

        return connection;
    }

    /// <inheritdoc />
    public IDbConnection GetMasterConnection(string clusterName)
    {
        var clusterConfig = GetClusterConfig(clusterName);
        if (clusterConfig == null)
        {
            throw new InvalidOperationException($"Cluster '{clusterName}' not found");
        }

        var masterNode = clusterConfig.Nodes.FirstOrDefault(n => n.IsMaster)
            ?? clusterConfig.Nodes.First();

        return CreateConnection(clusterConfig.DatabaseType, masterNode.ConnectionString);
    }

    /// <inheritdoc />
    public IDbConnection GetSlaveConnection(string clusterName)
    {
        var clusterConfig = GetClusterConfig(clusterName);
        if (clusterConfig == null)
        {
            throw new InvalidOperationException($"Cluster '{clusterName}' not found");
        }

        // 如果没有配置从节点或读写分离未启用，返回主节点
        var slaveNodes = clusterConfig.Nodes.Where(n => !n.IsMaster).ToList();
        if (slaveNodes.Count == 0)
        {
            return GetMasterConnection(clusterName);
        }

        // 根据负载均衡策略选择从节点
        var strategy = clusterConfig.ReadWriteSplitting?.LoadBalanceStrategy ?? "Random";
        var selectedNode = SelectNodeByStrategy(slaveNodes, strategy);

        return CreateConnection(clusterConfig.DatabaseType, selectedNode.ConnectionString);
    }

    /// <inheritdoc />
    public DatabaseClusterConfig? GetClusterConfig(string clusterName)
    {
        _options.Clusters.TryGetValue(clusterName, out var config);
        return config;
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(string clusterName, int nodeIndex = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var clusterConfig = GetClusterConfig(clusterName);
            if (clusterConfig == null)
            {
                return false;
            }

            var nodeConfig = clusterConfig.Nodes.FirstOrDefault(n => n.NodeIndex == nodeIndex);
            if (nodeConfig == null)
            {
                return false;
            }

            using var connection = CreateConnection(clusterConfig.DatabaseType, nodeConfig.ConnectionString);

            if (connection is System.Data.Common.DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken);
            }
            else
            {
                connection.Open();
            }

            return connection.State == ConnectionState.Open;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test connection for cluster '{ClusterName}', node {NodeIndex}", clusterName, nodeIndex);
            return false;
        }
    }

    private IDbConnection CreateConnection(string databaseType, string connectionString)
    {
        if (!_connectionFactories.TryGetValue(databaseType, out var factory))
        {
            throw new NotSupportedException($"Database type '{databaseType}' is not supported");
        }

        // 使用反射或其他方式创建连接
        // 这里简化处理，实际应该根据 factory 创建
        return databaseType.ToLowerInvariant() switch
        {
            "postgresql" => CreatePostgreSqlConnection(connectionString),
            "mysql" => CreateMySqlConnection(connectionString),
            "sqlite" => CreateSqliteConnection(connectionString),
            _ => throw new NotSupportedException($"Database type '{databaseType}' is not supported")
        };
    }

    private static IDbConnection CreatePostgreSqlConnection(string connectionString)
    {
        // 使用 Npgsql
        var type = Type.GetType("Npgsql.NpgsqlConnection, Npgsql");
        if (type == null)
        {
            throw new InvalidOperationException("Npgsql is not installed. Please install the Npgsql package.");
        }

        return (IDbConnection)Activator.CreateInstance(type, connectionString)!;
    }

    private static IDbConnection CreateMySqlConnection(string connectionString)
    {
        // 使用 MySqlConnector
        var type = Type.GetType("MySqlConnector.MySqlConnection, MySqlConnector");
        if (type == null)
        {
            throw new InvalidOperationException("MySqlConnector is not installed. Please install the MySqlConnector package.");
        }

        return (IDbConnection)Activator.CreateInstance(type, connectionString)!;
    }

    private static IDbConnection CreateSqliteConnection(string connectionString)
    {
        // 使用 Microsoft.Data.Sqlite
        var type = Type.GetType("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite");
        if (type == null)
        {
            throw new InvalidOperationException("Microsoft.Data.Sqlite is not installed. Please install the Microsoft.Data.Sqlite package.");
        }

        return (IDbConnection)Activator.CreateInstance(type, connectionString)!;
    }

    private DatabaseNodeConfig SelectNodeByStrategy(List<DatabaseNodeConfig> nodes, string strategy)
    {
        return strategy.ToLowerInvariant() switch
        {
            "roundrobin" => SelectByRoundRobin(nodes),
            "weighted" => SelectByWeighted(nodes),
            _ => SelectByRandom(nodes)
        };
    }

    private DatabaseNodeConfig SelectByRandom(List<DatabaseNodeConfig> nodes)
    {
        var index = _random.Next(nodes.Count);
        return nodes[index];
    }

    private int _roundRobinIndex = -1;

    private DatabaseNodeConfig SelectByRoundRobin(List<DatabaseNodeConfig> nodes)
    {
        var index = Interlocked.Increment(ref _roundRobinIndex) % nodes.Count;
        return nodes[index];
    }

    private DatabaseNodeConfig SelectByWeighted(List<DatabaseNodeConfig> nodes)
    {
        var totalWeight = nodes.Sum(n => n.Weight);
        var randomValue = _random.Next(totalWeight);

        var currentWeight = 0;
        foreach (var node in nodes)
        {
            currentWeight += node.Weight;
            if (randomValue < currentWeight)
            {
                return node;
            }
        }

        return nodes.Last();
    }
}
