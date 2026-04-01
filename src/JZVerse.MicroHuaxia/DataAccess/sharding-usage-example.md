# 分库分表使用示例

## 1. 配置 appsettings.json

```json
{
  "DataAccess": {
    "DefaultConnectionName": "default",
    "Clusters": {
      "orderCluster": {
        "Name": "orderCluster",
        "DatabaseType": "PostgreSql",
        "Nodes": [
          {
            "Name": "order-db-0",
            "NodeIndex": 0,
            "ConnectionString": "Host=localhost;Port=5432;Database=orders_0;Username=postgres;Password=xxx",
            "IsMaster": true
          },
          {
            "Name": "order-db-1",
            "NodeIndex": 1,
            "ConnectionString": "Host=localhost;Port=5432;Database=orders_1;Username=postgres;Password=xxx",
            "IsMaster": true
          }
        ]
      }
    },
    "ShardingRules": {
      "orders": {
        "Name": "orders",
        "Strategy": "Hash",
        "ShardingKey": "UserId",
        "ShardCount": 16,
        "TableNameFormat": "orders_{0:000}",
        "ClusterName": "orderCluster"
      }
    }
  }
}
```

## 2. 注册服务

```csharp
// Program.cs
builder.Services.AddShardingDataAccess(builder.Configuration);

// 或者使用代码配置
builder.Services.AddShardingDataAccess(options =>
{
    options.DefaultConnectionName = "default";
    options.Clusters.Add("orderCluster", new DatabaseClusterConfig
    {
        Name = "orderCluster",
        DatabaseType = "PostgreSql",
        Nodes = new List<DatabaseNodeConfig>
        {
            new DatabaseNodeConfig { Name = "db0", NodeIndex = 0, ConnectionString = "...", IsMaster = true },
            new DatabaseNodeConfig { Name = "db1", NodeIndex = 1, ConnectionString = "...", IsMaster = true }
        }
    });
});
```

## 3. 定义分片实体

```csharp
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;

// 方式1: 使用特性标记（推荐）
[DatabaseRoute("orderCluster")]
[TableRoute("orders", Strategy = ShardingStrategyType.Hash, ShardingKey = "UserId", ShardCount = 16)]
public class Order
{
    public Guid Id { get; set; }
    
    [ShardingKey]  // 标记分片键
    public Guid UserId { get; set; }
    
    public string OrderNo { get; set; } = string.Empty;
    
    public decimal TotalAmount { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    [DbIgnore]  // 不持久化此字段
    public List<OrderItem> Items { get; set; } = new();
}

// 方式2: 仅使用 DatabaseRoute，分片规则从配置加载
[DatabaseRoute("logCluster", UseSharding = true)]
public class SystemLog
{
    public Guid Id { get; set; }
    
    public string Level { get; set; } = string.Empty;
    
    public string Message { get; set; } = string.Empty;
    
    [ShardingKey]
    public DateTime CreatedAt { get; set; }  // 按时间分片
}

// 方式3: 不分片，只指定数据库集群
[DatabaseRoute("default", UseSharding = false)]
public class Config
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
```

## 4. 使用 IShardingDbExecutor

### 4.1 基本 CRUD

