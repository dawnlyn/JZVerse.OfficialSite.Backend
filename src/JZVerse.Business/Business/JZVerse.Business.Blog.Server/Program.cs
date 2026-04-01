using JZVerse.Business.Blog.Services;
using JZVerse.Business.Infrastructure.Extensions;
using JZVerse.MicroHuaxia.DataAccess.PostgreSql;
using JZVerse.MicroHuaxia.ConfigCenter.AspNetCore;
using JZVerse.MicroHuaxia.MessageQueue.Client;
using JZVerse.MicroHuaxia.Observability.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.Saga.AspNetCore;
using JZVerse.MicroHuaxia.ServiceCommunication.AspNetCore;
using JZVerse.MicroHuaxia.ServiceDiscovery.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// MicroHuaxia 微服务框架集成
// ============================================================

// 1. 统一可观测性 - 控制台/文件日志 + OpenTelemetry (日志/追踪/指标)
builder.Services.AddObservability(builder.Configuration);

// 2. 服务注册发现 - 自动注册到 ServiceDiscovery、健康检查、心跳
builder.Services.AddServiceDiscoveryClient(builder.Configuration);

// 3. 配置中心 - 远程配置、热重载、本地缓存回退
builder.Services.AddConfigCenter(builder.Configuration);

// 4. 服务通信 - HTTP/gRPC + 弹性策略（重试、熔断）
builder.Services
    .AddServiceCommunication()
    .AddHttp()
    .AddGrpc()
    .AddResilience();

// 5. 消息队列客户端 - 异步事件处理
builder.Services.AddMessageQueueClient(options =>
{
    var config = builder.Configuration.GetSection("MessageQueue:Client");
    options.Host = config["Host"] ?? "localhost";
    options.Port = config.GetValue("Port", 9527);
    options.ClientId = config["ClientId"] ?? "blog-server";
    options.ConsumerGroup = config["ConsumerGroup"] ?? "blog-group";
    options.AutoReconnect = config.GetValue("AutoReconnect", true);
    options.ReconnectInterval = config.GetValue("ReconnectIntervalMs", 5000);
});

// 6. 分布式事务 Saga - 跨服务事务协调
builder.Services.AddSaga(options =>
{
    var config = builder.Configuration.GetSection("Saga");
    options.Storage = Enum.Parse<SagaStorageType>(config["Storage"] ?? "Memory");
    options.CommunicationMode = Enum.Parse<SagaCommunicationMode>(config["CommunicationMode"] ?? "Http");
    options.TimeoutOptions.DefaultTimeout = TimeSpan.FromSeconds(config.GetValue("TimeoutSeconds", 300));
});

// ============================================================
// Blog 服务配置
// ============================================================

// 7. 业务基础设施 - JWT、SM2 加密、校验器
builder.Services.AddBusinessInfrastructure(builder.Configuration);

// 8. 数据访问 - PostgreSQL + Dapper
var connectionString = builder.Configuration.GetConnectionString("Blog");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddPostgreSqlDataAccess(connectionString);
}

// 9. Blog 业务服务注册
builder.Services.AddScoped<ArticleService>();

// 10. 控制器和 Swagger
builder.Services.AddControllers().AddBusinessFilters();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ============================================================
// 中间件管道
// ============================================================

app.UseObservability();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseBusinessExceptionHandling();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
