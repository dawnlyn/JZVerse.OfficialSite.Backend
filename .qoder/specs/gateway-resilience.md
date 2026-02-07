# Gateway 容错弹性能力实现方案

## 概述

为 API Gateway 添加完整的容错弹性能力，包括重试、熔断、舱壁隔离和降级策略。采用"统一通信组件做基础承载 + 容错弹性做独立核心组件"的架构方式。

## 设计原则

### 为什么创建独立的 Gateway.Resilience 模块？

1. **职责分离**：Gateway 面向外部流量，ServiceCommunication 面向内部服务调用，语义不同
2. **扩展性**：Gateway 需要降级（Fallback）和舱壁（Bulkhead），ServiceCommunication 不需要
3. **配置粒度**：Gateway 按路由配置弹性策略，ServiceCommunication 按服务名配置

### 为什么复用 ServiceCommunication 的基础组件？

1. **避免重复造轮子**：熔断器、重试策略的核心逻辑相同
2. **保持一致性**：整个 MicroHuaxia 生态使用相同的弹性实现
3. **减少维护成本**：一处修复，全局受益

## 模块结构

```
JZVerse.MicroHuaxia.Gateway.Resilience/
├── Abstractions/
│   ├── IGatewayResilienceContext.cs     # 弹性执行上下文
│   ├── IFallbackHandler.cs              # 降级策略接口
│   ├── IBulkhead.cs                     # 舱壁隔离接口
│   └── IGatewayResiliencePipeline.cs    # Gateway 弹性管道
├── Fallback/
│   ├── StaticResponseFallbackHandler.cs  # 静态响应降级
│   ├── CachedResponseFallbackHandler.cs  # 缓存降级
│   └── FallbackHandlerRegistry.cs        # 处理器注册表
├── Bulkhead/
│   ├── SemaphoreBulkhead.cs             # 信号量舱壁
│   └── BulkheadFactory.cs               # 舱壁工厂
├── Pipeline/
│   ├── GatewayResiliencePipeline.cs     # 管道实现
│   └── GatewayResiliencePipelineFactory.cs
├── Configuration/
│   └── GatewayResilienceOptions.cs      # 全局配置
└── Extensions/
    └── ServiceCollectionExtensions.cs    # DI 扩展
```

## 依赖关系

```
Gateway.Http ──────────────┐
                           ↓
               Gateway.Resilience ────────┐
                           │              │
                           ↓              ↓
          ServiceCommunication.Core    Gateway.Abstractions
                           │
                           ↓
       ServiceCommunication.Abstractions
```

## 核心接口

### 1. IGatewayResiliencePipeline

```csharp
public interface IGatewayResiliencePipeline
{
    Task<HttpResponseMessage> ExecuteAsync(
        IGatewayResilienceContext context,
        Func<CancellationToken, Task<HttpResponseMessage>> action,
        CancellationToken cancellationToken = default);
}
```

**执行顺序**：舱壁 → 重试 → 熔断 → 超时 → 降级

### 2. IFallbackHandler

```csharp
public interface IFallbackHandler
{
    string Name { get; }
    bool CanHandle(IGatewayResilienceContext context, Exception exception);
    Task<FallbackResult> HandleAsync(IGatewayResilienceContext context, Exception exception, CancellationToken ct);
}
```

**三种实现**：
- `StaticResponseFallbackHandler` - 返回预定义 JSON
- `CachedResponseFallbackHandler` - 返回缓存的最后成功响应
- 自定义处理器 - 通过 DI 注册

### 3. IBulkhead

```csharp
public interface IBulkhead
{
    string Name { get; }
    int MaxConcurrency { get; }
    int CurrentConcurrency { get; }
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
```

## 配置扩展

在 `GatewayRoute` 中添加新配置：

```csharp
// RouteBulkhead
public sealed record RouteBulkhead
{
    public bool Enabled { get; init; } = true;
    public int MaxConcurrency { get; init; } = 100;
    public int MaxQueueLength { get; init; } = 100;
    public TimeSpan QueueTimeout { get; init; } = TimeSpan.FromSeconds(10);
}

// RouteFallback
public sealed record RouteFallback
{
    public bool Enabled { get; init; } = true;
    public FallbackType Type { get; init; } = FallbackType.Static;
    public FallbackStaticResponse? StaticResponse { get; init; }
    public FallbackCacheOptions? CacheOptions { get; init; }
    public string? CustomHandlerName { get; init; }
}
```

## 实现任务

### Phase 1: 基础设施 (Gateway.Resilience 项目)
1. 创建 Gateway.Resilience 项目结构
2. 定义核心接口（IGatewayResilienceContext, IFallbackHandler, IBulkhead, IGatewayResiliencePipeline）
3. 实现 SemaphoreBulkhead 和 BulkheadFactory

### Phase 2: 降级能力
4. 扩展 GatewayRoute 配置（添加 RouteBulkhead 和 RouteFallback）
5. 实现 StaticResponseFallbackHandler
6. 实现 CachedResponseFallbackHandler
7. 实现 FallbackHandlerRegistry

### Phase 3: 弹性管道
8. 实现 GatewayResiliencePipeline（组合所有策略）
9. 实现 GatewayResiliencePipelineFactory
10. 配置 DI 扩展 (AddGatewayResilience)

### Phase 4: 集成
11. 重构 HttpRequestForwarder 使用 IGatewayResiliencePipeline
12. 更新 GatewayAuditMiddleware 记录弹性指标
13. 更新解决方案文件

### Phase 5: 测试
14. 编写 Bulkhead 单元测试
15. 编写 Fallback 处理器单元测试
16. 编写 Pipeline 集成测试
17. 运行所有测试验证

## 关键文件

| 文件 | 操作 | 说明 |
|-----|------|------|
| `Gateway.Resilience/*.cs` | 新建 | 弹性模块所有文件 |
| `Gateway.Abstractions/Routing/GatewayRoute.cs` | 修改 | 添加 Bulkhead 和 Fallback 配置 |
| `Gateway.Http/HttpRequestForwarder.cs` | 修改 | 集成弹性管道 |
| `Gateway.AspNetCore/Middleware/GatewayMiddleware.cs` | 修改 | 更新审计日志字段 |
| `JZVerse.OfficialSite.Backend.slnx` | 修改 | 添加新项目 |

## 执行流程

```
请求 → Bulkhead检查 → 获取信号量
                        ↓
              熔断器状态检查 → 打开则触发降级
                        ↓
              重试循环 → 发送HTTP请求
                        ↓
              成功 → 记录指标 → 释放信号量 → 返回响应
                        ↓
              失败 → 重试/触发熔断 → 执行降级 → 返回降级响应
```

## 验证方式

1. **构建验证**：`dotnet build` 确保无编译错误
2. **单元测试**：`dotnet test --filter "Gateway.Resilience"` 验证各组件
3. **集成测试**：验证完整弹性流程（重试→熔断→降级）
4. **手动测试**：启动 Gateway.Server，模拟后端故障验证降级响应
