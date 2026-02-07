# Saga 协调式分布式事务实现计划

## 设计概述

采用 **Saga 协调式模式**，由中央协调器编排多个服务的本地事务，失败时自动反向补偿。MQ 集成通过配置可选（HTTP 直连 / 消息队列）。

**与现有事务消息的关系**：
- 事务消息：解决单服务的本地事务与消息发送原子性
- Saga：解决跨多服务的分布式事务协调

---

## 模块结构

```
src/JZVerse.MicroHuaxia/
├── JZVerse.MicroHuaxia.Saga.Abstractions/     # 接口层
│   ├── ISagaDefinition.cs                     # Saga 定义
│   ├── ISagaStep.cs                           # 步骤定义（正向+补偿）
│   ├── ISagaOrchestrator.cs                   # 协调器接口
│   ├── ISagaStore.cs                          # 持久化接口
│   ├── ISagaCommunicationAdapter.cs           # 通信适配器
│   └── Models/                                # 数据模型
│       ├── SagaInstance.cs
│       ├── SagaStepInstance.cs
│       ├── SagaContext.cs
│       ├── SagaStatus.cs
│       └── StepStatus.cs
│
├── JZVerse.MicroHuaxia.Saga.Core/             # 核心实现
│   ├── Orchestration/
│   │   ├── SagaOrchestrator.cs                # 协调器
│   │   └── SagaStateMachine.cs                # 状态机
│   ├── Compensation/
│   │   └── CompensationEngine.cs              # 补偿引擎
│   ├── Storage/
│   │   └── MemorySagaStore.cs                 # 内存存储
│   ├── Communication/
│   │   ├── HttpSagaAdapter.cs                 # HTTP 通信
│   │   └── MqSagaAdapter.cs                   # MQ 通信
│   ├── BackgroundServices/
│   │   ├── SagaTimeoutService.cs              # 超时检测
│   │   └── SagaRecoveryService.cs             # 故障恢复
│   └── SagaCoreExtensions.cs
│
└── JZVerse.MicroHuaxia.Saga.AspNetCore/       # ASP.NET Core 集成
    ├── SagaOptions.cs
    └── SagaExtensions.cs

tests/JZVerse.MicroHuaxia.Saga.Tests.Unit/     # 单元测试
```

---

## 核心接口

### ISagaDefinition - Saga 定义
```csharp
public interface ISagaDefinition
{
    string SagaId { get; }
    string Name { get; }
    IReadOnlyList<ISagaStep> Steps { get; }
    TimeSpan? Timeout { get; }
    CompensationStrategy Strategy { get; }
}
```

### ISagaStep - 步骤定义
```csharp
public interface ISagaStep
{
    string StepId { get; }
    string Name { get; }
    int Order { get; }
    Func<SagaContext, CancellationToken, Task<StepExecutionResult>> ExecuteAsync { get; }
    Func<SagaContext, CancellationToken, Task<CompensationResult>>? CompensateAsync { get; }
    bool IsCompensable { get; }
    RetryPolicy? RetryPolicy { get; }
}
```

### ISagaOrchestrator - 协调器
```csharp
public interface ISagaOrchestrator
{
    Task<string> StartAsync(ISagaDefinition saga, SagaContext context, CancellationToken ct = default);
    Task<bool> ResumeAsync(string sagaInstanceId, CancellationToken ct = default);
    Task CompensateAsync(string sagaInstanceId, CancellationToken ct = default);
    Task<SagaInstance?> GetInstanceAsync(string sagaInstanceId, CancellationToken ct = default);
    Task<bool> CancelAsync(string sagaInstanceId, CancellationToken ct = default);
}
```

