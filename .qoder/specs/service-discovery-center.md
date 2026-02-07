# JZVerse.MicroHuaxia 服务注册发现中心实现计划

## 概述

在 JZVerse.MicroHuaxia 项目中实现一个自研的服务注册发现中心，支持：
- 混合式架构（独立注册中心 + 本地缓存 + 故障转移）
- 双注册模式（自注册 + 第三方注册）
- 双协议支持（HTTP REST + gRPC）
- 双健康检查（主动探测 + 被动心跳）
- 先单节点后扩展的部署策略

## 项目结构

```
src/JZVerse.MicroHuaxia/
├── JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions/   # 核心抽象层
├── JZVerse.MicroHuaxia.ServiceDiscovery.Core/           # 核心实现
├── JZVerse.MicroHuaxia.ServiceDiscovery.Server/         # 注册中心服务
├── JZVerse.MicroHuaxia.ServiceDiscovery.Client/         # 客户端 SDK
├── JZVerse.MicroHuaxia.ServiceDiscovery.AspNetCore/     # ASP.NET Core 集成
└── JZVerse.MicroHuaxia.ServiceDiscovery.HealthChecks/   # 健康检查扩展
```

## 实现步骤

### Phase 1: Abstractions 抽象层

**目标**: 定义所有接口、数据模型和契约

**文件清单**:
- `Models/ServiceInstance.cs` - 服务实例模型
- `Models/ServiceMetadata.cs` - 服务元数据
- `Models/HealthStatus.cs` - 健康状态枚举和检查结果
- `Models/ServiceRegistration.cs` - 注册请求模型
- `Models/ServiceQuery.cs` - 查询条件模型
- `Models/ServiceEvent.cs` - 事件模型
- `IServiceRegistry.cs` - 服务注册接口
- `IServiceDiscovery.cs` - 服务发现接口
- `IHealthChecker.cs` - 健康检查器接口
- `IHealthCheckManager.cs` - 健康检查管理器接口
- `IServiceInstanceRepository.cs` - 服务仓储接口
- `IServiceEventPublisher.cs` - 事件发布接口
- `IServiceDiscoveryCache.cs` - 缓存接口
- `ILoadBalancer.cs` - 负载均衡接口
- `Clustering/IClusterManager.cs` - 集群管理接口（预留）
- `Persistence/IServiceInstanceStore.cs` - 持久化接口（预留）

### Phase 2: Core 核心实现

**目标**: 实现核心业务逻辑

**文件清单**:
- `Repositories/InMemoryServiceInstanceRepository.cs` - 内存仓储
- `Services/ServiceRegistry.cs` - 服务注册实现
- `Services/ServiceDiscoveryService.cs` - 服务发现实现
- `Services/HealthCheckManager.cs` - 健康检查管理器
- `Services/ServiceEventPublisher.cs` - 事件发布器
- `LoadBalancing/RoundRobinLoadBalancer.cs` - 轮询负载均衡
- `LoadBalancing/RandomLoadBalancer.cs` - 随机负载均衡
- `LoadBalancing/WeightedRandomLoadBalancer.cs` - 加权随机负载均衡
- `Caching/InMemoryServiceDiscoveryCache.cs` - 内存缓存

### Phase 3: HealthChecks 健康检查

**目标**: 实现可插拔的健康检查器

**文件清单**:
- `HttpHealthChecker.cs` - HTTP 健康检查器
- `GrpcHealthChecker.cs` - gRPC 健康检查器（预留）
- `TcpHealthChecker.cs` - TCP 健康检查器（预留）

### Phase 4: Server 注册中心服务

**目标**: 实现独立的注册中心服务端

**文件清单**:
- `Controllers/ServiceRegistryController.cs` - REST API 控制器
- `Controllers/HealthController.cs` - 健康端点控制器
- `Grpc/Protos/service_discovery.proto` - gRPC 协议定义
- `Grpc/ServiceDiscoveryGrpcService.cs` - gRPC 服务实现
- `Extensions/ServiceDiscoveryServerExtensions.cs` - DI 扩展
- `BackgroundServices/HealthCheckBackgroundService.cs` - 后台服务
- `Program.cs` - 入口程序
- `appsettings.json` - 配置文件

### Phase 5: Client 客户端 SDK

**目标**: 实现客户端 SDK，支持自动注册和服务发现

**文件清单**:
- `Configuration/ServiceDiscoveryClientOptions.cs` - 客户端配置
- `Http/HttpServiceDiscoveryClient.cs` - HTTP 客户端
- `Grpc/GrpcServiceDiscoveryClient.cs` - gRPC 客户端（预留）
- `BackgroundServices/ServiceRegistrationBackgroundService.cs` - 自动注册服务

### Phase 6: AspNetCore 集成

**目标**: 提供 ASP.NET Core 无缝集成

**文件清单**:
- `Extensions/ServiceDiscoveryClientExtensions.cs` - 客户端 DI 扩展
- `Middleware/ServiceDiscoveryMiddleware.cs` - 中间件（可选）

## 核心数据模型

