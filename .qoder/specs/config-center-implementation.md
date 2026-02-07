# JZVerse.MicroHuaxia 配置中心实现计划

## 概述

基于现有的 ServiceDiscovery 架构模式，创建一个完整的配置中心（ConfigCenter），支持配置 CRUD、版本管理、灰度发布、多种通知机制和持久化存储。

## 项目结构

```
src/JZVerse.MicroHuaxia/
├── JZVerse.MicroHuaxia.ConfigCenter.Abstractions/   # 抽象层
├── JZVerse.MicroHuaxia.ConfigCenter.Core/           # 核心实现
├── JZVerse.MicroHuaxia.ConfigCenter.Server/         # 服务端 API
├── JZVerse.MicroHuaxia.ConfigCenter.Client/         # 客户端 SDK
└── JZVerse.MicroHuaxia.ConfigCenter.AspNetCore/     # ASP.NET Core 集成
```

## 实施步骤

### Phase 1: 抽象层（Abstractions）

创建 `JZVerse.MicroHuaxia.ConfigCenter.Abstractions` 项目：

**数据模型（Models/）**：
| 模型 | 说明 |
|------|------|
| `ConfigApplication` | 应用实体（ApplicationId, Name, Owner, Metadata） |
| `ConfigEnvironment` | 环境实体（dev/test/staging/prod） |
| `ConfigNamespace` | 命名空间/配置组（Format: JSON/YAML/Properties） |
| `ConfigItem` | 配置项（Key, Value, Version, IsSecret, Comment） |
| `ConfigVersion` | 版本历史（OldValue, NewValue, ChangeType, Reason） |
| `GrayRelease` | 灰度发布（Rules, Status, RolloutPercentage） |
| `GrayReleaseRule` | 灰度规则（IP/Tag/ClientId/Percentage） |
| `ConfigQuery` | 查询条件 |
| `ConfigChangeEvent` | 变更事件 |
| `ClientInfo` | 客户端信息（用于灰度匹配） |

**枚举类型**：
- `ConfigFormat`（JSON, YAML, XML, Properties, TOML）
- `ConfigValueType`（String, Int, Bool, JSON, Array）
- `ConfigChangeType`（Created, Updated, Deleted, Rollback）
- `GrayReleaseStatus`（Draft, InProgress, Completed, Rollback）
- `GrayRuleType`（IP, Tag, ClientId, Percentage）
- `ConfigEventType`（ItemChanged, NamespacePublished, GrayReleaseStarted）

**核心接口**：
| 接口 | 职责 |
|------|------|
| `IConfigRegistry` | 配置 CRUD |
| `IConfigDiscovery` | 配置查询发现 |
| `IConfigVersionManager` | 版本管理和回滚 |
| `IConfigEventPublisher` | 事件发布订阅 |
| `IGrayReleaseManager` | 灰度发布管理 |
| `IConfigItemRepository` | 配置项仓储 |
| `IConfigVersionRepository` | 版本历史仓储 |
| `IGrayReleaseRepository` | 灰度发布仓储 |
| `IConfigCache` | 配置缓存 |
| `IConfigStore`（Persistence/） | 持久化存储 |
| `IConfigAccessControl`（预留） | 权限控制 |

### Phase 2: 核心实现（Core）

创建 `JZVerse.MicroHuaxia.ConfigCenter.Core` 项目：

**目录结构**：
```
Core/
├── Services/
│   ├── ConfigRegistry.cs
│   ├── ConfigDiscoveryService.cs
│   ├── ConfigVersionManager.cs
│   ├── ConfigEventPublisher.cs
│   └── GrayReleaseManager.cs
├── Repositories/
│   ├── InMemoryConfigItemRepository.cs
│   ├── InMemoryConfigVersionRepository.cs
│   └── InMemoryGrayReleaseRepository.cs
├── Caching/
│   └── ConfigCache.cs
├── Persistence/
│   └── FileConfigStore.cs
└── GrayRelease/
    └── GrayRuleEvaluator.cs
```

