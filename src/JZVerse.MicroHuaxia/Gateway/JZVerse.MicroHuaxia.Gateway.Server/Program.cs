using JZVerse.MicroHuaxia.ConfigCenter.AspNetCore;
using JZVerse.MicroHuaxia.ConfigCenter.Server.Extensions;
using JZVerse.MicroHuaxia.Gateway.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.Gateway.Http;
using JZVerse.MicroHuaxia.Gateway.WebSocket;
using JZVerse.MicroHuaxia.Observability.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.ServiceDiscovery.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ==================== 聚合/独立模式配置 ====================

var gatewayConfig = builder.Configuration.GetSection("Gateway");
var useLocalSD = gatewayConfig.GetValue("ServiceDiscovery:UseLocal", true);
var useLocalCC = gatewayConfig.GetValue("ConfigCenter:UseLocal", true);

// 1. 统一可观测性
builder.Services.AddObservability(builder.Configuration);

// 2. 服务发现：聚合模式内嵌 SD Server，独立模式作为客户端 + IServiceDiscovery 适配
if (useLocalSD)
{
    builder.Services.AddServiceDiscoveryServer(builder.Configuration);
}
else
{
    builder.Services.AddServiceDiscoveryClientWithDiscovery(builder.Configuration);
}

// 3. 配置中心：聚合模式内嵌 CC Server，独立模式作为客户端
if (useLocalCC)
{
    builder.Services.AddConfigCenterServer(builder.Configuration);
}
else
{
    builder.Services.AddConfigCenter(builder.Configuration);
}

// 4. 添加网关服务
builder.Services.AddGateway(builder.Configuration);

// 5. 添加协议支持
builder.Services.AddGatewayHttp();
builder.Services.AddGatewayWebSocket();
// builder.Services.AddGatewayGrpc(); // gRPC 代理需要更完整实现

// 6. 添加控制器和 OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ==================== 中间件配置 ====================

app.UseObservability();

// 开发环境启用 OpenAPI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// WebSocket 支持
app.UseGatewayWebSocket();

// 网关中间件（包含认证、限流、缓存、路由转发）
app.UseGateway();

// 管理 API
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
