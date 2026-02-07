# JZVerse.MicroHuaxia.MessageQueue - 自研消息队列设计规范

## 概述

设计一个对标 RabbitMQ、Kafka、RocketMQ、ZeroMQ、ActiveMQ 的自研消息队列系统，集成各家优点，冲突部分设为可选配置。

### 本次实现范围

**Phase 1-3: 高级特性版本**
- Phase 1: 基础框架 + 内存存储 + 进程内协议
- Phase 2: FileLog 持久化 + TCP/gRPC 双协议 + 单节点 Broker
- Phase 3: 事务消息 + 延迟消息 + 消息轨迹追踪

**命名**: `JZVerse.MicroHuaxia.MessageQueue`
**协议**: TCP 自定义协议 + gRPC 双协议支持

---

### 设计目标

| 特性维度 | 设计决策 | 参考产品 |
|---------|---------|---------|
| 部署模式 | Broker + Brokerless 双模式 | Kafka + ZeroMQ |
| 消费模式 | Push + Pull 双支持 | RabbitMQ + Kafka |
| 存储模式 | 内存 + 日志 + 可选外部存储 | RabbitMQ + Kafka |
| 高级特性 | 事务消息、延迟消息、消息轨迹、流处理 | RocketMQ + Kafka |

---

## 一、模块分层架构

```
JZVerse.MicroHuaxia.MessageQueue/
├── Abstractions        # 核心接口定义
├── Core                # 业务逻辑实现
├── Protocol            # 协议抽象层
│   ├── Protocol.InProc   # 进程内直连
│   ├── Protocol.Tcp      # TCP 自定义协议
│   ├── Protocol.Grpc     # gRPC 协议
│   └── Protocol.Http     # HTTP REST 协议
├── Storage             # 存储抽象层
│   ├── Storage.Memory    # 内存存储
│   ├── Storage.FileLog   # 日志文件存储 (Kafka风格)
│   └── Storage.Hybrid    # 混合存储策略
├── Server              # Broker 服务端
├── Client              # 客户端 SDK
├── AspNetCore          # ASP.NET Core 集成
└── Cluster             # 集群高可用
```

---

## 二、核心接口定义

### 2.1 消息模型

```csharp
// Abstractions/Models/Message.cs
public interface IMessage
{
    string MessageId { get; }           // 全局唯一ID
    string Topic { get; }               // 主题
    string? Tag { get; }                // 标签（过滤用）
    byte[] Body { get; }                // 消息体
    IDictionary<string, string> Headers { get; } // 元数据
    DateTimeOffset Timestamp { get; }   // 生产时间
    string? PartitionKey { get; }       // 分区键（顺序保证）
    int? DelaySeconds { get; }          // 延迟投递（秒）
    int? ExpireSeconds { get; }         // 过期时间
    string? TransactionId { get; }      // 事务ID
}

// Abstractions/Models/MessageEnvelope.cs
public interface IMessageEnvelope
{
    IMessage Message { get; }
    int DeliveryCount { get; }          // 投递次数
    DateTimeOffset? LastDeliveryTime { get; }
    TraceContext? TraceContext { get; } // 链路追踪
    RoutingInfo Routing { get; }        // 路由信息
}
```

### 2.2 生产者接口

```csharp
// Abstractions/IMessageProducer.cs
public interface IMessageProducer
{
    Task<SendResult> SendAsync(IMessage message, SendOptions? options = null, CancellationToken ct = default);
    Task<SendResult> SendBatchAsync(IEnumerable<IMessage> messages, SendOptions? options = null, CancellationToken ct = default);
    Task<SendResult> SendDelayedAsync(IMessage message, TimeSpan delay, CancellationToken ct = default);
}

// Abstractions/ITransactionProducer.cs (事务消息)
public interface ITransactionProducer
{
    Task<TransactionResult> PrepareAsync(IMessage message, CancellationToken ct = default);
    Task CommitAsync(string transactionId, CancellationToken ct = default);
    Task RollbackAsync(string transactionId, CancellationToken ct = default);
}
```

### 2.3 消费者接口

