using JZVerse.MicroHuaxia.Dashboard.Web.Components;
using Microsoft.AspNetCore.Builder;

namespace JZVerse.MicroHuaxia.Dashboard.AspNetCore.Extensions;

/// <summary>
/// Dashboard 应用配置扩展方法
/// </summary>
public static class DashboardApplicationExtensions
{
    /// <summary>
    /// 使用 Dashboard 中间件和路由
    /// </summary>
    /// <param name="app">应用构建器</param>
    /// <returns>应用构建器</returns>
    public static IApplicationBuilder UseDashboard(this IApplicationBuilder app)
    {
        // 静态文件支持
        app.UseStaticFiles();

        // 防伪造令牌
        app.UseAntiforgery();

        return app;
    }

    /// <summary>
    /// 映射 Dashboard Blazor 组件
    /// </summary>
    /// <param name="app">Web 应用</param>
    /// <returns>Web 应用</returns>
    public static WebApplication MapDashboard(this WebApplication app)
    {
        // 映射 Razor 组件
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        return app;
    }
}
