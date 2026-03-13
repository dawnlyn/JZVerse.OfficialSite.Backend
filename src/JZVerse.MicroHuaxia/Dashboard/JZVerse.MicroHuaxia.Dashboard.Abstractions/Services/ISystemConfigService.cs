using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;

/// <summary>
/// 系统配置聚合服务接口
/// </summary>
public interface ISystemConfigService
{
    /// <summary>
    /// 获取所有微服务组件的运行时配置
    /// </summary>
    Task<List<ServiceConfigInfo>> GetAllConfigsAsync(CancellationToken cancellationToken = default);
}