**关键实现**：
1. `InMemoryConfigItemRepository`：基于 `ConcurrentDictionary`，维护命名空间、环境、Key 三级索引
2. `ConfigRegistry`：配置 CRUD + 版本递增 + 事件发布 + 缓存失效
3. `GrayRuleEvaluator`：支持 IP 匹配、标签匹配、百分比灰度
4. `FileConfigStore`：JSON 文件持久化到 `data/config/{app}/{env}/{ns}.json`

### Phase 3: 服务端（Server）

创建 `JZVerse.MicroHuaxia.ConfigCenter.Server` 项目：

**API 端点**：
```
/api/v1/config
├── POST   /items                       # 创建/更新配置
├── DELETE /items/{itemId}              # 删除配置
├── GET    /items/{itemId}              # 获取配置详情
├── POST   /items/batch                 # 批量设置

/api/v1/discovery
├── POST   /query                       # 条件查询
├── GET    /applications/{appId}/environments/{envId}/namespaces/{nsId}
├── POST   /watch                       # 长轮询
├── ws://  /ws/config                   # WebSocket 推送

/api/v1/versions
├── GET    /items/{itemId}/history      # 版本历史
├── POST   /items/{itemId}/rollback     # 回滚

/api/v1/gray-releases
├── POST   /                            # 创建灰度发布
├── POST   /{releaseId}/start           # 开始灰度
├── POST   /{releaseId}/complete        # 完成灰度
├── POST   /{releaseId}/rollback        # 回滚灰度
```

**后台服务**：
- `ConfigSnapshotBackgroundService`：定期快照持久化
- `GrayReleaseMonitorService`：灰度发布状态监控

### Phase 4: 客户端 SDK（Client）

创建 `JZVerse.MicroHuaxia.ConfigCenter.Client` 项目：

**核心类**：
```
Client/
├── IConfigCenterClient.cs              # 客户端接口
├── Http/
│   └── HttpConfigCenterClient.cs       # HTTP 实现
├── Cache/
│   └── LocalConfigCache.cs             # 本地缓存
├── Polling/
│   └── ConfigPollingService.cs         # 轮询服务
└── WebSocket/
    └── ConfigWebSocketClient.cs        # WebSocket 客户端
```

**功能**：
- 多服务端故障转移
- 本地文件缓存（容灾）
- 三种通知模式：长轮询、WebSocket、主动拉取
- 配置变更回调

### Phase 5: ASP.NET Core 集成（AspNetCore）

创建 `JZVerse.MicroHuaxia.ConfigCenter.AspNetCore` 项目：

**扩展方法**：
```csharp
services.AddConfigCenter(options => {
    options.ServerUrls = ["http://localhost:5100"];
    options.ApplicationId = "my-app";
    options.EnvironmentId = "dev";
    options.NotificationMode = NotificationMode.LongPolling;
});
```

**IConfiguration 集成**：
- `ConfigCenterConfigurationProvider`：与 ASP.NET Core 配置系统集成
- 支持热重载

### Phase 6: 更新解决方案

更新 `JZVerse.OfficialSite.Backend.slnx`：
```xml
<Folder Name="/src/JZVerse.MicroHuaxia/">
    <!-- 添加 ConfigCenter 项目 -->
    <Project Path="src/JZVerse.MicroHuaxia/JZVerse.MicroHuaxia.ConfigCenter.Abstractions/..." />
    <Project Path="src/JZVerse.MicroHuaxia/JZVerse.MicroHuaxia.ConfigCenter.Core/..." />
    <Project Path="src/JZVerse.MicroHuaxia/JZVerse.MicroHuaxia.ConfigCenter.Server/..." />
    <Project Path="src/JZVerse.MicroHuaxia/JZVerse.MicroHuaxia.ConfigCenter.Client/..." />
    <Project Path="src/JZVerse.MicroHuaxia/JZVerse.MicroHuaxia.ConfigCenter.AspNetCore/..." />
</Folder>
```

### Phase 7: 测试

