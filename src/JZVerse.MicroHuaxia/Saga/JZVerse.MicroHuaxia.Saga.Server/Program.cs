using JZVerse.MicroHuaxia.ConfigCenter.AspNetCore;
using JZVerse.MicroHuaxia.Observability.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.Saga.Server.Extensions;
using JZVerse.MicroHuaxia.ServiceDiscovery.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 统一可观测性
builder.Services.AddObservability(builder.Configuration);

// 添加 Saga 服务端
builder.Services.AddSagaServer(builder.Configuration);

// 服务发现 - 注册到 SD（软依赖，SD 不可用时跳过注册继续运行）
builder.Services.AddServiceDiscoveryClient(builder.Configuration);

// 配置中心 - 运行时配置管理
builder.Services.AddConfigCenter(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 健康检查
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseObservability();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
