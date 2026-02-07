# 服务发现中心本地开发体验增强计划

## 概述

为 JZVerse.MicroHuaxia.ServiceDiscovery 添加类似 .NET Aspire 的本地开发/调试体验，包括 Dashboard 可视化面板、AppHost 服务编排和 Telemetry 可观测性集成。

## 技术决策

| 功能 | 技术选型 | 理由 |
|------|----------|------|
| Dashboard | Blazor Server + SignalR | 与 .NET 技术栈一致，实时双向通信 |
| 服务编排 | AppHost + CLI 双模式 | 代码方式+配置文件方式，灵活适配 |
| 可观测性 | OpenTelemetry | 行业标准，可导出到任意平台 |

## 新增项目结构

```
src/JZVerse.MicroHuaxia/
├── JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry/      # 可观测性模块
├── JZVerse.MicroHuaxia.ServiceDiscovery.Dashboard/      # Dashboard 面板
├── JZVerse.MicroHuaxia.ServiceDiscovery.Hosting/        # AppHost 核心库
└── JZVerse.MicroHuaxia.ServiceDiscovery.Hosting.Cli/    # CLI 工具
```

---

## 第一阶段: Telemetry 可观测性模块

### 文件清单

```
JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry/
├── Configuration/
│   └── TelemetryOptions.cs              # 配置选项
├── Logging/
│   ├── LogEntry.cs                      # 日志模型
│   ├── InMemoryLogStore.cs              # 内存存储
│   └── TelemetryLoggerProvider.cs       # 日志提供者
├── Tracing/
│   ├── ServiceDiscoveryActivitySource.cs # 活动源
│   └── SpanAttributes.cs                # Span 属性常量
├── Metrics/
│   ├── ServiceDiscoveryMetrics.cs       # 指标收集器
│   └── MetricNames.cs                   # 指标名称常量
├── Dashboard/
│   ├── ITelemetryDataProvider.cs        # 数据提供者接口
│   └── TelemetryDataProvider.cs         # 实现
└── Extensions/
    └── TelemetryExtensions.cs           # DI 扩展
```

### 核心依赖

```xml
<PackageReference Include="OpenTelemetry" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.10.0" />
```

### 关键接口

```csharp
// 指标收集
public sealed class ServiceDiscoveryMetrics
{
    void RecordRegistration(string serviceName, string instanceId);
    void RecordDeregistration(string serviceName, string instanceId);
    void RecordHeartbeat(string serviceName, string instanceId);
    void RecordHealthCheck(string serviceName, string instanceId, bool success, double durationMs);
    void RecordDiscoveryRequest(string serviceName, int instanceCount, double durationMs);
}

// 追踪活动源
public static class ServiceDiscoveryActivitySource
{
    Activity? StartRegisterActivity(string serviceName, string instanceId);
    Activity? StartDeregisterActivity(string instanceId);
    Activity? StartHealthCheckActivity(string serviceName, string instanceId);
    Activity? StartDiscoverActivity(string serviceName);
}

// Dashboard 数据提供者
public interface ITelemetryDataProvider
{
    LogQueryResult QueryLogs(LogQuery query);
    LogStatistics GetLogStatistics();
    Task<TraceListResult> GetTracesAsync(TraceQuery query, CancellationToken ct);
    Task<MetricSnapshot> GetMetricsSnapshotAsync(CancellationToken ct);
}
```

### 需要修改的现有文件

1. **ServiceRegistry.cs** - 添加追踪和指标记录
2. **HealthCheckManager.cs** - 添加健康检查追踪
3. **ServiceDiscoveryService.cs** - 添加发现请求追踪
4. **Server/Program.cs** - 添加 Telemetry 服务注册

---

## 第二阶段: Dashboard 可视化面板

### 文件清单

```
JZVerse.MicroHuaxia.ServiceDiscovery.Dashboard/
├── Components/
│   ├── App.razor
│   ├── Routes.razor
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   └── NavMenu.razor
│   ├── Pages/
│   │   ├── Index.razor                  # 总览
│   │   ├── Services.razor               # 服务列表
│   │   ├── ServiceDetail.razor          # 服务详情
│   │   ├── Health.razor                 # 健康监控
│   │   ├── Logs.razor                   # 日志聚合
│   │   └── Traces.razor                 # 分布式追踪
│   └── Shared/
│       ├── ServiceCard.razor
│       ├── HealthBadge.razor
│       └── EventTimeline.razor
├── Hubs/
│   └── ServiceDiscoveryHub.cs           # SignalR Hub
├── Services/
│   ├── IDashboardService.cs
│   └── DashboardService.cs
├── EventListeners/
│   └── DashboardEventListener.cs        # 事件转发
├── Extensions/
│   └── ServiceDiscoveryDashboardExtensions.cs
└── wwwroot/
    └── css/dashboard.css
```

