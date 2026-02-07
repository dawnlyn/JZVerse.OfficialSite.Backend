using JZVerse.MicroHuaxia.ConfigCenter.Server.Extensions;
using JZVerse.MicroHuaxia.Gateway.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.Gateway.Http;
using JZVerse.MicroHuaxia.Gateway.WebSocket;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ==================== 聚合服务配置 ====================

// 1. 添加服务发现服务端
builder.Services.AddServiceDiscoveryServer(builder.Configuration);

// 2. 添加配置中心服务端
builder.Services.AddConfigCenterServer(builder.Configuration);

// 3. 添加网关服务
builder.Services.AddGateway(builder.Configuration);

// 4. 添加协议支持
builder.Services.AddGatewayHttp();
builder.Services.AddGatewayWebSocket();
// builder.Services.AddGatewayGrpc(); // gRPC 代理需要更完整实现

// 5. 添加控制器和 OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ==================== 中间件配置 ====================

// 开发环境启用 OpenAPI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// WebSocket 支持
app.UseGatewayWebSocket();

// 网关中间件（包含认证、限流、缓存、路由转发）
app.UseGateway();

// 管理 API（ServiceDiscovery + ConfigCenter + Gateway）
app.MapControllers();

app.Run();
