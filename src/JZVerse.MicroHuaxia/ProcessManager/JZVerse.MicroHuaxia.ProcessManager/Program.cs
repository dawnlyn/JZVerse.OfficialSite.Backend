using JZVerse.MicroHuaxia.ProcessManager.Configuration;
using JZVerse.MicroHuaxia.ProcessManager.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ProcessManagerOptions>(
    builder.Configuration.GetSection(ProcessManagerOptions.SectionName));

builder.Services.AddSingleton<ProcessManagerService>();
builder.Services.AddControllers();

var app = builder.Build();

// 应用关闭时优雅停止所有托管进程
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var manager = app.Services.GetRequiredService<ProcessManagerService>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application stopping, shutting down all managed processes...");
    manager.StopAllAsync(CancellationToken.None).GetAwaiter().GetResult();
    logger.LogInformation("All managed processes stopped");
});

app.MapControllers();

app.Run();