### 核心依赖

```xml
<PackageReference Include="MudBlazor" Version="7.22.0" />
<PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.0-rc.2.*" />
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.0-rc.2.*" />
```

### 关键接口

```csharp
// Dashboard 服务
public interface IDashboardService
{
    Task<IReadOnlyList<ServiceStatistics>> GetServicesStatisticsAsync(CancellationToken ct);
    Task<ServiceInstance?> GetServiceInstanceAsync(string instanceId, CancellationToken ct);
    Task<ClusterStatistics> GetClusterStatisticsAsync(CancellationToken ct);
}

// 事件监听器 - 集成现有 IServiceEventPublisher
public class DashboardEventListener : IServiceEventListener
{
    // 将 ServiceEvent 转发到 SignalR 客户端
    Task OnEventAsync(ServiceEvent @event, CancellationToken ct);
}

// 扩展方法
public static class ServiceDiscoveryDashboardExtensions
{
    // 嵌入模式
    IServiceCollection AddServiceDiscoveryDashboard(this IServiceCollection services);
    
    // 独立模式
    IServiceCollection AddServiceDiscoveryDashboardStandalone(
        this IServiceCollection services, string serverUrl);
    
    // 映射路由
    IEndpointRouteBuilder MapServiceDiscoveryDashboard(
        this IEndpointRouteBuilder endpoints, string basePath = "/dashboard");
}
```

### 页面功能

| 页面 | 路由 | 功能 |
|------|------|------|
| 总览 | `/dashboard` | 集群健康率、服务数统计、最近事件时间线 |
| 服务列表 | `/dashboard/services` | 服务分组、实例卡片、健康状态筛选 |
| 健康监控 | `/dashboard/health` | 健康状态矩阵、检查历史图表 |
| 日志聚合 | `/dashboard/logs` | 日志搜索、级别筛选、实时滚动 |
| 追踪视图 | `/dashboard/traces` | 追踪列表、瀑布图、服务依赖图 |

---

## 第三阶段: AppHost 服务编排

### 文件清单

```
JZVerse.MicroHuaxia.ServiceDiscovery.Hosting/
├── Abstractions/
│   ├── IAppHostBuilder.cs
│   ├── IResourceBuilder.cs
│   └── IServiceResource.cs
├── Builders/
│   ├── AppHostBuilder.cs
│   └── ServiceResourceBuilder.cs
├── Resources/
│   ├── ProjectResource.cs
│   ├── ExecutableResource.cs
│   └── ExternalServiceResource.cs
├── Lifecycle/
│   ├── ProcessLifecycleManager.cs
│   ├── DependencyResolver.cs
│   └── HealthCheckOrchestrator.cs
├── Configuration/
│   ├── AppHostOptions.cs
│   ├── PortAllocator.cs
│   └── EnvironmentInjector.cs
└── Extensions/
    └── AppHostBuilderExtensions.cs

JZVerse.MicroHuaxia.ServiceDiscovery.Hosting.Cli/
├── Commands/
│   ├── RunCommand.cs
│   ├── StopCommand.cs
│   ├── ListCommand.cs
│   └── ValidateCommand.cs
├── Configuration/
│   └── ConfigurationLoader.cs
└── Program.cs
```

### Builder API 示例

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// 服务发现中心
var discovery = builder.AddServiceDiscoveryServer("service-discovery", port: 8500);

// 后端服务
var api = builder.AddProject("api-service", "../ApiService/ApiService.csproj")
    .WithHttpEndpoint(port: 5001)
    .WithReference(discovery)
    .WithServiceDiscovery(opt => opt.Tags.Add("backend"))
    .WithHealthCheck("/health")
    .WaitFor(discovery);

// 前端服务
var web = builder.AddProject("web-app", "../WebApp/WebApp.csproj")
    .WithHttpEndpoint(port: 3000)
    .WithReference(api, envVarPrefix: "ApiService")
    .WaitFor(api);