```csharp
public class OrderService
{
    private readonly IShardingDbExecutor _dbExecutor;

    public OrderService(IShardingDbExecutor dbExecutor)
    {
        _dbExecutor = dbExecutor;
    }

    // 插入订单（自动路由到对应分片）
    public async Task<Guid> CreateOrderAsync(Guid userId, List<OrderItem> items)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,  // 分片键
            OrderNo = GenerateOrderNo(),
            TotalAmount = items.Sum(i => i.Price * i.Quantity),
            CreatedAt = DateTime.UtcNow
        };

        await _dbExecutor.InsertAsync(order);
        return order.Id;
    }

    // 查询指定用户的订单（自动路由）
    public async Task<IEnumerable<Order>> GetUserOrdersAsync(Guid userId)
    {
        // 使用分片键路由到对应分片查询
        return await _dbExecutor.QueryAsync<Order>(
            shardingKey: userId,  // 分片键值
            sql: "SELECT * FROM orders WHERE UserId = @UserId ORDER BY CreatedAt DESC",
            param: new { UserId = userId });
    }

    // 根据订单ID查询（需要传入分片键）
    public async Task<Order?> GetOrderAsync(Guid orderId, Guid userId)
    {
        return await _dbExecutor.QuerySingleOrDefaultAsync<Order>(
            shardingKey: userId,
            sql: "SELECT * FROM orders WHERE Id = @Id",
            param: new { Id = orderId });
    }

    // 删除订单
    public async Task<bool> DeleteOrderAsync(Guid orderId, Guid userId)
    {
        var order = new Order { Id = orderId, UserId = userId };
        var affected = await _dbExecutor.DeleteAsync(order);
        return affected > 0;
    }
}
```

### 4.2 批量操作

```csharp
public class BatchOrderService
{
    private readonly IShardingDbExecutor _dbExecutor;

    public BatchOrderService(IShardingDbExecutor dbExecutor)
    {
        _dbExecutor = dbExecutor;
    }

    // 批量插入（自动按分片分组并行插入）
    public async Task<int> BatchCreateOrdersAsync(List<Order> orders)
    {
        return await _dbExecutor.InsertBatchAsync(orders);
    }

    // 跨分片查询所有订单（性能较低，谨慎使用）
    public async Task<IEnumerable<Order>> GetAllOrdersAsync()
    {
        return await _dbExecutor.QueryAllShardsAsync<Order>(
            "SELECT * FROM orders WHERE CreatedAt >= @StartDate",
            new { StartDate = DateTime.UtcNow.AddDays(-7) });
    }
}
```

### 4.3 手动设置路由上下文

```csharp
public class ComplexQueryService
{
    private readonly IShardingDbExecutor _dbExecutor;

    public ComplexQueryService(IShardingDbExecutor dbExecutor)
    {
        _dbExecutor = dbExecutor;
    }

    public async Task<IEnumerable<Order>> ComplexQueryAsync(Guid userId)
    {
        // 设置路由上下文
        _dbExecutor.SetRoutingContext(new RoutingContext
        {
            EntityType = typeof(Order),
            ShardingKey = userId,
            OperationType = DataOperationType.Query
        });

        try
        {
            // 后续查询会自动使用设置的路由上下文
            var orders = await _dbExecutor.QueryAsync<Order>(
                "SELECT * FROM orders WHERE UserId = @UserId",
                new { UserId = userId });

            // 可以在同一个分片内执行多个查询
            var count = await _dbExecutor.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM orders WHERE UserId = @UserId",
                new { UserId = userId });

            return orders;
        }
        finally
        {
            // 清除路由上下文
            _dbExecutor.ClearRoutingContext();
        }
    }
}
```

### 4.4 事务操作

```csharp
public class OrderTransactionService
{
    private readonly IShardingDbExecutor _dbExecutor;

    public async Task<bool> TransferOrderAsync(Guid orderId, Guid fromUserId, Guid toUserId)
    {
        var order = await _dbExecutor.QuerySingleOrDefaultAsync<Order>(
            fromUserId,
            "SELECT * FROM orders WHERE Id = @Id",
            new { Id = orderId });

        if (order == null) return false;

        // 如果分片不变，使用普通事务
        if (CalculateShard(fromUserId) == CalculateShard(toUserId))
        {
            return await _dbExecutor.WithTransactionAsync<Order, bool>(order, async transaction =>
            {
                // 在同一个分片内执行更新
                await _dbExecutor.ExecuteAsync(
                    order,
                    "UPDATE orders SET UserId = @NewUserId WHERE Id = @Id",
                    new { Id = orderId, NewUserId = toUserId });

                return true;
            });
        }
        else
        {
            // 跨分片操作需要应用层处理
            // 1. 在原分片删除
            // 2. 在新分片插入
            // 这需要分布式事务支持
            throw new NotSupportedException("Cross-shard transfer requires distributed transaction");
        }
    }

    private int CalculateShard(Guid userId)
    {
        return Math.Abs(userId.GetHashCode()) % 16;
    }
}
```

