# Gateway 指标监控系统实现计划

## 概述

为 JZVerse.MicroHuaxia.Gateway 添加完整的指标监控功能，支持 OTLP + Prometheus 导出、内存存储、HTTP API 查询，覆盖请求、认证、限流、缓存、转发、弹性等全部环节。

## 项目结构

```
JZVerse.MicroHuaxia.Gateway.Metrics/
├── Configuration/
│   ├── GatewayMetricsOptions.cs           # 主配置选项
│   └── InMemoryMetricsStoreOptions.cs     # 内存存储配置
├── Metrics/
│   ├── GatewayMetricNames.cs              # 指标名称常量
│   ├── GatewayMetricTags.cs               # 标签名称常量
│   └── GatewayMetrics.cs                  # 核心指标收集器
├── Storage/
│   ├── IMetricsStore.cs                   # 存储接口
│   ├── InMemoryMetricsStore.cs            # 内存存储实现
│   ├── MetricDataPoint.cs                 # 数据点模型
│   ├── MetricBucket.cs                    # 分桶模型
│   └── MetricAggregator.cs                # 聚合器
├── Api/
│   ├── IGatewayMetricsProvider.cs         # API 数据提供者接口
│   ├── GatewayMetricsProvider.cs          # API 数据提供者实现
│   └── Models/
│       ├── MetricsSnapshot.cs             # 快照模型
│       ├── MetricsQuery.cs                # 查询模型
│       └── MetricsStatistics.cs           # 统计模型
├── Extensions/
│   └── GatewayMetricsExtensions.cs        # DI 扩展方法
└── JZVerse.MicroHuaxia.Gateway.Metrics.csproj
```

## 指标定义

### 请求指标
| 指标名称 | 类型 | 描述 |
|---------|------|------|
| `gateway.requests.total` | Counter | 请求总数 |
| `gateway.request.duration` | Histogram | 请求耗时(ms) |
| `gateway.request.size` | Histogram | 请求体大小 |
| `gateway.response.size` | Histogram | 响应体大小 |

### 认证指标
| 指标名称 | 类型 | 描述 |
|---------|------|------|
| `gateway.auth.attempts` | Counter | 认证尝试总数 |
| `gateway.auth.failures` | Counter | 认证失败总数 |
| `gateway.auth.duration` | Histogram | 认证耗时(ms) |

### 限流指标
| 指标名称 | 类型 | 描述 |
|---------|------|------|
| `gateway.ratelimit.allowed` | Counter | 允许通过数 |
| `gateway.ratelimit.blocked` | Counter | 被限流数 |
| `gateway.ratelimit.remaining` | Gauge | 剩余配额 |

### 缓存指标
| 指标名称 | 类型 | 描述 |
|---------|------|------|
| `gateway.cache.hits` | Counter | 缓存命中数 |
| `gateway.cache.misses` | Counter | 缓存未命中数 |
| `gateway.cache.size` | Gauge | 缓存条目数 |
| `gateway.cache.evictions` | Counter | 缓存驱逐数 |

### 转发指标
| 指标名称 | 类型 | 描述 |
|---------|------|------|
| `gateway.forward.attempts` | Counter | 转发尝试数 |
| `gateway.forward.failures` | Counter | 转发失败数 |
| `gateway.forward.duration` | Histogram | 转发耗时(ms) |

### 弹性指标
| 指标名称 | 类型 | 描述 |
|---------|------|------|
| `gateway.retry.attempts` | Counter | 重试次数 |
| `gateway.circuitbreaker.state` | Gauge | 熔断器状态 |
| `gateway.circuitbreaker.trips` | Counter | 熔断触发数 |
| `gateway.bulkhead.concurrent` | Gauge | 舱壁并发数 |
| `gateway.bulkhead.rejected` | Counter | 舱壁拒绝数 |
| `gateway.fallback.executions` | Counter | 降级执行数 |

## 核心类设计

### GatewayMetrics (核心收集器)

```csharp
public sealed class GatewayMetrics
{
    public GatewayMetrics(IMeterFactory meterFactory);
    
    // 请求指标
    public void RecordRequest(string routeId, string method, int statusCode, 
                              double durationMs, long? requestSize, long? responseSize);
    
    // 认证指标
    public void RecordAuthentication(string routeId, string strategy, 
                                     bool success, double durationMs);
    
    // 限流指标
    public void RecordRateLimit(string routeId, string algorithm, 
                                string keyStrategy, bool allowed, long? remaining);
    
    // 缓存指标
    public void RecordCacheAccess(string routeId, bool hit);
    public void RecordCacheEviction(string routeId);
    
    // 转发指标
    public void RecordForward(string routeId, string protocol, string target, 
                              bool success, double durationMs);
    
    // 弹性指标
    public void RecordRetry(string routeId, int attempt);
    public void RecordCircuitBreakerTrip(string name, string state);
    public void RecordBulkheadRejection(string name);
    public void RecordFallback(string routeId, string type);
    
    // 状态提供者
    public void SetCacheStatsProvider(Func<(int size, int count)> provider);
    public void SetCircuitBreakerStatsProvider(Func<IReadOnlyDictionary<string, string>> provider);
    public void SetBulkheadStatsProvider(Func<IReadOnlyDictionary<string, int>> provider);
}
```

### InMemoryMetricsStore (内存存储)

