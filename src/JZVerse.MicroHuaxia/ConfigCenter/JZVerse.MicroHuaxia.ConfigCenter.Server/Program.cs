using JZVerse.MicroHuaxia.ConfigCenter.Server.Extensions;
using JZVerse.MicroHuaxia.Observability.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.ServiceDiscovery.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 统一可观测性
builder.Services.AddObservability(builder.Configuration);

// 添加配置中心服务
builder.Services.AddConfigCenterServer(builder.Configuration);

// 服务发现 - 注册到 SD（软依赖，SD 不可用时跳过注册继续运行）
builder.Services.AddServiceDiscoveryClient(builder.Configuration);

// 添加控制器
builder.Services.AddControllers();

// 添加 OpenAPI
builder.Services.AddOpenApi();

// 健康检查
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseObservability();

// 开发环境启用 OpenAPI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