### ServiceInstance
```csharp
public sealed class ServiceInstance
{
    public string InstanceId { get; init; }
    public string ServiceName { get; init; }
    public string Version { get; init; }
    public string Host { get; init; }
    public int Port { get; init; }
    public string Scheme { get; init; }
    public string? BasePath { get; init; }
    public HashSet<string> Tags { get; init; }
    public ServiceMetadata Metadata { get; init; }
    public HealthStatus Health { get; set; }
    public DateTimeOffset RegisteredAt { get; init; }
    public DateTimeOffset LastHeartbeatAt { get; set; }
    public int FailureCount { get; set; }
    public int Weight { get; init; }
    public bool Enabled { get; set; }
}
```

### HealthStatus
```csharp
public enum HealthStatus
{
    Unknown = 0,
    Healthy = 1,
    Unhealthy = 2,
    Degraded = 3,
    Starting = 4,
    Stopping = 5
}
```

## HTTP REST API 端点

```
POST   /api/v1/services/register              # 注册服务
DELETE /api/v1/services/{instanceId}          # 注销服务
PUT    /api/v1/services/{instanceId}/heartbeat # 心跳
PUT    /api/v1/services/{instanceId}/health   # 更新健康状态
GET    /api/v1/services                       # 获取服务列表
GET    /api/v1/services/{serviceName}         # 获取服务实例
GET    /api/v1/services/{serviceName}/instance # 负载均衡获取实例
POST   /api/v1/services/discover              # 条件查询
GET    /api/v1/instances/{instanceId}         # 获取实例详情
GET    /api/v1/health                         # 注册中心健康状态
```

## 配置示例

### 服务端配置
```json
{
  "ServiceDiscovery": {
    "Server": {
      "ServiceName": "service-discovery-server",
      "HttpPort": 8500,
      "GrpcPort": 8501,
      "HealthCheckIntervalSeconds": 10,
      "HeartbeatTimeoutSeconds": 30,
      "LoadBalancer": "RoundRobin"
    }
  }
}
```

### 客户端配置
```json
{
  "ServiceDiscovery": {
    "Client": {
      "ServerUrls": ["http://localhost:8500"],
      "Service": {
        "ServiceName": "my-service",
        "Host": "localhost",
        "Port": 5000,
        "AutoRegister": true
      },
      "Heartbeat": {
        "Enabled": true,
        "IntervalSeconds": 30
      }
    }
  }
}
```

## 健康检查机制

### 主动检查（注册中心发起）
- 定期扫描所有注册的服务实例
- 根据协议选择健康检查器（HTTP/gRPC/TCP）
- 调用服务健康端点，记录结果
- 连续失败达阈值（默认3次）标记为不健康

### 被动检查（服务心跳）
- 服务定期发送心跳（默认30秒）
- 更新最后心跳时间
- 心跳超时（默认90秒）标记为不健康

### 状态转换
```
Unknown → Starting → Healthy ⟷ Degraded
                ↓         ↓
              Unhealthy ← ┘
```

## 扩展预留

### 集群模式接口
```csharp
public interface IClusterManager
{
    Task<bool> JoinClusterAsync(ClusterNode node, CancellationToken ct);
    Task<bool> LeaveClusterAsync(CancellationToken ct);
    Task<IReadOnlyList<ClusterNode>> GetNodesAsync(CancellationToken ct);
    Task SyncDataAsync(CancellationToken ct);
}
```

### 持久化接口
```csharp
public interface IServiceInstanceStore
{
    Task SaveSnapshotAsync(IReadOnlyList<ServiceInstance> instances, CancellationToken ct);
    Task<IReadOnlyList<ServiceInstance>> LoadSnapshotAsync(CancellationToken ct);
}
```

## 验证计划

### 单元测试
- 测试服务注册/注销流程
- 测试服务发现和负载均衡
- 测试健康检查状态转换
- 测试缓存和事件机制

### 集成测试
1. 启动注册中心服务
2. 启动测试客户端服务，验证自动注册
3. 验证服务发现返回正确实例
4. 模拟服务下线，验证健康检查
5. 测试心跳超时处理
6. 测试多实例负载均衡

### 手动验证
```bash
# 启动注册中心
dotnet run --project src/JZVerse.MicroHuaxia/JZVerse.MicroHuaxia.ServiceDiscovery.Server

# 注册服务
curl -X POST http://localhost:8500/api/v1/services/register \
  -H "Content-Type: application/json" \
  -d '{"serviceName":"test-service","host":"localhost","port":5000}'

# 查询服务
curl http://localhost:8500/api/v1/services/test-service

# 发送心跳
curl -X PUT http://localhost:8500/api/v1/services/{instanceId}/heartbeat
```

## 依赖项

```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0-rc.2" />
<PackageReference Include="Grpc.AspNetCore" Version="2.x" />
<PackageReference Include="Microsoft.Extensions.Http" Version="10.x" />
```

## 实现优先级

1. **P0 - 必须实现**: Abstractions, Core (仓储、注册、发现), Server (HTTP API), HealthChecks (HTTP)
2. **P1 - 重要功能**: Client SDK, AspNetCore 集成, 心跳机制
3. **P2 - 增强功能**: gRPC 支持, 更多负载均衡策略, 事件通知
4. **P3 - 预留扩展**: 集群模式, 持久化, TCP 健康检查
