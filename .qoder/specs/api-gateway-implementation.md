# API Gateway Implementation Plan

## Overview

实现 JZVerse.MicroHuaxia 微服务框架的统一 API 网关，作为所有外部请求的入口，与 ServiceDiscovery、ConfigCenter 聚合部署。

## Requirements

| 需求 | 描述 |
|------|------|
| **核心功能** | 全功能网关：路由转发、负载均衡、认证授权、限流熔断、请求/响应转换、缓存、审计日志 |
| **部署模式** | 聚合部署：网关与 ServiceDiscovery、ConfigCenter 部署在一起，所有外部请求通过网关入口 |
| **认证方式** | 多层可组合认证：JWT Token、API Key、密钥二次加密、策略可组合叠加 |
| **协议支持** | 全协议：HTTP REST、WebSocket 透传、gRPC 透传 |

## Project Structure

创建 8 个项目，遵循现有模块四层结构：

```
/src/JZVerse.MicroHuaxia/
├── JZVerse.MicroHuaxia.Gateway.Abstractions/   # 抽象接口
├── JZVerse.MicroHuaxia.Gateway.Core/           # 核心实现
├── JZVerse.MicroHuaxia.Gateway.AspNetCore/     # ASP.NET Core 集成
├── JZVerse.MicroHuaxia.Gateway.Server/         # 聚合服务应用
├── JZVerse.MicroHuaxia.Gateway.Http/           # HTTP 协议处理
├── JZVerse.MicroHuaxia.Gateway.WebSocket/      # WebSocket 透传
├── JZVerse.MicroHuaxia.Gateway.Grpc/           # gRPC 透传
└── (可选) JZVerse.MicroHuaxia.Gateway.Dashboard/ # 监控仪表板
```

## Implementation Steps

### Phase 1: Core Routing (P0)

**1.1 创建项目结构**
- [ ] 创建 `Gateway.Abstractions` 项目
- [ ] 创建 `Gateway.Core` 项目
- [ ] 创建 `Gateway.AspNetCore` 项目
- [ ] 创建 `Gateway.Server` 项目
- [ ] 更新 slnx 文件

**1.2 定义核心抽象 (Gateway.Abstractions)**

```csharp
// 路由配置模型
public sealed record GatewayRoute
{
    public required string RouteId { get; init; }
    public required string RouteName { get; init; }
    public int Priority { get; init; } = 100;
    public required RouteMatch Match { get; init; }
    public required RouteDestination Destination { get; init; }
    public RouteAuthentication? Authentication { get; init; }
    public RouteRateLimit? RateLimit { get; init; }
    public RouteCache? Cache { get; init; }
    public TimeSpan? Timeout { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}

// 路由匹配引擎接口
public interface IRouteMatchingEngine
{
    ValueTask<RouteMatchResult?> MatchAsync(HttpContext context, CancellationToken ct = default);
    void AddRoute(GatewayRoute route);
    void RemoveRoute(string routeId);
    void UpdateRoutes(IEnumerable<GatewayRoute> routes);
}

// 请求转发器接口
public interface IRequestForwarder
{
    Task<HttpResponseMessage> ForwardAsync(HttpContext context, GatewayRoute route, CancellationToken ct = default);
}
```

**1.3 实现路由匹配引擎 (Gateway.Core)**

路径匹配策略：
- 精确路径：`/api/users`
- 参数路径：`/api/users/{id}`
- 通配符：`/api/users/{**catch-all}`

优先级排序：精确 > 参数 > 通配符

**1.4 实现 HTTP 转发 (Gateway.Http)**

复用 ServiceCommunication 的能力：
- `IServiceInstanceSelector` 选择后端实例
- `ILoadBalancer` 负载均衡
- `ICircuitBreaker` 熔断保护
- `IRetryPolicy` 重试策略

### Phase 2: Authentication (P0)

**2.1 认证抽象接口**

```csharp
// 认证处理器接口
public interface IAuthenticationHandler
{
    string Name { get; }
    int Priority { get; }
    ValueTask<AuthenticationResult> AuthenticateAsync(HttpContext context, CancellationToken ct = default);
}

// 认证管道接口
public interface IAuthenticationPipeline
{
    Task<AuthenticationResult> AuthenticateAsync(
        HttpContext context, 
        RouteAuthentication config, 
        CancellationToken ct = default);
}

// 密钥加密器接口
public interface ISecretEncryptor
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
```

**2.2 认证处理器实现**

| 处理器 | 功能 |
|--------|------|
| `JwtAuthenticationHandler` | JWT Token 验证，支持密钥加密存储 |
| `ApiKeyAuthenticationHandler` | API Key 验证，支持 Header/Query 方式 |
| `CompositeAuthenticationHandler` | 组合认证，支持 AND/OR 策略 |

