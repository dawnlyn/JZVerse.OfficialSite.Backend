using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Tests.Integration;

/// <summary>
/// 自定义 WebApplicationFactory 用于集成测试
/// </summary>
public class ServiceDiscoveryWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // 可以在这里替换服务为测试用的实现
            // 例如替换数据库为内存数据库等
        });
    }
}

/// <summary>
/// 集成测试基类
/// </summary>
[TestFixture]
public abstract class IntegrationTestBase
{
    private static ServiceDiscoveryWebApplicationFactory? _factory;
    protected HttpClient Client { get; private set; } = null!;
    protected ServiceDiscoveryWebApplicationFactory Factory => _factory!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory ??= new ServiceDiscoveryWebApplicationFactory();
    }

    [SetUp]
    public void SetUp()
    {
        Client = _factory!.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        Client?.Dispose();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory?.Dispose();
        _factory = null;
    }
}
