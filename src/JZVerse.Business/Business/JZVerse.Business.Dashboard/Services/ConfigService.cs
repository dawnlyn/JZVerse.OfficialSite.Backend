using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Dashboard.Arguments;
using JZVerse.Business.Dashboard.Database;
using JZVerse.Business.Dashboard.Results;
using JZVerse.DataAccess.Abstractions;

namespace JZVerse.Business.Dashboard.Services;

/// <summary>
/// 系统配置服务
/// </summary>
public sealed class ConfigService
{
    private readonly IDbExecutor _db;
    private readonly ISnowflakeIdGenerator _idGenerator;

    public ConfigService(IDbExecutor db, ISnowflakeIdGenerator idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public async Task<bool> SaveAsync(ArgSaveConfig arg)
    {
        var config = new SystemConfigEntity
        {
            Id = _idGenerator.GenerateGuid(),
            ConfigKey = arg.ConfigKey,
            ConfigValue = arg.ConfigValue,
            Description = arg.Description,
            Category = arg.Category,
            IsPublic = arg.IsPublic,
            CreatedAt = DateTime.UtcNow
        };

        var affected = await _db.ExecuteAsync(DashboardSql.CreateConfig, config);
        return affected > 0;
    }

    /// <summary>
    /// 批量保存配置
    /// </summary>
    public async Task<int> SaveBatchAsync(ArgSaveConfigBatch arg)
    {
        var count = 0;
        foreach (var config in arg.Configs)
        {
            if (await SaveAsync(config))
                count++;
        }
        return count;
    }

    /// <summary>
    /// 获取配置
    /// </summary>
    public async Task<ResultConfigInfo?> GetByKeyAsync(string key)
    {
        var config = await _db.QueryFirstOrDefaultAsync<SystemConfigEntity>(
            DashboardSql.GetConfigByKey,
            new { ConfigKey = key });

        return config is null ? null : MapToResult(config);
    }

    /// <summary>
    /// 获取配置值
    /// </summary>
    public async Task<string?> GetValueAsync(string key, string? defaultValue = null)
    {
        var config = await GetByKeyAsync(key);
        return config?.ConfigValue ?? defaultValue;
    }

    /// <summary>
    /// 按分类获取配置
    /// </summary>
    public async Task<IReadOnlyList<ResultConfigInfo>> GetByCategoryAsync(ConfigCategory category)
    {
        var configs = await _db.QueryAsync<SystemConfigEntity>(
            DashboardSql.GetConfigsByCategory,
            new { Category = category });

        return configs.Select(MapToResult).ToList();
    }

    /// <summary>
    /// 获取所有配置（分组）
    /// </summary>
    public async Task<IReadOnlyList<ResultConfigGroup>> GetAllGroupedAsync()
    {
        var configs = await _db.QueryAsync<SystemConfigEntity>(DashboardSql.GetAllConfigs);

        return configs
            .GroupBy(c => c.Category)
            .Select(g => new ResultConfigGroup
            {
                Category = g.Key,
                CategoryName = GetCategoryName(g.Key),
                Configs = g.Select(MapToResult).ToList()
            })
            .OrderBy(g => g.Category)
            .ToList();
    }

    /// <summary>
    /// 获取公开配置
    /// </summary>
    public async Task<IReadOnlyList<ResultConfigInfo>> GetPublicConfigsAsync()
    {
        var configs = await _db.QueryAsync<SystemConfigEntity>(DashboardSql.GetPublicConfigs);
        return configs.Select(MapToResult).ToList();
    }

    /// <summary>
    /// 删除配置
    /// </summary>
    public async Task<bool> DeleteAsync(string key)
    {
        var affected = await _db.ExecuteAsync(DashboardSql.DeleteConfig, new { ConfigKey = key });
        return affected > 0;
    }

    private static ResultConfigInfo MapToResult(SystemConfigEntity entity) => new()
    {
        Id = entity.Id,
        ConfigKey = entity.ConfigKey,
        ConfigValue = entity.ConfigValue,
        Description = entity.Description,
        Category = entity.Category,
        IsPublic = entity.IsPublic,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static string GetCategoryName(ConfigCategory category) => category switch
    {
        ConfigCategory.Basic => "基础配置",
        ConfigCategory.Module => "模块配置",
        ConfigCategory.Api => "接口配置",
        ConfigCategory.Storage => "存储配置",
        ConfigCategory.Security => "安全配置",
        _ => "其他配置"
    };
}