**2.3 密钥加密支持**

- 使用 AES-256 加密 JWT 密钥
- 配置中存储加密后的密钥：`"secretKey": "encrypted:xxxxx"`
- 运行时解密使用

### Phase 3: Rate Limiting (P0)

**3.1 限流器抽象**

```csharp
public interface IRateLimiter
{
    ValueTask<RateLimitResult> TryAcquireAsync(
        string key, 
        RateLimitPolicy policy, 
        CancellationToken ct = default);
}

public enum RateLimitAlgorithm { SlidingWindow, TokenBucket, LeakyBucket }
```

**3.2 限流器实现**

- 滑动窗口算法：`SlidingWindowRateLimiter`
- 限流状态存储：`MemoryRateLimiterStore`（单机）

### Phase 4: Dynamic Routing (P0)

**4.1 ConfigCenter 集成**

从 ConfigCenter 加载路由配置：
- 配置命名空间：`gateway.routes`
- 配置键：`routes.json`, `authentication.json`

**4.2 路由热更新**

- 后台服务监听配置变更
- 配置变更时重建路由匹配树
- 无中断更新

### Phase 5: Request/Response Transform (P1)

**5.1 请求转换**
- 路径重写：去除/添加前缀
- Header 转换：添加 `X-Forwarded-*` 系列
- 添加网关标识：`X-Gateway-Request-Id`

**5.2 响应转换**
- 统一错误响应格式
- 添加响应 Header：`X-Gateway-Time`

### Phase 6: Caching (P1)

**6.1 缓存策略**
- 仅缓存 GET 请求
- 支持 TTL 配置
- 支持 VaryBy Header/Query

### Phase 7: Audit Logging (P1)

**7.1 审计日志字段**
- TraceId, RequestPath, Method, ClientIp
- UserId, AuthenticationMethod
- TargetService, TargetInstance
- StatusCode, Duration, ErrorMessage

### Phase 8: WebSocket Support (P2)

**8.1 WebSocket 透传**
- 建立双向连接
- 消息透传

### Phase 9: gRPC Support (P2)

**9.1 gRPC 代理**
- gRPC Channel 代理
- 元数据透传

## Middleware Pipeline

```
请求 → ExceptionHandler → Metrics → Audit → Authentication → RateLimit → Cache(Read) → Routing → Cache(Write) → 响应
```

## Gateway Server Integration

```csharp
// Program.cs 结构
var builder = WebApplication.CreateBuilder(args);

// 聚合服务
builder.Services.AddServiceDiscoveryServer(builder.Configuration);
builder.Services.AddConfigCenterServer(builder.Configuration);
builder.Services.AddGatewayServer(builder.Configuration)
    .WithServiceDiscovery()
    .WithConfigCenter()
    .WithAuthentication()
    .WithRateLimit()
    .WithCache()
    .WithAuditLog();

// 协议支持
builder.Services.AddGatewayHttp();
builder.Services.AddGatewayWebSocket();
builder.Services.AddGatewayGrpc();

var app = builder.Build();
app.UseGateway();
app.MapGatewayManagementApi();
app.MapServiceDiscoveryApi();
app.MapConfigCenterApi();
app.Run();
```

## Configuration Example

```json
{
  "Gateway": {
    "ServiceName": "api-gateway",
    "DynamicRouting": {
      "Enabled": true,
      "RefreshIntervalSeconds": 30,
      "Source": "ConfigCenter"
    },
    "DefaultTimeout": "00:00:30",
    "EnableMetrics": true,
    "EnableAuditLog": true
  }
}
```

## Critical Files

| 文件 | 用途 |
|------|------|
| `ServiceCommunication.Abstractions/IServiceClient.cs` | 服务调用抽象，网关转发复用 |
| `ServiceCommunication.Http/HttpServiceClient.cs` | HTTP 转发实现参考 |
| `ServiceCommunication.Core/ServiceDiscovery/ServiceInstanceSelector.cs` | 实例选择器，网关复用 |
| `ServiceCommunication.Core/Resilience/CircuitBreaker.cs` | 熔断器，网关复用 |
| `ServiceDiscovery.Server/Extensions/ServiceDiscoveryServerExtensions.cs` | DI 扩展模式参考 |
| `ConfigCenter.Abstractions/IConfigDiscovery.cs` | 配置获取接口，动态路由使用 |

## Verification

1. **单元测试**
   - 路由匹配引擎测试
   - 认证处理器测试
   - 限流器测试

2. **集成测试**
   - HTTP 转发测试
   - 认证流程测试
   - 动态路由更新测试

3. **端到端测试**
   - 启动 Gateway.Server
   - 注册后端服务到 ServiceDiscovery
   - 配置路由到 ConfigCenter
   - 通过网关访问后端服务
