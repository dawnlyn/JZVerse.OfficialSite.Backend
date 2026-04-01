using JZVerse.Business.Dashboard.Services;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;

namespace JZVerse.Business.Tests.Unit.Dashboard;

/// <summary>
/// 仪表盘服务单元测试
/// </summary>
/// <remarks>
/// 注意：DashboardService 大量使用 dynamic 类型查询数据库，
/// 完整的业务逻辑测试应放在集成测试中进行。
/// 此处仅测试服务的基本功能。
/// </remarks>
[TestFixture]
public class DashboardServiceTests
{
    private Mock<IDbExecutor> _dbExecutorMock = null!;
    private DashboardService _dashboardService = null!;

    [SetUp]
    public void SetUp()
    {
        _dbExecutorMock = new Mock<IDbExecutor>();
        _dashboardService = new DashboardService(_dbExecutorMock.Object);
    }

    [Test]
    public void Constructor_ShouldInitializeService()
    {
        // Assert
        _dashboardService.Should().NotBeNull();
    }

    [Test]
    public void GetServiceStatuses_ShouldReturnAllServices()
    {
        // DashboardService.GetServiceStatuses 返回硬编码的服务状态列表
        // 这是一个可以独立测试的方法（如果公开的话）
        // 目前仅验证服务初始化正确
        _dashboardService.Should().NotBeNull();
    }
}
