# JZVerse.MicroHuaxia 服务通信模块设计方案

## 概述

为 JZVerse.MicroHuaxia 微服务框架添加统一的服务间通信能力，支持 HTTP、gRPC、JSON-RPC 三种通信协议，并为现有的 ServiceDiscovery 和 ConfigCenter 模块添加 RPC 支持。

## 架构设计

```
┌─────────────────────────────────────────────────────────────┐
│                    业务服务层 (Business Services)             │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│              服务通信抽象层 (ServiceCommunication)            │
│    IServiceClient, ServiceRequest/Response, ILoadBalancer    │
└─────────────────────────────────────────────────────────────┘
                              ↓
        ┌─────────────┬───────────────┬──────────────┐
        ↓             ↓               ↓              ↓
┌──────────────┐ ┌─────────┐ ┌──────────────┐ ┌─────────────┐
│   HTTP 层    │ │ gRPC 层 │ │ JSON-RPC 层  │ │  核心层     │
│ HttpService  │ │ gRPC    │ │  JsonRpc     │ │ LoadBalance │
│   Client     │ │ Channel │ │  Client      │ │   Retry     │
│              │ │ Factory │ │              │ │ CircuitBreak│
└──────────────┘ └─────────┘ └──────────────┘ └─────────────┘
                              ↓
        ┌──────────────────────────────────────┐
        │     服务发现层 (ServiceDiscovery)     │
        └──────────────────────────────────────┘
```

## 项目结构

### ServiceCommunication 模块（7个新项目）

| 项目 | 说明 | 状态 |
|------|------|------|
| `ServiceCommunication.Abstractions` | 通信抽象、负载均衡、弹性策略接口 | ✅ 已创建部分 |
| `ServiceCommunication.Core` | 重试策略、熔断器、弹性管道实现 | ⭐ 新建 |
| `ServiceCommunication.Http` | HTTP 客户端、DelegatingHandler 链 | ⭐ 新建 |
| `ServiceCommunication.Grpc` | gRPC Channel 工厂、拦截器 | ⭐ 新建 |
| `ServiceCommunication.JsonRpc` | JSON-RPC 2.0 客户端和服务端 | ⭐ 新建 |
| `ServiceCommunication.AspNetCore` | ASP.NET Core 集成 | ⭐ 新建 |
| `ServiceCommunication.Protos` | gRPC Proto 文件 | ⭐ 新建 |

### 现有模块 RPC 扩展（4个新项目）

| 项目 | 说明 |
|------|------|
| `ServiceDiscovery.Grpc` | 服务发现 gRPC 服务端 |
| `ServiceDiscovery.JsonRpc` | 服务发现 JSON-RPC 处理器 |
| `ConfigCenter.Grpc` | 配置中心 gRPC 服务端 |
| `ConfigCenter.JsonRpc` | 配置中心 JSON-RPC 处理器 |

## 核心接口

### 弹性策略

```csharp
// 重试策略
public interface IRetryPolicy
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}

// 熔断器
public interface ICircuitBreaker
{
    CircuitBreakerState State { get; }
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}

// 弹性管道（组合重试+熔断+超时）
public interface IResiliencePipeline
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
```

### 服务实例选择器

```csharp
// 整合服务发现 + 负载均衡
public interface IServiceInstanceSelector
{
    Task<ServiceInstance?> SelectAsync(string serviceName, LoadBalancerContext? context, CancellationToken ct);
    void ReportInstanceStatus(ServiceInstance instance, bool success, TimeSpan duration, Exception? ex);
}
```

## gRPC Proto 定义

### service_discovery.proto

```protobuf
service ServiceDiscoveryService {
    rpc Register (RegisterRequest) returns (RegisterResponse);
    rpc Deregister (DeregisterRequest) returns (DeregisterResponse);
    rpc Heartbeat (HeartbeatRequest) returns (HeartbeatResponse);
    rpc GetInstances (GetInstancesRequest) returns (GetInstancesResponse);
    rpc GetInstance (GetInstanceRequest) returns (GetInstanceResponse);
    rpc Discover (DiscoverRequest) returns (DiscoverResponse);
    rpc GetServiceNames (GetServiceNamesRequest) returns (GetServiceNamesResponse);
    rpc WatchService (WatchServiceRequest) returns (stream ServiceEvent);  // 服务变更订阅
}
```

### config_center.proto

```protobuf
service ConfigCenterService {
    rpc GetConfig (GetConfigRequest) returns (GetConfigResponse);
    rpc GetValue (GetValueRequest) returns (GetValueResponse);
    rpc GetNamespaceConfig (GetNamespaceConfigRequest) returns (GetNamespaceConfigResponse);
    rpc QueryConfig (QueryConfigRequest) returns (QueryConfigResponse);
    rpc WatchConfig (WatchConfigRequest) returns (stream ConfigChangeEvent);  // 配置变更订阅
    rpc UpsertConfig (UpsertConfigRequest) returns (UpsertConfigResponse);
    rpc DeleteConfig (DeleteConfigRequest) returns (DeleteConfigResponse);
}
```

## 关键实现

### 1. ResiliencePipeline（弹性管道）

```
执行流程：
1. 检查熔断器状态 → 如果 Open 则抛出 BrokenCircuitException
2. 创建带超时的 CancellationTokenSource
3. 使用 IRetryPolicy 包装操作（支持指数退避、抖动）
4. 操作成功 → 通知熔断器 OnSuccess
5. 操作失败 → 通知熔断器 OnFailure
```