**单元测试**：
- `ConfigRegistryTests`
- `ConfigDiscoveryServiceTests`
- `ConfigVersionManagerTests`
- `GrayReleaseManagerTests`
- `InMemoryConfigItemRepositoryTests`
- `GrayRuleEvaluatorTests`

**集成测试**：
- `ConfigControllerTests`
- `ConfigDiscoveryControllerTests`
- `GrayReleaseFlowTests`
- `HttpConfigCenterClientTests`

## 编码规范

### 主构造函数（Primary Constructor）

**所有类优先使用主构造函数**，减少样板代码：

```csharp
// 推荐：主构造函数
public class ConfigRegistry(
    IConfigItemRepository _repository,
    IConfigEventPublisher _eventPublisher,
    ILogger<ConfigRegistry> _logger
) : IConfigRegistry
{
    public async Task<ConfigItem> SetAsync(ConfigItem item, CancellationToken ct)
    {
        _logger.LogInformation("Setting config item {Key}", item.Key);
        // ...
    }
}

// 避免：传统构造函数
public class ConfigRegistry : IConfigRegistry
{
    private readonly IConfigItemRepository _repository;
    private readonly IConfigEventPublisher _eventPublisher;
    private readonly ILogger<ConfigRegistry> _logger;

    public ConfigRegistry(
        IConfigItemRepository repository,
        IConfigEventPublisher eventPublisher,
        ILogger<ConfigRegistry> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }
}
```

### 目标类型 new()

**初始化时使用 `new()` 目标类型推断**，减少冗余类型声明：

```csharp
// 推荐：new() 目标类型推断
private readonly ConcurrentDictionary<string, ConfigItem> _items = new();
private readonly Lock _lock = new();
public List<string> Tags { get; init; } = [];
public Dictionary<string, string> Metadata { get; init; } = new();

// 避免：重复类型声明
private readonly ConcurrentDictionary<string, ConfigItem> _items = new ConcurrentDictionary<string, ConfigItem>();
```

### 集合初始化

```csharp
// 推荐：集合表达式
public List<string> Tags { get; init; } = [];
public HashSet<string> AllowedIps { get; init; } = [];

// 推荐：字典使用 new()
public Dictionary<string, string> Properties { get; init; } = new();
```

## 关键设计决策

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 存储模式 | 内存 + 文件持久化（默认） | 轻量、无额外依赖、启动快 |
| 通知机制 | 长轮询（默认）+ WebSocket + 拉取 | 兼容性好、实时性可选 |
| 版本管理 | 自增版本号 + 历史记录 | 简单可靠、支持回滚 |
| 灰度策略 | IP/Tag/ClientId/百分比 | 覆盖常见场景 |
| 缓存 | 内存缓存 + TTL | 减少存储压力 |
| 构造函数 | 主构造函数 | 减少样板代码，代码更简洁 |
| 对象初始化 | `new()` 目标类型推断 | 减少冗余类型声明 |

## 验证方法

1. **编译验证**：`dotnet build JZVerse.OfficialSite.Backend.slnx`
2. **单元测试**：`dotnet test tests/JZVerse.MicroHuaxia.ConfigCenter.Tests.Unit`
3. **集成测试**：`dotnet test tests/JZVerse.MicroHuaxia.ConfigCenter.Tests.Integration`
4. **手动验证**：
   - 启动 Server：`dotnet run --project src/.../ConfigCenter.Server`
   - 调用 API 创建配置
   - 验证版本历史
   - 测试灰度发布流程

## 参考文件

| 参考文件 | 用途 |
|----------|------|
| `ServiceDiscovery.Abstractions/IServiceRegistry.cs` | 接口设计模式 |
| `ServiceDiscovery.Core/Services/ServiceRegistry.cs` | 服务实现模式 |
| `ServiceDiscovery.Core/Repositories/InMemoryServiceInstanceRepository.cs` | 仓储和索引设计 |
| `ServiceDiscovery.Server/Controllers/ServiceDiscoveryController.cs` | API 设计模式 |
| `ServiceDiscovery.Client/Http/HttpServiceDiscoveryClient.cs` | 客户端故障转移 |
