# Gateway 分布式链路跟踪功能实现计划

## 概述

为 JZVerse.MicroHuaxia.Gateway 添加分布式链路跟踪功能，支持多种追踪协议（W3C TraceContext、B3、自定义 Header），提供完整的请求生命周期追踪，支持内存存储 + OTLP 导出。

## 项目结构

创建新项目 `JZVerse.MicroHuaxia.Gateway.Tracing`：

```
Gateway.Tracing/
├── GatewayActivitySource.cs              # 网关活动源（核心）
├── GatewaySpanAttributes.cs              # Span 属性常量
├── Configuration/
│   └── GatewayTracingOptions.cs          # 追踪配置选项
├── Propagation/
│   ├── ITracePropagator.cs               # 传播器接口
│   ├── W3CTracePropagator.cs             # W3C TraceContext
│   ├── B3TracePropagator.cs              # B3 协议（单头/多头）
│   ├── CustomTracePropagator.cs          # 自定义 Header
│   └── CompositeTracePropagator.cs       # 组合传播器
├── Storage/
│   ├── ITraceStore.cs                    # 存储接口
│   ├── InMemoryTraceStore.cs             # 内存存储
│   └── TraceSpan.cs                      # Span 数据模型
├── Middleware/
│   └── GatewayTracingMiddleware.cs       # 追踪中间件
└── Extensions/
    └── GatewayTracingExtensions.cs       # DI 扩展方法
```

## 核心组件设计

### 1. GatewayActivitySource

参考 `ServiceDiscoveryActivitySource` 模式，定义网关专用活动源：

```csharp
public static class GatewayActivitySource
{
    public const string Name = "JZVerse.MicroHuaxia.Gateway";
    public static ActivitySource Instance { get; } = new(Name, "1.0.0");
    
    // 各环节 Activity 创建方法
    public static Activity? StartGatewayRequestActivity(HttpContext context);
    public static Activity? StartRouteMatchingActivity(string path);
    public static Activity? StartAuthenticationActivity(string strategy);
    public static Activity? StartRateLimitActivity(string key);
    public static Activity? StartCacheActivity(string key, bool isRead);
    public static Activity? StartForwardingActivity(string target);
    public static Activity? StartRetryActivity(int attempt);
    
    // 扩展方法
    public static void RecordRouteMatch(this Activity? activity, ...);
    public static void RecordForwardingResult(this Activity? activity, ...);
}
```

### 2. 追踪传播器接口

```csharp
public interface ITracePropagator
{
    string Name { get; }
    void Inject(Activity? activity, HttpRequestMessage request);
    PropagationContext Extract(HttpContext context);
}
```

**实现类：**
- `W3CTracePropagator` - 处理 `traceparent` / `tracestate` 头
- `B3TracePropagator` - 处理 `b3` / `X-B3-*` 头（支持单头和多头模式）
- `CustomTracePropagator` - 处理自定义 Header（如 `X-Request-ID`）
- `CompositeTracePropagator` - 组合多个传播器，按优先级提取

### 3. 配置选项

```csharp
public sealed class GatewayTracingOptions
{
    public bool Enabled { get; set; } = true;
    public double SamplingRatio { get; set; } = 1.0;
    public TracePropagationFormat PropagationFormat { get; set; } = TracePropagationFormat.W3C;
    public CustomHeaderOptions? CustomHeaders { get; set; }
    public TraceRecordOptions Record { get; set; } = new();
    public InMemoryStoreOptions InMemoryStore { get; set; } = new();
    public List<string> IgnorePaths { get; set; } = ["/health", "/metrics"];
}

public enum TracePropagationFormat
{
    W3C,        // W3C TraceContext (默认)
    B3Single,   // B3 单头模式
    B3Multi,    // B3 多头模式
    Custom,     // 自定义 Header
    Composite   // 组合模式（同时支持多种）
}
```

### 4. 内存存储

```csharp
public sealed record TraceSpan
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public required string Name { get; init; }
    public ActivityKind Kind { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public TimeSpan? Duration { get; init; }
    public ActivityStatusCode Status { get; init; }
    public Dictionary<string, object?> Attributes { get; init; } = [];
    public List<SpanEvent> Events { get; init; } = [];
}

public interface ITraceStore
{
    void AddSpan(TraceSpan span);
    IReadOnlyList<TraceSpan> GetTrace(string traceId);
    TraceQueryResult Query(TraceQuery query);
}
```

