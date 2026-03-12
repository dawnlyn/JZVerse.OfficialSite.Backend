using JZVerse.Business.Dashboard.Arguments;
using JZVerse.Business.Dashboard.Database;
using JZVerse.Business.Dashboard.Results;
using JZVerse.Business.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Dashboard.Controllers;

/// <summary>
/// 系统配置控制器
/// </summary>
[ApiController]
[Route("api/v1/dashboard/config")]
[Authorize(Roles = "admin")]
public sealed class ConfigController : ControllerBase
{
    private readonly ConfigService _configService;

    public ConfigController(ConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    [HttpPost]
    public async Task<bool> SaveAsync([FromBody] ArgSaveConfig arg)
    {
        return await _configService.SaveAsync(arg);
    }

    /// <summary>
    /// 批量保存配置
    /// </summary>
    [HttpPost("batch")]
    public async Task<int> SaveBatchAsync([FromBody] ArgSaveConfigBatch arg)
    {
        return await _configService.SaveBatchAsync(arg);
    }

    /// <summary>
    /// 获取配置
    /// </summary>
    [HttpGet("{key}")]
    public async Task<ResultConfigInfo?> GetByKeyAsync(string key)
    {
        return await _configService.GetByKeyAsync(key);
    }

    /// <summary>
    /// 按分类获取配置
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<IReadOnlyList<ResultConfigInfo>> GetByCategoryAsync(ConfigCategory category)
    {
        return await _configService.GetByCategoryAsync(category);
    }

    /// <summary>
    /// 获取所有配置（分组）
    /// </summary>
    [HttpGet("grouped")]
    public async Task<IReadOnlyList<ResultConfigGroup>> GetAllGroupedAsync()
    {
        return await _configService.GetAllGroupedAsync();
    }

    /// <summary>
    /// 获取公开配置
    /// </summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IReadOnlyList<ResultConfigInfo>> GetPublicConfigsAsync()
    {
        return await _configService.GetPublicConfigsAsync();
    }

    /// <summary>
    /// 删除配置
    /// </summary>
    [HttpDelete("{key}")]
    public async Task<bool> DeleteAsync(string key)
    {
        return await _configService.DeleteAsync(key);
    }
}