```csharp
public sealed class InMemoryMetricsStore : IMetricsStore, IDisposable
{
    // 存储
    public void Record(string name, double value, IDictionary<string, object?>? tags);
    
    // 查询
    public IReadOnlyList<MetricDataPoint> GetTimeSeries(string name, 
        DateTimeOffset start, DateTimeOffset end, IDictionary<string, object?>? tags);
    public MetricsSnapshot GetSnapshot();
    public MetricsStatistics GetStatistics();
    
    // 聚合
    public IReadOnlyList<MetricDataPoint> Aggregate(string name, 
        TimeSpan bucketSize, AggregationType type);
    
    // 维护
    public void Cleanup();
    public void Clear();
}
```

### GatewayMetricsOptions (配置)

```csharp
public sealed class GatewayMetricsOptions
{
    public bool Enabled { get; set; } = true;
    public bool CollectRequestMetrics { get; set; } = true;
    public bool CollectAuthMetrics { get; set; } = true;
    public bool CollectRateLimitMetrics { get; set; } = true;
    public bool CollectCacheMetrics { get; set; } = true;
    public bool CollectForwardMetrics { get; set; } = true;
    public bool CollectResilienceMetrics { get; set; } = true;
    
    public InMemoryMetricsStoreOptions InMemoryStore { get; set; } = new();
    
    public bool EnableOtlpExporter { get; set; }
    public string? OtlpEndpoint { get; set; }
    public bool EnablePrometheusExporter { get; set; }
    public string PrometheusEndpoint { get; set; } = "/metrics";
    public bool EnableHttpApi { get; set; } = true;
    
    public List<string> IgnorePaths { get; set; } = ["/health", "/metrics"];
}
```

## 中间件集成点

在现有中间件中注入 `GatewayMetrics`（可选依赖），收集指标：

| 中间件 | 收集内容 |
|--------|---------|
| `GatewayAuditMiddleware` | 请求总数、耗时、状态码、请求/响应大小 |
| `GatewayAuthenticationMiddleware` | 认证尝试、成功/失败、策略、耗时 |
| `GatewayRateLimitMiddleware` | 限流允许/拒绝、算法、剩余配额 |
| `GatewayCacheMiddleware` | 缓存命中/未命中 |
| `HttpRequestForwarder` | 转发尝试/失败、目标、协议、耗时 |
| `GatewayResiliencePipeline` | 重试、熔断、舱壁、降级 |

## HTTP API 端点

| 端点 | 方法 | 描述 |
|------|------|------|
| `/api/gateway/metrics/snapshot` | GET | 当前指标快照 |
| `/api/gateway/metrics/{name}/timeseries` | GET | 时间序列查询 |
| `/api/gateway/metrics/statistics` | GET | 统计信息 |
| `/api/gateway/metrics/routes/top` | GET | Top N 路由 |
| `/api/gateway/metrics/routes/{routeId}` | GET | 单路由指标 |

## 实现步骤

### 第 1 步：创建项目和常量
- 创建 `Gateway.Metrics` 项目
- 实现 `GatewayMetricNames` 和 `GatewayMetricTags`
- 添加 NuGet 包引用

### 第 2 步：实现核心收集器
- 实现 `GatewayMetrics` 类
- 使用 `IMeterFactory` 创建 Meter
- 定义所有 Counter、Histogram、Gauge

### 第 3 步：实现配置选项
- 实现 `GatewayMetricsOptions`
- 实现 `InMemoryMetricsStoreOptions`

### 第 4 步：实现内存存储
- 实现 `IMetricsStore` 接口
- 实现 `InMemoryMetricsStore`
- 实现分桶、聚合、过期清理

### 第 5 步：实现 DI 扩展
- 实现 `GatewayMetricsExtensions`
- 配置 OpenTelemetry Metrics
- 注册 OTLP 和 Prometheus 导出器

### 第 6 步：集成中间件
- 在各中间件中注入 `GatewayMetrics`
- 在关键点调用指标记录方法

### 第 7 步：实现 HTTP API
- 实现 `IGatewayMetricsProvider`
- 在 Gateway.Server 添加 `MetricsController`

### 第 8 步：编写单元测试
- 测试指标收集器
- 测试内存存储
- 测试聚合计算

### 第 9 步：验证
- 运行所有测试
- 验证 Prometheus 端点
- 验证 HTTP API

## 关键文件

| 文件 | 说明 |
|------|------|
| `Gateway.Metrics/Metrics/GatewayMetrics.cs` | 核心指标收集器 |
| `Gateway.Metrics/Storage/InMemoryMetricsStore.cs` | 内存存储实现 |
| `Gateway.Metrics/Extensions/GatewayMetricsExtensions.cs` | DI 扩展 |
| `Gateway.AspNetCore/Middleware/GatewayMiddleware.cs` | 中间件集成点 |
| `Gateway.Server/Controllers/MetricsController.cs` | HTTP API |

## NuGet 包依赖

```xml
<PackageReference Include="OpenTelemetry" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.10.0-rc.1" />
```

## 验证方式

1. 运行单元测试：`dotnet test`
2. 启动 Gateway.Server，访问 `/metrics` 验证 Prometheus 格式
3. 访问 `/api/gateway/metrics/snapshot` 验证 HTTP API
4. 发送测试请求，观察指标变化
