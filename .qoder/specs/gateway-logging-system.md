# Gateway.Logging 统一日志收集与存储系统实现计划

## 概述

创建独立的 `JZVerse.MicroHuaxia.Gateway.Logging` 项目，提供多渠道日志存储、混合分类策略、完整回放功能和自动轮转机制。

## 目录结构

```
JZVerse.MicroHuaxia.Gateway.Logging/
├── Configuration/                    # 配置选项
│   ├── GatewayLoggingOptions.cs     # 主配置（SectionName = "Gateway:Logging"）
│   ├── InMemoryLogStoreOptions.cs   # 内存存储配置
│   ├── FileLogStoreOptions.cs       # 文件存储配置（轮转、压缩、保留）
│   ├── SqliteLogStoreOptions.cs     # SQLite 存储配置
│   └── OtlpLogExporterOptions.cs    # OTLP 导出配置
│
├── Storage/                          # 存储抽象层
│   ├── ILogStore.cs                 # 核心存储接口
│   ├── LogEntry.cs                  # 日志条目模型
│   ├── LogQuery.cs                  # 查询条件
│   ├── LogQueryResult.cs            # 查询结果
│   ├── LogStatistics.cs             # 统计信息
│   │
│   ├── InMemory/                    # 内存存储
│   │   ├── InMemoryLogStore.cs      # 实时查询、流式推送
│   │   └── CircularBuffer.cs        # 高性能循环缓冲区
│   │
│   ├── File/                        # 文件存储
│   │   ├── FileLogStore.cs          # 文件写入
│   │   ├── LogFileRotator.cs        # 轮转管理器
│   │   ├── LogFileCompressor.cs     # 压缩管理器
│   │   └── LogFileRetentionPolicy.cs # 保留策略
│   │
│   ├── Sqlite/                      # SQLite 存储
│   │   ├── SqliteLogStore.cs        # SQLite 操作
│   │   └── SqliteSchemaManager.cs   # 表结构和分区管理
│   │
│   └── Composite/                   # 组合存储
│       └── CompositeLogStore.cs     # 多存储写入
│
├── Classification/                   # 分类策略
│   ├── ILogClassifier.cs            # 分类器接口
│   └── CompositeClassifier.cs       # 混合分类（服务+级别+时间）
│
├── Replay/                           # 日志回放
│   ├── ILogReplayService.cs         # 回放服务接口
│   ├── LogReplayService.cs          # 基本回放
│   ├── TraceLogAggregator.cs        # TraceId 关联
│   ├── SlowQueryAnalyzer.cs         # 慢查询分析
│   └── LogStreamProvider.cs         # WebSocket 实时流
│
├── Exporters/                        # 导出器
│   └── OtlpLogExporter.cs           # OTLP 导出（Loki/ELK）
│
├── Api/                              # HTTP API
│   ├── IGatewayLogsProvider.cs      # 查询接口
│   ├── GatewayLogsProvider.cs       # 查询实现
│   └── Models/                      # API 模型
│
├── Providers/                        # 日志提供者
│   ├── GatewayLoggerProvider.cs     # ILoggerProvider 实现
│   └── GatewayLogger.cs             # ILogger 实现
│
├── Middleware/                       # 中间件
│   └── GatewayLoggingMiddleware.cs  # 请求日志拦截
│
├── Extensions/                       # DI 扩展
│   └── GatewayLoggingExtensions.cs  # 服务注册
│
└── JZVerse.MicroHuaxia.Gateway.Logging.csproj
```

## 核心设计

### 1. 配置选项 (GatewayLoggingOptions)

```csharp
public sealed class GatewayLoggingOptions
{
    public const string SectionName = "Gateway:Logging";
    
    public bool Enabled { get; set; } = true;
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public bool IncludeScopes { get; set; } = true;
    public bool IncludeStackTrace { get; set; } = true;
    public bool EnrichWithTraceContext { get; set; } = true;
    
    // 存储配置
    public InMemoryLogStoreOptions InMemoryStore { get; set; } = new();
    public FileLogStoreOptions FileStore { get; set; } = new();
    public SqliteLogStoreOptions SqliteStore { get; set; } = new();
    
    // 导出器配置
    public bool EnableOtlpExporter { get; set; }
    public OtlpLogExporterOptions OtlpExporter { get; set; } = new();
    
    // API 配置
    public bool EnableHttpApi { get; set; } = true;
    public bool EnableWebSocketStream { get; set; } = true;
}
```

### 2. 存储接口 (ILogStore)

```csharp
public interface ILogStore
{
    // 写入
    Task AddAsync(LogEntry entry, CancellationToken ct = default);
    Task AddBatchAsync(IEnumerable<LogEntry> entries, CancellationToken ct = default);
    
    // 查询
    Task<LogQueryResult> QueryAsync(LogQuery query, CancellationToken ct = default);
    Task<LogEntry?> GetByIdAsync(string logId, CancellationToken ct = default);
    Task<IReadOnlyList<LogEntry>> GetByTraceIdAsync(string traceId, CancellationToken ct = default);
    
    // 统计
    Task<LogStatistics> GetStatisticsAsync(CancellationToken ct = default);
    
    // 管理
    Task CleanupAsync(CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}
```

