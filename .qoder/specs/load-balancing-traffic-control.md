# 负载均衡与流量控制完善方案

## 概述

完善 JZVerse.MicroHuaxia 网关的负载均衡和流量控制能力，包括：
1. **限流增强**：TokenBucket + LeakyBucket 算法实现
2. **流量染色**：请求标记和灰度路由支持
3. **流量镜像**：异步复制流量到测试环境
4. **自适应负载均衡**：基于实时指标动态调整权重

存储方案：当前使用内存存储，接口设计兼容 Garnet（微软高性能缓存，Redis 协议兼容）。

---

## Phase 1: 限流增强

### 1.1 扩展 IRateLimiterStore 接口

**文件**: `Gateway.Abstractions/RateLimiting/IRateLimiter.cs`

新增方法支持 TokenBucket 和 LeakyBucket：
```csharp
// Token Bucket 相关
ValueTask<TokenBucketState> GetTokenBucketStateAsync(string key, CancellationToken ct);
ValueTask<TokenBucketState> ConsumeTokensAsync(string key, TokenBucketPolicy policy, int tokens, CancellationToken ct);

// Leaky Bucket 相关  
ValueTask<LeakyBucketState> GetLeakyBucketStateAsync(string key, CancellationToken ct);
ValueTask<LeakyBucketState> EnqueueRequestAsync(string key, LeakyBucketPolicy policy, CancellationToken ct);
```

### 1.2 实现 TokenBucketRateLimiter

**文件**: `Gateway.Core/RateLimiting/TokenBucketRateLimiter.cs`

算法要点：
- 令牌按 `TokensPerSecond` 速率补充
- 桶容量 `BucketCapacity` 允许突发流量
- 每次请求消耗 1 个令牌
- 令牌不足时拒绝请求

### 1.3 实现 LeakyBucketRateLimiter

**文件**: `Gateway.Core/RateLimiting/LeakyBucketRateLimiter.cs`

算法要点：
- 固定速率漏出请求（平滑流量）
- 队列满时拒绝新请求
- 返回预计等待时间

### 1.4 实现 RateLimiterFactory

**文件**: `Gateway.Core/RateLimiting/RateLimiterFactory.cs`

- 根据 `RateLimitAlgorithm` 枚举创建对应限流器
- 缓存实例避免重复创建
- 注入 `IRateLimiterStore`

### 1.5 重构 MemoryRateLimiterStore

**文件**: `Gateway.Core/RateLimiting/Storage/MemoryRateLimiterStore.cs`

- 从 `SlidingWindowRateLimiter.cs` 拆分独立文件
- 实现扩展后的 `IRateLimiterStore` 接口
- 支持三种算法的状态存储

### 1.6 更新中间件集成

**文件**: `Gateway.AspNetCore/Middleware/GatewayMiddleware.cs`

修改 `GatewayRateLimitMiddleware`：
- 注入 `IRateLimiterFactory` 替代直接依赖 `IRateLimiter`
- 根据路由配置的 `Algorithm` 动态选择限流器

---

## Phase 2: 流量染色

### 2.1 定义染色接口和模型

**文件**: `Gateway.Abstractions/TrafficControl/ITrafficColoringService.cs`

```csharp
public interface ITrafficColoringService
{
    Task<TrafficColoringResult> ApplyColoringAsync(HttpContext context, GatewayRoute route, CancellationToken ct);
}

public sealed record TrafficColoringResult
{
    public HashSet<string> Tags { get; init; }  // 染色标签
    public string? MatchedRule { get; init; }   // 匹配的规则名
}
```

**文件**: `Gateway.Abstractions/TrafficControl/TrafficColoringRule.cs`

```csharp
public sealed record RouteTrafficColoring
{
    public bool Enabled { get; init; }
    public List<TrafficColoringRule> Rules { get; init; }
}

public sealed record TrafficColoringRule
{
    public string Name { get; init; }
    public TrafficColoringType Type { get; init; }  // Percentage, HeaderMatch, UserWhitelist
    public string Tag { get; init; }
    public int? Percentage { get; init; }
    public string? HeaderName { get; init; }
    public string? HeaderValue { get; init; }
    public HashSet<string>? UserIds { get; init; }
}
```

### 2.2 扩展 GatewayRoute 配置

**文件**: `Gateway.Abstractions/Routing/GatewayRoute.cs`