```csharp
// Abstractions/IMessageConsumer.cs
public interface IMessageConsumer
{
    // Push 模式
    Task SubscribeAsync(string topic, Func<IMessageEnvelope, CancellationToken, Task<ConsumeResult>> handler, ConsumeOptions? options = null, CancellationToken ct = default);
    Task UnsubscribeAsync(string topic, CancellationToken ct = default);
    
    // Pull 模式
    Task<IReadOnlyList<IMessageEnvelope>> PullAsync(string topic, int batchSize = 10, CancellationToken ct = default);
    
    // 确认
    Task AcknowledgeAsync(string messageId, CancellationToken ct = default);
    Task NegativeAcknowledgeAsync(string messageId, CancellationToken ct = default);
}

// Abstractions/IStreamConsumer.cs (流处理)
public interface IStreamConsumer
{
    IAsyncEnumerable<IMessageEnvelope> CreateStreamAsync(string topic, CancellationToken ct = default);
    IAsyncEnumerable<TAggregated> AggregateAsync<TKey, TAggregated>(string topic, Func<IMessage, TKey> keySelector, Func<IEnumerable<IMessage>, TAggregated> aggregator, WindowOptions window, CancellationToken ct = default);
}
```

### 2.4 路由接口 (RabbitMQ 风格)

```csharp
// Abstractions/Routing/IExchange.cs
public interface IExchange
{
    string Name { get; }
    ExchangeType Type { get; }  // Direct, Topic, Fanout, Headers
    
    Task BindQueueAsync(string queueName, string routingKey, CancellationToken ct = default);
    Task UnbindQueueAsync(string queueName, string routingKey, CancellationToken ct = default);
    Task PublishAsync(IMessage message, string routingKey, CancellationToken ct = default);
}

public enum ExchangeType { Direct, Topic, Fanout, Headers }
```

### 2.5 存储接口

```csharp
// Abstractions/Storage/IMessageStore.cs
public interface IMessageStore
{
    Task<long> AppendAsync(IMessage message, CancellationToken ct = default);
    Task<IMessage?> GetByIdAsync(string messageId, CancellationToken ct = default);
    Task<IReadOnlyList<IMessage>> GetByOffsetAsync(string topic, int partition, long offset, int count, CancellationToken ct = default);
    Task<bool> DeleteAsync(string messageId, CancellationToken ct = default);
}

// Abstractions/Storage/IOffsetManager.cs
public interface IOffsetManager
{
    Task<long> GetOffsetAsync(string consumerGroup, string topic, int partition, CancellationToken ct = default);
    Task CommitOffsetAsync(string consumerGroup, string topic, int partition, long offset, CancellationToken ct = default);
    Task ResetOffsetAsync(string consumerGroup, string topic, int partition, long offset, CancellationToken ct = default);
}
```

---

## 三、存储层设计

### 3.1 混合存储架构

```
L1: Memory Queue (热数据)
    - System.Threading.Channels 实现
    - LRU 缓存热点消息
    - 容量可配（默认 10 万条）

L2: WAL (Write-Ahead Log)
    - 所有消息先写 WAL
    - 保证崩溃恢复
    - 顺序追加写入

L3: FileLog (持久化)
    - 日志段文件（默认 1GB/段）
    - 稀疏索引（每 4KB 一个索引点）
    - 支持 mmap 零拷贝读取

L4: Archive (归档)
    - 冷数据压缩归档
    - 可选对象存储（S3 等）
```

### 3.2 FileLog 目录结构

```
/data/messagequeue/
├── topic-orders-0/              # 主题-分区
│   ├── 00000000000000000000.log   # 日志段
│   ├── 00000000000000000000.index # 偏移索引
│   ├── 00000000000000000000.timeindex # 时间索引
│   └── ...
├── __transaction_half/          # 半消息存储
├── __delay_queue/               # 延迟队列
├── __dead_letter/               # 死信队列
└── .metadata/
    ├── consumer-offsets.db      # 消费位点
    └── topics.json              # 主题元数据
```

---

## 四、协议层设计

### 4.1 协议矩阵

