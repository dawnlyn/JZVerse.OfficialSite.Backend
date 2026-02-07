using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Extensions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Extensions;

var builder = WebApplication.CreateBuilder(args);
// 添加可观测性 (Telemetry)
builder.Services.AddServiceDiscoveryTelemetry(builder.Configuration);
// 添加服务发现服务端
builder.Services.AddServiceDiscoveryServer(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.MapControllers();
app.Run();
public partial class Program { }