添加属性：
```csharp
public RouteTrafficColoring? TrafficColoring { get; init; }
```

### 2.3 实现 TrafficColoringService

**文件**: `Gateway.Core/TrafficControl/TrafficColoringService.cs`

染色策略：
- **Percentage**: 基于 UserId/ClientIP 哈希的百分比分流
- **HeaderMatch**: Header 值精确匹配
- **UserWhitelist**: 用户白名单

### 2.4 实现染色中间件

**文件**: `Gateway.AspNetCore/Middleware/GatewayTrafficColoringMiddleware.cs`

执行逻辑：
1. 获取路由配置 `route.TrafficColoring`
2. 调用 `ITrafficColoringService.ApplyColoringAsync()`
3. 注入标签到 `HttpContext.Items["TrafficTags"]`
4. 添加 Header: `X-Traffic-Tag: {tags}`

### 2.5 集成到负载均衡

**文件**: `Gateway.Http/HttpRequestForwarder.cs`

在 `ResolveTargetAddressAsync` 中：
- 从 `HttpContext.Items["TrafficTags"]` 读取标签
- 设置 `LoadBalancerContext.PreferredTags = trafficTags`
- 由 `ServiceInstanceSelector` 根据标签筛选实例

---

## Phase 3: 流量镜像

### 3.1 定义镜像接口和配置

**文件**: `Gateway.Abstractions/TrafficControl/ITrafficMirrorService.cs`

```csharp
public interface ITrafficMirrorService
{
    Task MirrorAsync(HttpContext context, HttpRequestMessage originalRequest, RouteTrafficMirror config, CancellationToken ct);
}
```

**文件**: `Gateway.Abstractions/Routing/GatewayRoute.cs`

添加属性：
```csharp
public RouteTrafficMirror? TrafficMirror { get; init; }

public sealed record RouteTrafficMirror
{
    public bool Enabled { get; init; }
    public string TargetAddress { get; init; }      // 镜像目标地址
    public int SamplePercentage { get; init; } = 100;  // 采样比例
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
    public bool IgnoreResponse { get; init; } = true;  // 忽略镜像响应
}
```

### 3.2 实现 TrafficMirrorService

**文件**: `Gateway.Core/TrafficControl/TrafficMirrorService.cs`

实现要点：
- 使用独立的 `HttpClient` 和线程池
- 异步发送（Fire-and-forget），不影响主请求
- 根据 `SamplePercentage` 随机采样
- 失败静默记录日志，不重试

### 3.3 集成到请求转发

**文件**: `Gateway.Http/HttpRequestForwarder.cs`

在 `WriteResponseAsync` 之前调用：
```csharp
if (route.TrafficMirror?.Enabled == true)
{
    _ = _mirrorService.MirrorAsync(context, clonedRequest, route.TrafficMirror, ct);
}
```

---

## Phase 4: 自适应负载均衡

### 4.1 定义指标模型

**文件**: `ServiceCommunication.Abstractions/LoadBalancing/InstanceMetrics.cs`

```csharp
public sealed class InstanceMetrics
{
    public string InstanceId { get; init; }
    public CircularBuffer<TimeSpan> ResponseTimes { get; }  // 最近 N 次响应时间
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }
    public int ActiveConnections { get; set; }
    public DateTimeOffset LastUpdateTime { get; set; }
    
    public double SuccessRate => SuccessCount / (double)(SuccessCount + FailureCount + 1);
    public TimeSpan AverageResponseTime { get; }
}
```

### 4.2 实现指标收集器

**文件**: `ServiceCommunication.Core/LoadBalancing/InstanceMetricsCollector.cs`

```csharp
public interface IInstanceMetricsCollector
{
    void RecordRequest(string instanceId, bool success, TimeSpan duration);
    InstanceMetrics? GetMetrics(string instanceId);
    IReadOnlyDictionary<string, InstanceMetrics> GetAllMetrics();
}
```

### 4.3 实现 AdaptiveWeightedLoadBalancer

**文件**: `ServiceCommunication.Core/LoadBalancing/AdaptiveWeightedLoadBalancer.cs`

权重计算公式：
```
W_final = W_base * (α * (1/AvgRT_normalized) + β * SuccessRate)
```
- α = 0.5, β = 0.5（可配置）
- 权重变化限制：每次 ≤ 20%
- 使用指数移动平均平滑指标

