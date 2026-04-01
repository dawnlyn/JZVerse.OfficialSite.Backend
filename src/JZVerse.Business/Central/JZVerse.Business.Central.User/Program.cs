using JZVerse.Business.Central.User.Services;
using JZVerse.Business.Infrastructure.Extensions;
using JZVerse.MicroHuaxia.ConfigCenter.AspNetCore;
using JZVerse.MicroHuaxia.DataAccess.PostgreSql;
using JZVerse.MicroHuaxia.MessageQueue.Client;
using JZVerse.MicroHuaxia.Observability.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.Saga.AspNetCore;
using JZVerse.MicroHuaxia.ServiceCommunication.AspNetCore;
using JZVerse.MicroHuaxia.ServiceDiscovery.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// MicroHuaxia 微服务框架集成
// ============================================================

// 1. 统一可观测性
builder.Services.AddObservability(builder.Configuration);

// 2. 服务注册发现
builder.Services.AddServiceDiscoveryClient(builder.Configuration);

// 3. 配置中心
builder.Services.AddConfigCenter(builder.Configuration);

// 4. 服务通信
builder.Services
    .AddServiceCommunication()
    .AddHttp()
    .AddGrpc()
    .AddResilience();

// 5. 消息队列客户端
builder.Services.AddMessageQueueClient(options =>
{
    var config = builder.Configuration.GetSection("MessageQueue:Client");
    options.Host = config["Host"] ?? "localhost";
    options.Port = config.GetValue("Port", 9527);
    options.ClientId = config["ClientId"] ?? "user-server";
    options.ConsumerGroup = config["ConsumerGroup"] ?? "user-group";
    options.AutoReconnect = config.GetValue("AutoReconnect", true);
    options.ReconnectInterval = config.GetValue("ReconnectIntervalMs", 5000);
});

// 6. 分布式事务 Saga
builder.Services.AddSaga(options =>
{
    var config = builder.Configuration.GetSection("Saga");
    options.Storage = Enum.Parse<SagaStorageType>(config["Storage"] ?? "Memory");
    options.CommunicationMode = Enum.Parse<SagaCommunicationMode>(config["CommunicationMode"] ?? "Http");
    options.TimeoutOptions.DefaultTimeout = TimeSpan.FromSeconds(config.GetValue("TimeoutSeconds", 300));
});

// ============================================================
// User 服务配置
// ============================================================

// 业务基础设施
builder.Services.AddBusinessInfrastructure(builder.Configuration);

// 数据访问
var connectionString = builder.Configuration.GetConnectionString("User");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddPostgreSqlDataAccess(connectionString);
}

// User 业务服务
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<VisitorStatsService>();

// 控制器和 Swagger
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