## 集成点

### 1. 中间件管道顺序

```
GatewayTracingMiddleware (最外层，创建根 Span)
└─> GatewayAuditMiddleware
    └─> GatewayAuthenticationMiddleware (子 Span)
        └─> GatewayRateLimitMiddleware (子 Span)
            └─> GatewayCacheMiddleware (子 Span)
                └─> GatewayRoutingMiddleware (子 Span)
```

### 2. HttpRequestForwarder 修改

**修改位置：** `CreateRequestMessage()` 方法

```csharp
// 注入追踪上下文到下游请求
if (_tracePropagator != null && Activity.Current != null)
{
    _tracePropagator.Inject(Activity.Current, requestMessage);
}
```

### 3. 弹性管道集成

在 `GatewayResiliencePipeline` 执行时记录：
- 重试事件：`Activity.AddEvent("retry", tags: { attempt, delay })`
- 熔断事件：`Activity.AddEvent("circuit_breaker.state_changed", tags: { state })`
- 舱壁拒绝：`Activity.AddEvent("bulkhead.rejected")`
- 降级执行：`Activity.AddEvent("fallback.executed", tags: { type })`

## 关键文件

| 文件 | 操作 | 说明 |
|------|------|------|
| `Gateway.Tracing/GatewayActivitySource.cs` | 新建 | 核心追踪源 |
| `Gateway.Tracing/Propagation/*.cs` | 新建 | 传播器实现 |
| `Gateway.Tracing/Configuration/GatewayTracingOptions.cs` | 新建 | 配置模型 |
| `Gateway.Tracing/Storage/InMemoryTraceStore.cs` | 新建 | 内存存储 |
| `Gateway.Tracing/Middleware/GatewayTracingMiddleware.cs` | 新建 | 追踪中间件 |
| `Gateway.Tracing/Extensions/GatewayTracingExtensions.cs` | 新建 | DI 扩展 |
| `Gateway.Http/HttpRequestForwarder.cs` | 修改 | 注入传播器 |
| `Gateway.AspNetCore/Extensions/GatewayApplicationExtensions.cs` | 修改 | 注册中间件 |
| `Gateway.Resilience/Pipeline/GatewayResiliencePipeline.cs` | 修改 | 记录弹性事件 |

## 实现步骤

1. **创建项目结构** - 新建 `Gateway.Tracing` 项目和基础文件
2. **实现 GatewayActivitySource** - 定义所有 Activity 创建方法和属性常量
3. **实现传播器** - W3C、B3、自定义、组合传播器
4. **实现配置和存储** - 配置选项、内存存储、查询接口
5. **实现追踪中间件** - 顶层中间件，创建根 Span
6. **修改现有中间件** - 在各环节创建子 Span
7. **集成 HttpRequestForwarder** - 注入传播器，传播追踪上下文
8. **集成弹性管道** - 记录重试、熔断等事件
9. **配置 DI 扩展** - 集成 OpenTelemetry，配置 OTLP 导出
10. **编写单元测试** - 传播器、存储、中间件测试
11. **验证构建和测试** - 确保所有测试通过

## 配置示例

```json
{
  "Gateway": {
    "Tracing": {
      "Enabled": true,
      "SamplingRatio": 1.0,
      "PropagationFormat": "Composite",
      "InMemoryStore": {
        "Enabled": true,
        "MaxSpans": 10000,
        "RetentionMinutes": 30
      },
      "IgnorePaths": ["/health", "/metrics"]
    }
  },
  "Telemetry": {
    "OtlpExporter": {
      "Endpoint": "http://jaeger:4317",
      "Protocol": "Grpc"
    }
  }
}
```

## 验证方法

1. **构建验证** - `dotnet build` 确保编译通过
2. **单元测试** - `dotnet test` 运行所有测试
3. **集成验证** - 启动 Gateway，发送请求，验证：
   - TraceId 正确传播到下游服务
   - Span 层级关系正确
   - 内存存储可查询追踪数据
   - OTLP 导出到 Jaeger/Tempo 可见