| 协议 | 场景 | 序列化 | 性能 |
|-----|------|-------|------|
| InProc | 进程内 | 直接引用 | 极高 |
| TCP | 高性能 Broker | 自定义二进制 | 高 |
| gRPC | 微服务互通 | Protobuf | 高 |
| HTTP | 跨语言兼容 | JSON | 中 |

### 4.2 TCP 自定义协议帧格式

```
+--------+--------+--------+--------+--------+--------+
| Magic  | Version| Type   | Length | Header | Body   |
| 2B     | 1B     | 1B     | 4B     | NB     | MB     |
+--------+--------+--------+--------+--------+--------+
Magic: 0xAB 0xCD
Type: Publish=0x01, Subscribe=0x02, Ack=0x03, Heartbeat=0x04, Transaction=0x05
```

---

## 五、高级特性实现

### 5.1 事务消息（两阶段提交）

```
1. Producer → Prepare(HalfMessage) → Broker
2. Broker → 存储到 __transaction_half 主题
3. Producer ← PrepareResult
4. Producer → 执行本地事务
5a. 成功 → Commit(transactionId) → 消息转正常队列
5b. 失败 → Rollback(transactionId) → 删除半消息
6. 超时回查 → Broker 回调 Producer 检查事务状态
```

### 5.2 延迟消息（时间轮）

```
L1: 秒级时间轮 (60 slots, 1s/slot)
L2: 分钟级时间轮 (60 slots, 1m/slot)
L3: 小时级时间轮 (24 slots, 1h/slot)
L4: 天级延迟队列

预设延迟级别: 1s, 5s, 10s, 30s, 1m, 2m, 5m, 10m, 30m, 1h, 2h, 6h, 12h, 24h
```

### 5.3 消息轨迹追踪

```
Trace Points:
├── Born: 消息ID生成、生产者信息、时间戳
├── Store: 存储位置、分区、偏移量
├── Dispatch: 路由决策、订阅者列表
├── Consume: 消费者信息、消费时间、耗时
└── Ack/Nack: 确认状态、重试次数

存储: __trace 主题 + OpenTelemetry 集成
```

### 5.4 流处理能力

```csharp
// 窗口类型
public enum WindowType { Tumbling, Sliding, Session }

// 聚合操作
await consumer.AggregateAsync(
    topic: "orders",
    keySelector: msg => msg.Headers["user_id"],
    aggregator: msgs => new { Count = msgs.Count(), Sum = msgs.Sum(m => GetAmount(m)) },
    window: WindowOptions.Tumbling(TimeSpan.FromMinutes(5))
);
```

---

## 六、集群高可用

### 6.1 集群架构

```
┌─────────────────┐
│ Service Discovery│ (注册中心)
└────────┬─────────┘
         │
    ┌────┼────┐
    │    │    │
┌───▼──┐ ┌▼───▼─┐ ┌──▼───┐
│Broker│◄─►Broker│◄─►Broker│
│Leader│ │Follower│ │Follower│
└──────┘ └───────┘ └──────┘
    │         │         │
    └── Raft 共识协议 ───┘
```

### 6.2 复制策略

| 策略 | 说明 | 适用场景 |
|-----|------|---------|
| 同步复制 | Leader 等待所有 Follower 确认 | 强一致性要求 |
| 异步复制 | Leader 写入即返回 | 高性能要求 |
| 半同步 | Leader 等待多数确认 | 平衡方案（推荐） |

---

## 七、ASP.NET Core 集成

```csharp
// DI 注册
services.AddMessageQueue(options =>
{
    options.Mode = MessageQueueMode.Broker; // 或 Brokerless
    options.BrokerEndpoints = ["localhost:9092"];
    options.Protocol = ProtocolType.Tcp;
    options.Storage = StorageType.FileLog;
    options.EnableTransactional = true;
    options.EnableTracing = true;
});

// 生产者注入
public class OrderService(IMessageProducer producer)
{
    public async Task CreateOrderAsync(Order order)
    {
        await producer.SendAsync(MessageBuilder
            .Topic("orders")
            .Tag("created")
            .Body(order)
            .Build());
    }
}

// 消费者后台服务
services.AddMessageQueueConsumer<OrderConsumer>(options =>
{
    options.Topic = "orders";
    options.ConsumerGroup = "order-processor";
    options.ConsumeMode = ConsumeMode.Push;
    options.MaxRetries = 3;
});
```

