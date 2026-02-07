using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JZVerse.MicroHuaxia.ConfigCenter.Tests.Integration;

/// <summary>
/// 自定义 WebApplicationFactory 用于 ConfigCenter 集成测试
/// </summary>
public class ConfigCenterWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // 可以在这里替换服务为测试用的实现
            // 默认使用内存存储，无需额外配置
        });
    }
}

/// <summary>
/// 集成测试基类
/// </summary>
[TestFixture]
public abstract class IntegrationTestBase
{
    private static ConfigCenterWebApplicationFactory? _factory;
    protected HttpClient Client { get; private set; } = null!;
    protected ConfigCenterWebApplicationFactory Factory => _factory!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory ??= new ConfigCenterWebApplicationFactory();
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