### 2. HTTP DelegatingHandler 链

```
请求流程：
ServiceDiscoveryHandler → LoadBalancingHandler → RetryHandler → CircuitBreakerHandler
    ↓                         ↓                      ↓                ↓
  解析服务名              选择实例              失败重试          熔断保护
```

### 3. gRPC Channel 工厂

```csharp
GetOrCreateChannel(serviceName):
  → 检查缓存
  → 从 ServiceDiscovery 获取实例列表
  → 创建 ServiceDiscoveryResolver
  → 创建 GrpcChannel（配置负载均衡）
  → 缓存并返回
```

### 4. JSON-RPC 中间件

```
HTTP POST /jsonrpc
  → 解析 JsonRpcRequest { jsonrpc: "2.0", method: "...", params: {...}, id: "..." }
  → 从 MethodRegistry 查找 Handler
  → 执行 Handler
  → 返回 JsonRpcResponse
```

## 依赖关系

```
ServiceCommunication.AspNetCore
            ↓
  ┌─────────┼─────────┐
  ↓         ↓         ↓
.Http    .Grpc    .JsonRpc
  └─────────┴─────────┘
            ↓
     .Core + .Abstractions
            ↓
  ServiceDiscovery.Abstractions
```

## 实现顺序

### 阶段 1：核心基础
1. `ServiceCommunication.Core` - 重试策略、熔断器、弹性管道
2. 补全 `ServiceCommunication.Abstractions`

### 阶段 2：HTTP 通信
3. `ServiceCommunication.Http` - HTTP 客户端、Handler 链

### 阶段 3：gRPC 支持
4. `ServiceCommunication.Protos` - Proto 文件定义
5. `ServiceDiscovery.Grpc` - 服务发现 gRPC 服务端
6. `ServiceCommunication.Grpc` - gRPC 客户端、Channel 工厂

### 阶段 4：JSON-RPC 支持
7. `ServiceCommunication.JsonRpc` - JSON-RPC 客户端和中间件
8. `ServiceDiscovery.JsonRpc` - 服务发现 JSON-RPC 处理器

### 阶段 5：ConfigCenter RPC 集成
9. `ConfigCenter.Grpc` - 配置中心 gRPC 服务端
10. `ConfigCenter.JsonRpc` - 配置中心 JSON-RPC 处理器

### 阶段 6：集成和测试
11. `ServiceCommunication.AspNetCore` - 统一 DI 扩展
12. 更新 slnx 文件
13. 添加单元测试和集成测试

## 关键文件

| 文件 | 说明 |
|------|------|
| `ServiceCommunication.Core/Resilience/ResiliencePipeline.cs` | 核心弹性管道 |
| `ServiceCommunication.Core/Resilience/CircuitBreaker.cs` | 熔断器实现 |
| `ServiceCommunication.Core/LoadBalancing/ServiceInstanceSelector.cs` | 服务实例选择器 |
| `ServiceCommunication.Http/HttpServiceClient.cs` | HTTP 客户端 |
| `ServiceCommunication.Grpc/GrpcChannelFactory.cs` | gRPC Channel 工厂 |
| `ServiceCommunication.JsonRpc/Server/JsonRpcMiddleware.cs` | JSON-RPC 中间件 |
| `ServiceCommunication.Protos/service_discovery.proto` | 服务发现 Proto |
| `ServiceCommunication.Protos/config_center.proto` | 配置中心 Proto |
| `ServiceDiscovery.Grpc/Services/ServiceDiscoveryGrpcService.cs` | gRPC 服务实现 |

## 配置示例

### 客户端配置

```csharp
builder.Services.AddServiceCommunication(options => 
{
    options.DefaultProtocol = ServiceProtocol.Http;
    options.DefaultTimeout = TimeSpan.FromSeconds(30);
})
.AddServiceDiscovery(options => 
{
    options.ServerUrls = ["http://localhost:5001"];
})
.AddResilience(options => 
{
    options.Retry = new RetryPolicyOptions { MaxRetries = 3 };
    options.CircuitBreaker = new CircuitBreakerOptions { FailureThreshold = 5 };
})
.AddHttpCommunication()
.AddGrpcCommunication()
.AddJsonRpcCommunication();
```

### 服务端配置

```csharp
// ServiceDiscovery.Server
builder.Services.AddGrpc();
builder.Services.AddServiceDiscoveryGrpc();
builder.Services.AddJsonRpc();
builder.Services.AddServiceDiscoveryJsonRpc();

app.MapGrpcService<ServiceDiscoveryGrpcService>();
app.UseJsonRpc();
```

## 验证方案

1. **编译验证**：`dotnet build JZVerse.OfficialSite.Backend.slnx`
2. **单元测试**：测试重试策略、熔断器、负载均衡
3. **集成测试**：
   - HTTP 通信：启动 ServiceDiscovery.Server，使用 HTTP 客户端调用
   - gRPC 通信：使用 gRPC 客户端调用 ServiceDiscoveryGrpcService
   - JSON-RPC 通信：POST /jsonrpc 端点测试
4. **多协议切换测试**：验证同一客户端可切换 HTTP/gRPC/JSON-RPC