### ISagaStore - 持久化
```csharp
public interface ISagaStore
{
    Task SaveAsync(SagaInstance instance, CancellationToken ct = default);
    Task<SagaInstance?> GetAsync(string sagaInstanceId, CancellationToken ct = default);
    Task UpdateStatusAsync(string sagaInstanceId, SagaStatus status, CancellationToken ct = default);
    Task UpdateStepStatusAsync(string sagaInstanceId, string stepId, StepStatus status, object? result = null, CancellationToken ct = default);
    Task<IReadOnlyList<SagaInstance>> GetPendingInstancesAsync(CancellationToken ct = default);
}
```

---

## 状态机

### SagaStatus
```
Pending → Executing → Completed (成功)
             ↓
        Compensating → Compensated (补偿完成)
             ↓
          Failed (补偿失败)
```

### StepStatus
```
Pending → Executing → Completed
              ↓
           Failed → Compensating → Compensated
                         ↓
                  CompensationFailed
```

---

## DI 注册

```csharp
// 基础使用
services.AddSaga(options =>
{
    options.Storage = SagaStorageType.Memory;
    options.CommunicationMode = SagaCommunicationMode.Http;
    options.EnableRecovery = true;
});

// 使用 MQ 通信
services.AddSaga(options =>
{
    options.CommunicationMode = SagaCommunicationMode.Mq;
    options.MqOptions = new MqAdapterOptions
    {
        ReplyQueuePrefix = "saga-reply",
        RequestTimeout = TimeSpan.FromSeconds(30)
    };
});

// 注册 Saga 定义
services.AddSagaDefinition<OrderSagaDefinition>();
```

---

## 实现步骤

### Phase 1: 核心框架
1. 创建 `Saga.Abstractions` 项目，定义所有接口和枚举
2. 创建 `Saga.Core` 项目，实现：
   - `SagaOrchestrator` - 协调器核心逻辑
   - `SagaStateMachine` - 状态转换
   - `CompensationEngine` - 补偿执行
   - `MemorySagaStore` - 内存存储
3. 编写单元测试

### Phase 2: 通信适配器
1. 实现 `HttpSagaAdapter` - HTTP 直接调用
2. 实现 `MqSagaAdapter` - 集成现有消息队列（请求-响应模式）

### Phase 3: 后台服务
1. `SagaTimeoutService` - 检测超时 Saga
2. `SagaRecoveryService` - 恢复中断的 Saga

### Phase 4: ASP.NET Core 集成
1. `SagaOptions` 配置类
2. `SagaExtensions` 扩展方法

---

## 关键实现文件

| 文件 | 职责 |
|------|------|
| `Saga.Abstractions/ISagaOrchestrator.cs` | 协调器接口 |
| `Saga.Core/Orchestration/SagaOrchestrator.cs` | 协调器实现（核心） |
| `Saga.Core/Compensation/CompensationEngine.cs` | 补偿引擎（反向执行） |
| `Saga.Core/Communication/MqSagaAdapter.cs` | MQ 通信适配 |
| `Saga.AspNetCore/SagaExtensions.cs` | DI 注册入口 |

---

## 验证方法

### 单元测试
```bash
dotnet test tests/JZVerse.MicroHuaxia.Saga.Tests.Unit/
```

测试用例：
- 状态机正确转换
- 补偿操作反向执行顺序
- 步骤重试策略
- 存储层并发安全

### 集成测试
- 模拟 3 步骤 Saga 完整流程（成功/失败/补偿）
- MQ 通信请求-响应模式
- 故障恢复场景

---

## 使用示例

```csharp
// 定义 Saga
public class OrderSagaDefinition : ISagaDefinition
{
    public string SagaId => "order-saga";
    public IReadOnlyList<ISagaStep> Steps => new[]
    {
        new SagaStep("reserve-inventory", ReserveInventory, ReleaseInventory),
        new SagaStep("process-payment", ProcessPayment, RefundPayment),
        new SagaStep("create-shipment", CreateShipment, compensate: null)
    };
}

// 使用
var sagaInstanceId = await _orchestrator.StartAsync(
    _orderSaga,
    new SagaContext { Data = { ["OrderId"] = orderId } }
);
```
