using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;

/// <summary>
/// Dashboard 数据服务接口
/// </summary>
public interface IDashboardDataService
{
    /// <summary>
    /// 获取 Dashboard 总览数据
    /// </summary>
    Task<DashboardOverview> GetOverviewAsync(CancellationToken cancellationToken = default);
}