---

## 八、实现路线图

### Phase 1: 基础框架 ✅ 本次实现
- [ ] 项目结构搭建（Abstractions/Core/Protocol/Storage/Server/Client/AspNetCore）
- [ ] 核心接口定义（IMessage, IMessageProducer, IMessageConsumer, IExchange）
- [ ] 内存存储实现（Storage.Memory - 基于 System.Threading.Channels）
- [ ] 进程内协议（Protocol.InProc）
- [ ] 基础路由引擎（Direct/Topic/Fanout/Headers 四种 Exchange）
- [ ] ASP.NET Core 集成扩展
- [ ] 单元测试框架

### Phase 2: 持久化和网络 ✅ 本次实现
- [ ] FileLog 存储实现（WAL + 日志段 + 稀疏索引）
- [ ] TCP 自定义协议实现（二进制帧协议）
- [ ] gRPC 协议实现（复用现有 Protos 基础设施）
- [ ] Broker Server 单节点实现
- [ ] 网络客户端（Producer/Consumer Client）
- [ ] 消费者组和偏移量管理
- [ ] 消息确认（ACK/NACK）和重试机制

### Phase 3: 高级特性 ✅ 本次实现
- [ ] 事务消息（两阶段提交 + 事务回查机制）
- [ ] 延迟消息（时间轮调度器 + 预设延迟级别）
- [ ] 消息过滤（Tag 过滤 + SQL92 表达式）
- [ ] 消息轨迹追踪（全链路 + OpenTelemetry 集成）
- [ ] 死信队列（DLQ）
- [ ] 优先级队列

### Phase 4: 集群高可用 (未来版本)
- [ ] Raft 一致性协议
- [ ] 分区和副本管理
- [ ] 主从复制和故障转移
- [ ] 客户端负载均衡
- [ ] 监控指标和管理 API

### Phase 5: 流处理和优化 (未来版本)
- [ ] 流处理 API（窗口聚合）
- [ ] 零拷贝优化（mmap/Span<T>）
- [ ] 批量发送/消费
- [ ] 消息压缩（LZ4/Zstd）
- [ ] Brokerless 模式实现
- [ ] 性能基准测试

---

## 九、关键参考文件

| 文件 | 用途 |
|------|------|
| `ServiceCommunication.Abstractions/Messaging/IMessageSender.cs` | 现有消息接口参考 |
| `ServiceDiscovery.Abstractions/IServiceEventPublisher.cs` | 发布订阅模式参考 |
| `ServiceDiscovery.Abstractions/Persistence/IServiceInstanceStore.cs` | 持久化接口参考 |
| `ServiceCommunication.Core/LoadBalancing/LoadBalancerFactory.cs` | 工厂模式参考 |
| `Gateway.Core/Authentication/AuthenticationPipeline.cs` | 管道模式参考 |

---

## 十、特性对比矩阵

| 特性 | RabbitMQ | Kafka | RocketMQ | ZeroMQ | JZVerse.MQ |
|-----|---------|-------|----------|--------|------------|
| Broker 模式 | O | O | O | X | O |
| Brokerless | X | X | X | O | O |
| Push 消费 | O | X | O | X | O |
| Pull 消费 | X | O | O | X | O |
| 事务消息 | O | X | O | X | O |
| 延迟消息 | O (插件) | X | O | X | O |
| 消息轨迹 | X | X | O | X | O |
| 流处理 | X | O | X | X | O |
| Exchange 路由 | O | X | X | X | O |
| 消费者组 | X | O | O | X | O |
| 日志持久化 | X | O | O | X | O |
| 消息回溯 | X | O | O | X | O |

---

## 验证方案

1. **单元测试**: 每个模块独立测试（存储、协议、路由）
2. **集成测试**: 端到端消息发送/接收/确认流程
3. **性能测试**: TPS 基准测试（对比 RabbitMQ/Kafka）
4. **故障测试**: Broker 故障转移、网络分区恢复
5. **压力测试**: 长时间高负载运行稳定性
