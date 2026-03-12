using JZVerse.MicroHuaxia.Observability.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Extensions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 添加统一可观测性 (控制台/文件/OpenTelemetry)
builder.Services.AddObservability(builder.Configuration);

// 添加 SD 专用遥测 (Dashboard 内存日志/指标/追踪)
builder.Services.AddServiceDiscoveryTelemetry(builder.Configuration);

// 添加服务发现服务端
builder.Services.AddServiceDiscoveryServer(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
var app = builder.Build();

app.UseObservability();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.MapControllers();
app.Run();
