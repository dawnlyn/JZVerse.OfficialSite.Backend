using JZVerse.MicroHuaxia.ConfigCenter.AspNetCore;
using JZVerse.MicroHuaxia.Dashboard.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.Dashboard.Web.Components;
using JZVerse.MicroHuaxia.Observability.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.ServiceDiscovery.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 统一可观测性
builder.Services.AddObservability(builder.Configuration);

// 添加 Dashboard 服务
builder.Services.AddDashboard(builder.Configuration);

// 服务发现 - 注册到 SD（软依赖，SD 不可用时跳过注册继续运行）
builder.Services.AddServiceDiscoveryClient(builder.Configuration);

// 配置中心 - 运行时配置管理
builder.Services.AddConfigCenter(builder.Configuration);

// 添加 Razor 组件支持
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 健康检查
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseObservability();

// 配置 HTTP 请求管道
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.MapStaticAssets();
app.UseRouting();
app.UseAntiforgery();

// 映射 Razor 组件
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
