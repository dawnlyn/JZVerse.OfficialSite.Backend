using JZVerse.MicroHuaxia.Security;
using JZVerse.MicroHuaxia.Observability.AspNetCore.Extensions;

// 创建应用构建器
var builder = WebApplication.CreateBuilder(args);

// 配置本地启动配置（仅用于启动）
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

// 添加可观测性
builder.Services.AddObservability(builder.Configuration);

// 添加安全平台核心服务
builder.Services.AddSecurityPlatform();

// 添加控制器
builder.Services.AddControllers();

// 添加 API 文档
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 添加健康检查
builder.Services.AddHealthChecks();

// 构建应用
var app = builder.Build();

// 使用可观测性中间件
app.UseObservability();

// 开发环境启用 API 文档
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 使用路由
app.UseRouting();

// 使用安全中间件
app.UseAuthentication();
app.UseAuthorization();

// 映射控制器
app.MapControllers();

// 映射健康检查
app.MapHealthChecks("/health");

// 启动应用
app.Run();