## 5. 使用 IDatabaseRouter 直接路由

```csharp
public class AdvancedService
{
    private readonly IDatabaseRouter _router;
    private readonly IMultiDbConnectionManager _connectionManager;

    public AdvancedService(
        IDatabaseRouter router,
        IMultiDbConnectionManager connectionManager)
    {
        _router = router;
        _connectionManager = connectionManager;
    }

    public async Task<RoutingResult> GetRouteInfoAsync(Guid userId)
    {
        // 获取路由信息
        var route = _router.Route<Order>(userId);
        
        Console.WriteLine($"Cluster: {route.ClusterName}");
        Console.WriteLine($"Node: {route.NodeName} (Index: {route.NodeIndex})");
        Console.WriteLine($"Table: {route.ActualTableName}");
        Console.WriteLine($"Shard: {route.ShardIndex}");

        return route;
    }

    public async Task TestAllConnectionsAsync()
    {
        // 测试所有节点的连接
        var clusters = new[] { "default", "orderCluster", "logCluster" };
        
        foreach (var cluster in clusters)
        {
            var config = _connectionManager.GetClusterConfig(cluster);
            if (config == null) continue;

            for (int i = 0; i < config.Nodes.Count; i++)
            {
                var isConnected = await _connectionManager.TestConnectionAsync(cluster, i);
                Console.WriteLine($"Cluster: {cluster}, Node {i}: {(isConnected ? "OK" : "FAILED")}");
            }
        }
    }
}
```

## 6. 自定义分片策略

```csharp
// 实现自定义分片策略
public class CustomShardingStrategy : IShardingStrategy
{
    public string Name => "Custom";

    public int CalculateShardIndex(object shardingKey, int shardCount)
    {
        // 自定义分片算法
        var hash = GetCustomHash(shardingKey);
        return hash % shardCount;
    }

    public string GenerateTableName(string logicalTableName, object? shardingKey, int shardIndex, string? format = null)
    {
        return $"{logicalTableName}_{shardIndex:D3}";
    }

    private int GetCustomHash(object key)
    {
        // 自定义哈希算法
        return key.GetHashCode();
    }
}

// 注册自定义策略
builder.Services.AddShardingStrategy<CustomShardingStrategy>("Custom");
```

## 7. 配置读写分离

```json
{
  "DataAccess": {
    "Clusters": {
      "userCluster": {
        "Name": "userCluster",
        "DatabaseType": "PostgreSql",
        "Nodes": [
          {
            "Name": "master",
            "NodeIndex": 0,
            "ConnectionString": "Host=master;Port=5432;Database=users;...",
            "IsMaster": true
          },
          {
            "Name": "slave-1",
            "NodeIndex": 1,
            "ConnectionString": "Host=slave1;Port=5432;Database=users;...",
            "IsMaster": false
          },
          {
            "Name": "slave-2",
            "NodeIndex": 2,
            "ConnectionString": "Host=slave2;Port=5432;Database=users;...",
            "IsMaster": false
          }
        ],
        "ReadWriteSplitting": {
          "Enabled": true,
          "SlaveNodeIndexes": [1, 2],
          "LoadBalanceStrategy": "RoundRobin"
        }
      }
    }
  }
}
```

## 8. 注意事项

1. **分片键选择**：分片键一旦确定通常不能更改，选择合适的分片键非常重要
2. **跨分片查询**：尽量避免跨分片查询，性能较差
3. **事务**：跨分片事务需要应用层处理或使用分布式事务
4. **ID 生成**：建议使用雪花算法等分布式 ID 生成器
5. **数据迁移**：分片扩容时需要考虑数据迁移方案