await builder.BuildAsync().RunAsync();
```

### CLI 工具

```bash
# 安装
dotnet tool install --global JZVerse.MicroHuaxia.ServiceDiscovery.Cli

# 使用
mh-orchestrator run --config apphost.yaml
mh-orchestrator stop --all
mh-orchestrator list
mh-orchestrator validate --config apphost.yaml
```

### 配置文件格式 (YAML)

```yaml
resources:
  - name: service-discovery
    type: project
    projectPath: ../ServiceDiscovery.Server/ServiceDiscovery.Server.csproj
    endpoints:
      - name: http
        port: 8500

  - name: api-service
    type: project
    projectPath: ../ApiService/ApiService.csproj
    endpoints:
      - name: http
        port: 5001
    dependencies:
      waitFor: [service-discovery]
      references:
        - resource: service-discovery
          envPrefix: ServiceDiscovery
    serviceDiscovery:
      enabled: true
      tags: [backend]
    healthCheck:
      endpoint: /health

options:
  autoAllocatePorts: true
  portRange: { start: 5000, end: 6000 }
```

---

## 实施步骤

### 步骤 1: 创建 Telemetry 项目
1. 创建项目文件和目录结构
2. 实现 TelemetryOptions 配置
3. 实现 InMemoryLogStore 和 TelemetryLoggerProvider
4. 实现 ServiceDiscoveryActivitySource 追踪
5. 实现 ServiceDiscoveryMetrics 指标
6. 实现 TelemetryExtensions 扩展方法
7. 修改 ServiceRegistry/HealthCheckManager 添加追踪

### 步骤 2: 创建 Dashboard 项目
1. 创建 Blazor Server 项目
2. 配置 MudBlazor UI 框架
3. 实现 SignalR Hub
4. 实现 DashboardEventListener 与现有事件机制集成
5. 创建页面组件 (Index, Services, Health, Logs, Traces)
6. 实现扩展方法并集成到 Server

### 步骤 3: 创建 AppHost 项目
1. 创建 Hosting 核心库项目
2. 实现 IAppHostBuilder 和资源模型
3. 实现 ProcessLifecycleManager 进程管理
4. 实现 PortAllocator 端口分配
5. 实现 EnvironmentInjector 环境变量注入
6. 实现 DependencyResolver 依赖解析

### 步骤 4: 创建 CLI 工具
1. 创建 CLI 项目
2. 实现 ConfigurationLoader (JSON/YAML)
3. 实现 run/stop/list/validate 命令
4. 配置为全局 dotnet 工具

### 步骤 5: 集成测试
1. 更新解决方案文件
2. 验证 Telemetry 指标收集
3. 验证 Dashboard 实时更新
4. 验证 AppHost 服务编排
5. 验证 CLI 工具功能

---

## 验证方案

### Telemetry 验证
```bash
# 启动服务器
cd src/JZVerse.MicroHuaxia/JZVerse.MicroHuaxia.ServiceDiscovery.Server
dotnet run

# 注册服务触发追踪
curl -X POST http://localhost:8500/api/v1/services/register \
  -H "Content-Type: application/json" \
  -d '{"serviceName":"test","host":"localhost","port":9000}'

# 检查日志/追踪输出
# 检查 /api/telemetry/logs/statistics
# 检查 /api/telemetry/metrics/snapshot
```

### Dashboard 验证
```bash
# 启动带 Dashboard 的服务器
dotnet run

# 访问 Dashboard
open http://localhost:8500/dashboard

# 注册/注销服务，观察实时更新
```

### AppHost 验证
```bash
# 代码方式
cd examples/AppHost.Example
dotnet run

# CLI 方式
mh-orchestrator run --config apphost.yaml
mh-orchestrator list
mh-orchestrator stop --all
```

---

## 关键文件路径

| 模块 | 关键文件 |
|------|----------|
| Telemetry | `Telemetry/Extensions/TelemetryExtensions.cs` |
| Dashboard | `Dashboard/Extensions/ServiceDiscoveryDashboardExtensions.cs` |
| AppHost | `Hosting/Builders/AppHostBuilder.cs` |
| CLI | `Hosting.Cli/Commands/RunCommand.cs` |
| 现有集成 | `Core/Services/ServiceRegistry.cs` (需修改) |
