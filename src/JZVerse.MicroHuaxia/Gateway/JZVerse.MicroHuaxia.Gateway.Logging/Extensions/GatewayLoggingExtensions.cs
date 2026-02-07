using JZVerse.MicroHuaxia.Gateway.Logging.Api;
using JZVerse.MicroHuaxia.Gateway.Logging.Classification;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using JZVerse.MicroHuaxia.Gateway.Logging.Exporters;
using JZVerse.MicroHuaxia.Gateway.Logging.Middleware;
using JZVerse.MicroHuaxia.Gateway.Logging.Providers;
using JZVerse.MicroHuaxia.Gateway.Logging.Replay;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage.File;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage.InMemory;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Extensions;

/// <summary>
/// Gateway 日志系统的 DI 扩展方法
/// </summary>
public static class GatewayLoggingExtensions
{
    /// <summary>
    /// 添加 Gateway 日志系统（从配置加载）
    /// </summary>
    public static IServiceCollection AddGatewayLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new GatewayLoggingOptions();
        configuration.GetSection(GatewayLoggingOptions.SectionName).Bind(options);

        return services.AddGatewayLogging(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.MinimumLevel = options.MinimumLevel;
            opt.IncludeScopes = options.IncludeScopes;
            opt.IncludeStackTrace = options.IncludeStackTrace;
            opt.EnrichWithTraceContext = options.EnrichWithTraceContext;
            opt.IgnorePaths = options.IgnorePaths;
            opt.ClassificationStrategy = options.ClassificationStrategy;
            opt.TimeClassificationGranularity = options.TimeClassificationGranularity;
            opt.EnableOtlpExporter = options.EnableOtlpExporter;
            opt.EnableHttpApi = options.EnableHttpApi;
            opt.EnableWebSocketStream = options.EnableWebSocketStream;

            // 复制嵌套配置
            CopyInMemoryOptions(options.InMemoryStore, opt.InMemoryStore);
            CopyFileOptions(options.FileStore, opt.FileStore);
            CopySqliteOptions(options.SqliteStore, opt.SqliteStore);
            CopyOtlpOptions(options.OtlpExporter, opt.OtlpExporter);
        });
    }

    /// <summary>
    /// 添加 Gateway 日志系统（委托配置）
    /// </summary>
    public static IServiceCollection AddGatewayLogging(
        this IServiceCollection services,
        Action<GatewayLoggingOptions>? configure = null)
    {
        var options = new GatewayLoggingOptions();
        configure?.Invoke(options);

        // 注册配置选项
        services.Configure<GatewayLoggingOptions>(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.MinimumLevel = options.MinimumLevel;
            opt.IncludeScopes = options.IncludeScopes;
            opt.IncludeStackTrace = options.IncludeStackTrace;
            opt.EnrichWithTraceContext = options.EnrichWithTraceContext;
            opt.IgnorePaths = options.IgnorePaths;
            opt.ClassificationStrategy = options.ClassificationStrategy;
            opt.TimeClassificationGranularity = options.TimeClassificationGranularity;
            opt.EnableOtlpExporter = options.EnableOtlpExporter;
            opt.EnableHttpApi = options.EnableHttpApi;
            opt.EnableWebSocketStream = options.EnableWebSocketStream;

            CopyInMemoryOptions(options.InMemoryStore, opt.InMemoryStore);
            CopyFileOptions(options.FileStore, opt.FileStore);
            CopySqliteOptions(options.SqliteStore, opt.SqliteStore);
            CopyOtlpOptions(options.OtlpExporter, opt.OtlpExporter);
        });

        services.Configure<InMemoryLogStoreOptions>(opt =>
            CopyInMemoryOptions(options.InMemoryStore, opt));

        services.Configure<FileLogStoreOptions>(opt =>
            CopyFileOptions(options.FileStore, opt));

        services.Configure<SqliteLogStoreOptions>(opt =>
            CopySqliteOptions(options.SqliteStore, opt));

        services.Configure<OtlpLogExporterOptions>(opt =>
            CopyOtlpOptions(options.OtlpExporter, opt));

        // 注册核心服务
        return services.AddGatewayLoggingCore(options);
    }

    private static IServiceCollection AddGatewayLoggingCore(
        this IServiceCollection services,
        GatewayLoggingOptions options)
    {
        if (!options.Enabled)
        {
            return services;
        }

        // 注册内存存储（始终启用作为主存储）
        if (options.InMemoryStore.Enabled)
        {
            services.TryAddSingleton<InMemoryLogStore>();
            services.TryAddSingleton<ILogStore>(sp => sp.GetRequiredService<InMemoryLogStore>());
            services.TryAddSingleton<IStreamableLogStore>(sp => sp.GetRequiredService<InMemoryLogStore>());
        }

        // 注册文件存储相关服务
        if (options.FileStore.Enabled)
        {
            services.TryAddSingleton<LogFileRotator>();
            services.TryAddSingleton<LogFileCompressor>();
            services.TryAddSingleton<LogFileRetentionPolicy>();
            services.TryAddSingleton<FileLogStore>();
        }

        // 注册 SQLite 存储相关服务
        if (options.SqliteStore.Enabled)
        {
            services.TryAddSingleton<SqliteSchemaManager>();
            services.TryAddSingleton<SqliteLogStore>();
        }

        // 注册分类器
        services.TryAddSingleton<ILogClassifier, CompositeClassifier>();

        // 注册回放服务
        services.TryAddSingleton<ILogReplayService, LogReplayService>();
        services.TryAddSingleton<LogStreamProvider>();

        // 注册 API 提供者
        services.TryAddSingleton<IGatewayLogsProvider, GatewayLogsProvider>();

        // 注册 OTLP 导出器
        if (options.EnableOtlpExporter)
        {
            services.TryAddSingleton<OtlpLogExporter>();
        }

        // 注册日志提供者
        services.TryAddSingleton<GatewayLoggerProvider>();
        services.AddSingleton<ILoggerProvider>(sp =>
        {
            var provider = sp.GetRequiredService<GatewayLoggerProvider>();
            var env = sp.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
            provider.Environment = env?.EnvironmentName;
            return provider;
        });

        return services;
    }

    /// <summary>
    /// 使用 Gateway 日志中间件
    /// </summary>
    public static IApplicationBuilder UseGatewayLogging(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<GatewayLoggingOptions>>().Value;

        if (!options.Enabled)
        {
            return app;
        }

        // 使用日志中间件
        app.UseMiddleware<GatewayLoggingMiddleware>();

        return app;
    }

    #region 配置复制辅助方法

    private static void CopyInMemoryOptions(InMemoryLogStoreOptions source, InMemoryLogStoreOptions target)
    {
        target.Enabled = source.Enabled;
        target.MaxCapacity = source.MaxCapacity;
        target.EnableIndexing = source.EnableIndexing;
        target.IndexedFields = [.. source.IndexedFields];
        target.RetentionMinutes = source.RetentionMinutes;
        target.CleanupIntervalMinutes = source.CleanupIntervalMinutes;
    }

    private static void CopyFileOptions(FileLogStoreOptions source, FileLogStoreOptions target)
    {
        target.Enabled = source.Enabled;
        target.BasePath = source.BasePath;
        target.FileNamePattern = source.FileNamePattern;
        target.UseUtcTime = source.UseUtcTime;
        target.EnableRotation = source.EnableRotation;
        target.RotationStrategy = source.RotationStrategy;
        target.MaxFileSizeBytes = source.MaxFileSizeBytes;
        target.RotationIntervalHours = source.RotationIntervalHours;
        target.EnableCompression = source.EnableCompression;
        target.CompressionFormat = source.CompressionFormat;
        target.CompressAfterDays = source.CompressAfterDays;
        target.RetentionDays = source.RetentionDays;
        target.MaxTotalSizeBytes = source.MaxTotalSizeBytes;
        target.RetentionCheckIntervalHours = source.RetentionCheckIntervalHours;
        target.WriteBufferSize = source.WriteBufferSize;
        target.FlushIntervalSeconds = source.FlushIntervalSeconds;
    }

    private static void CopySqliteOptions(SqliteLogStoreOptions source, SqliteLogStoreOptions target)
    {
        target.Enabled = source.Enabled;
        target.DatabasePath = source.DatabasePath;
        target.EnablePartitioning = source.EnablePartitioning;
        target.PartitionStrategy = source.PartitionStrategy;
        target.CreateIndexes = source.CreateIndexes;
        target.IndexedColumns = [.. source.IndexedColumns];
        target.EnableFullTextSearch = source.EnableFullTextSearch;
        target.MaxConnectionPoolSize = source.MaxConnectionPoolSize;
        target.EnableWal = source.EnableWal;
        target.BatchInsertSize = source.BatchInsertSize;
        target.BatchFlushIntervalSeconds = source.BatchFlushIntervalSeconds;
        target.VacuumIntervalDays = source.VacuumIntervalDays;
        target.PartitionRetentionDays = source.PartitionRetentionDays;
    }

    private static void CopyOtlpOptions(OtlpLogExporterOptions source, OtlpLogExporterOptions target)
    {
        target.Endpoint = source.Endpoint;
        target.Protocol = source.Protocol;
        target.Headers = new Dictionary<string, string>(source.Headers);
        target.BatchSize = source.BatchSize;
        target.ExportIntervalSeconds = source.ExportIntervalSeconds;
        target.ExportTimeoutSeconds = source.ExportTimeoutSeconds;
        target.MaxQueueSize = source.MaxQueueSize;
        target.EnableRetry = source.EnableRetry;
        target.MaxRetryAttempts = source.MaxRetryAttempts;
    }

    #endregion
}