### 3. 文件存储配置 (FileLogStoreOptions)

```csharp
public sealed class FileLogStoreOptions
{
    public bool Enabled { get; set; } = true;
    public string BasePath { get; set; } = "./logs/gateway";
    public string FileNamePattern { get; set; } = "{Service}/{Level}/log-{Date}.log";
    
    // 轮转
    public bool EnableRotation { get; set; } = true;
    public RotationStrategy RotationStrategy { get; set; } = RotationStrategy.Both;
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    public int RotationIntervalHours { get; set; } = 24;
    
    // 压缩
    public bool EnableCompression { get; set; } = true;
    public int CompressAfterDays { get; set; } = 1;
    
    // 保留
    public int RetentionDays { get; set; } = 30;
    public long MaxTotalSizeBytes { get; set; } = 10L * 1024 * 1024 * 1024; // 10GB
}
```

### 4. 回放服务 (ILogReplayService)

```csharp
public interface ILogReplayService
{
    // 基本回放
    Task<LogQueryResult> ReplayAsync(LogQuery query, CancellationToken ct = default);
    
    // TraceId 关联回放
    Task<IReadOnlyList<LogEntry>> ReplayByTraceAsync(string traceId, CancellationToken ct = default);
    
    // 慢查询分析
    Task<IReadOnlyList<SlowQueryInfo>> AnalyzeSlowQueriesAsync(
        int thresholdMs, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    
    // 实时流式回放
    IAsyncEnumerable<LogEntry> StreamLogsAsync(LogQuery query, CancellationToken ct = default);
}
```

## 实现步骤

### Phase 1: 核心基础 (8 个文件)
1. 创建项目和 csproj
2. GatewayLoggingOptions + 嵌套配置类
3. LogEntry 日志条目模型
4. ILogStore 存储接口
5. LogQuery/LogQueryResult 查询模型
6. InMemoryLogStore 内存存储实现
7. GatewayLoggerProvider/GatewayLogger
8. GatewayLoggingExtensions DI 扩展

### Phase 2: 文件存储 (5 个文件)
1. FileLogStore 文件写入
2. LogFileRotator 轮转管理
3. LogFileCompressor 压缩管理
4. LogFileRetentionPolicy 保留策略
5. CompositeClassifier 混合分类

### Phase 3: SQLite 存储 (3 个文件)
1. SqliteLogStore 数据库操作
2. SqliteSchemaManager 表结构管理
3. CompositeLogStore 组合存储

### Phase 4: 回放功能 (4 个文件)
1. ILogReplayService 接口
2. LogReplayService 基本实现
3. TraceLogAggregator TraceId 关联
4. SlowQueryAnalyzer 慢查询分析

### Phase 5: 实时流与 API (4 个文件)
1. LogStreamProvider WebSocket 流
2. IGatewayLogsProvider API 接口
3. GatewayLogsProvider API 实现
4. LogsController API 控制器

### Phase 6: 导出与集成 (3 个文件)
1. OtlpLogExporter OTLP 导出
2. GatewayLoggingMiddleware 中间件
3. 单元测试

## 关键文件清单

| 优先级 | 文件 | 说明 |
|--------|------|------|
| 1 | Configuration/GatewayLoggingOptions.cs | 核心配置 |
| 2 | Storage/ILogStore.cs | 存储接口 |
| 3 | Storage/LogEntry.cs | 日志模型 |
| 4 | Storage/InMemory/InMemoryLogStore.cs | 内存存储 |
| 5 | Providers/GatewayLoggerProvider.cs | 日志提供者 |
| 6 | Extensions/GatewayLoggingExtensions.cs | DI 扩展 |
| 7 | Storage/File/FileLogStore.cs | 文件存储 |
| 8 | Storage/File/LogFileRotator.cs | 轮转管理 |
| 9 | Storage/Sqlite/SqliteLogStore.cs | SQLite 存储 |
| 10 | Replay/LogReplayService.cs | 回放服务 |

## 验证方式

1. **构建验证**: `dotnet build` 确保无编译错误
2. **单元测试**: 运行 Metrics 风格的测试套件
3. **集成测试**: 
   - 日志写入和查询
   - 文件轮转和压缩
   - TraceId 关联查询
   - WebSocket 实时流

## 配置示例

```json
{
  "Gateway": {
    "Logging": {
      "Enabled": true,
      "MinimumLevel": "Information",
      "InMemoryStore": { "MaxCapacity": 50000 },
      "FileStore": {
        "Enabled": true,
        "BasePath": "./logs/gateway",
        "EnableRotation": true,
        "MaxFileSizeBytes": 104857600,
        "RetentionDays": 30
      },
      "SqliteStore": {
        "Enabled": true,
        "DatabasePath": "./logs/gateway.db"
      },
      "EnableOtlpExporter": true,
      "OtlpExporter": { "Endpoint": "http://loki:3100/otlp" }
    }
  }
}
```
