using JZVerse.MicroHuaxia.ConfigCenter.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 添加配置中心服务
builder.Services.AddConfigCenterServer(builder.Configuration);

// 添加控制器
builder.Services.AddControllers();

// 添加 OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// 开发环境启用 OpenAPI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();