### 4.4 扩展 LoadBalancerStrategy 枚举

**文件**: `ServiceCommunication.Abstractions/LoadBalancing/ILoadBalancer.cs`

```csharp
public enum LoadBalancerStrategy
{
    // 现有...
    AdaptiveWeighted  // 新增
}
```

### 4.5 集成到 ServiceInstanceSelector

**文件**: `ServiceCommunication.Core/ServiceDiscovery/ServiceInstanceSelector.cs`

- 添加 `ReportInstanceStatus(instanceId, success, duration)` 方法
- 在 HTTP 响应后调用，收集指标
- 自适应负载均衡器使用收集的指标

### 4.6 扩展 LoadBalancerFactory

**文件**: `ServiceCommunication.Core/LoadBalancing/LoadBalancerFactory.cs`

添加 `AdaptiveWeighted` 策略支持，注入 `IInstanceMetricsCollector`。

---

## 关键文件清单

| 操作 | 文件路径 |
|------|----------|
| 修改 | `Gateway.Abstractions/RateLimiting/IRateLimiter.cs` |
| 新增 | `Gateway.Core/RateLimiting/TokenBucketRateLimiter.cs` |
| 新增 | `Gateway.Core/RateLimiting/LeakyBucketRateLimiter.cs` |
| 新增 | `Gateway.Core/RateLimiting/RateLimiterFactory.cs` |
| 新增 | `Gateway.Core/RateLimiting/Storage/MemoryRateLimiterStore.cs` |
| 修改 | `Gateway.Core/RateLimiting/SlidingWindowRateLimiter.cs` |
| 新增 | `Gateway.Abstractions/TrafficControl/ITrafficColoringService.cs` |
| 新增 | `Gateway.Abstractions/TrafficControl/TrafficColoringRule.cs` |
| 新增 | `Gateway.Core/TrafficControl/TrafficColoringService.cs` |
| 新增 | `Gateway.AspNetCore/Middleware/GatewayTrafficColoringMiddleware.cs` |
| 新增 | `Gateway.Abstractions/TrafficControl/ITrafficMirrorService.cs` |
| 新增 | `Gateway.Core/TrafficControl/TrafficMirrorService.cs` |
| 修改 | `Gateway.Abstractions/Routing/GatewayRoute.cs` |
| 修改 | `Gateway.Http/HttpRequestForwarder.cs` |
| 修改 | `Gateway.AspNetCore/Middleware/GatewayMiddleware.cs` |
| 修改 | `Gateway.AspNetCore/Extensions/GatewayServiceCollectionExtensions.cs` |
| 修改 | `Gateway.AspNetCore/Extensions/GatewayApplicationExtensions.cs` |
| 新增 | `ServiceCommunication.Abstractions/LoadBalancing/InstanceMetrics.cs` |
| 新增 | `ServiceCommunication.Core/LoadBalancing/InstanceMetricsCollector.cs` |
| 新增 | `ServiceCommunication.Core/LoadBalancing/AdaptiveWeightedLoadBalancer.cs` |
| 修改 | `ServiceCommunication.Abstractions/LoadBalancing/ILoadBalancer.cs` |
| 修改 | `ServiceCommunication.Core/LoadBalancing/LoadBalancerFactory.cs` |
| 修改 | `ServiceCommunication.Core/ServiceDiscovery/ServiceInstanceSelector.cs` |

---

## 验证方案

### 单元测试
```bash
# 运行限流相关测试
dotnet test tests/JZVerse.MicroHuaxia.Gateway.Tests.Unit --filter "FullyQualifiedName~RateLimiting"

# 运行流量控制相关测试
dotnet test tests/JZVerse.MicroHuaxia.Gateway.Tests.Unit --filter "FullyQualifiedName~TrafficControl"

# 运行负载均衡相关测试
dotnet test tests/JZVerse.MicroHuaxia.ServiceCommunication.Tests.Unit --filter "FullyQualifiedName~LoadBalancing"
```

### 集成测试
1. 启动 Gateway.Server
2. 配置测试路由，启用不同限流算法
3. 使用 curl/wrk 发送请求验证限流行为
4. 验证流量染色 Header 传递
5. 验证自适应权重变化

### 构建验证
```bash
dotnet build JZVerse.OfficialSite.Backend.sln
```
