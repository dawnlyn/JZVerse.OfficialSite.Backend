using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Dashboard.Core.Services;

/// <summary>
/// Dashboard 数据服务实现
/// </summary>
public class DashboardDataService : IDashboardDataService
{
    private readonly IServiceDiscoveryApiClient _serviceDiscoveryClient;
    private readonly IConfigCenterApiClient _configCenterClient;
    private readonly IGatewayApiClient _gatewayClient;
    private readonly ILogger<DashboardDataService> _logger;

    public DashboardDataService(
        IServiceDiscoveryApiClient serviceDiscoveryClient,
        IConfigCenterApiClient configCenterClient,
        IGatewayApiClient gatewayClient,
        ILogger<DashboardDataService> logger)
    {
        _serviceDiscoveryClient = serviceDiscoveryClient;
        _configCenterClient = configCenterClient;
        _gatewayClient = gatewayClient;
        _logger = logger;
    }

    public async Task<DashboardOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var overview = new DashboardOverview();

        // 并发获取各服务的统计数据
        var tasks = new List<Task>
        {
            GetServiceDiscoveryStatsAsync(overview, cancellationToken),
            GetConfigCenterStatsAsync(overview, cancellationToken),
            GetGatewayStatsAsync(overview, cancellationToken)
        };

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取部分统计数据时发生错误");
        }

        return overview;
    }

    private async Task GetServiceDiscoveryStatsAsync(DashboardOverview overview, CancellationToken cancellationToken)
    {
        try
        {
            overview.ServiceDiscovery = await _serviceDiscoveryClient.GetStatsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取服务发现统计数据失败");
            overview.ServiceDiscovery = new ServiceDiscoveryStats();
        }
    }

    private async Task GetConfigCenterStatsAsync(DashboardOverview overview, CancellationToken cancellationToken)
    {
        try
        {
            overview.ConfigCenter = await _configCenterClient.GetStatsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取配置中心统计数据失败");
            overview.ConfigCenter = new ConfigCenterStats();
        }
    }

    private async Task GetGatewayStatsAsync(DashboardOverview overview, CancellationToken cancellationToken)
    {
        try
        {
            overview.Gateway = await _gatewayClient.GetStatsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取网关统计数据失败");
            overview.Gateway = new GatewayStats();
        }
    }
}
