using JZVerse.Business.Academy.Controllers;
using JZVerse.Business.Academy.Services;
using JZVerse.Business.Blog.Controllers;
using JZVerse.Business.Blog.Services;
using JZVerse.Business.Central.File.Controllers;
using JZVerse.Business.Central.File.Services;
using JZVerse.Business.Central.Security.Controllers;
using JZVerse.Business.Central.Security.Services;
using JZVerse.Business.Central.User.Controllers;
using JZVerse.Business.Central.User.Services;
using JZVerse.Business.Dashboard.Controllers;
using JZVerse.Business.Dashboard.Services;
using JZVerse.Business.Project.Controllers;
using JZVerse.Business.Project.Services;
using JZVerse.Business.Server.Configuration;

namespace JZVerse.Business.Server.Extensions;

/// <summary>
/// 业务模块扩展方法
/// </summary>
public static class BusinessModuleExtensions
{
    /// <summary>
    /// 添加所有业务模块
    /// </summary>
    public static IServiceCollection AddBusinessModules(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置管理选项
        services.Configure<ManagementOptions>(configuration.GetSection(ManagementOptions.SectionName));

        // 用户中心 模块
        services.AddUserCentralModule();

        // 安全中心 模块
        services.AddSecurityCentralModule();

        // 文件中心 模块
        services.AddFileCentralModule();

        // Blog 模块
        services.AddBlogModule();

        // Academy 模块
        services.AddAcademyModule();

        // Project 模块
        services.AddProjectModule();

        // Dashboard 模块
        services.AddDashboardModule();

        return services;
    }

    /// <summary>
    /// 添加 用户中心 模块
    /// </summary>
    public static IServiceCollection AddUserCentralModule(this IServiceCollection services)
    {
        // 注册服务
        services.AddScoped<AdminService>();
        services.AddScoped<VisitorStatsService>();

        // 注册控制器（通过 AddControllers 自动发现）
        services.AddControllers().AddApplicationPart(typeof(AdminController).Assembly);

        return services;
    }

    /// <summary>
    /// 添加 安全中心 模块
    /// </summary>
    public static IServiceCollection AddSecurityCentralModule(this IServiceCollection services)
    {
        // 注册服务
        services.AddScoped<AuthenticationService>();

        // 注册控制器（通过 AddControllers 自动发现）
        services.AddControllers().AddApplicationPart(typeof(AuthenticationController).Assembly);

        return services;
    }

    /// <summary>
    /// 添加 文件中心 模块
    /// </summary>
    public static IServiceCollection AddFileCentralModule(this IServiceCollection services)
    {
        // 注册服务
        services.AddScoped<FileService>();

        // 注册控制器（通过 AddControllers 自动发现）
        services.AddControllers().AddApplicationPart(typeof(FileController).Assembly);

        return services;
    }

    /// <summary>
    /// 添加 Blog 模块
    /// </summary>
    public static IServiceCollection AddBlogModule(this IServiceCollection services)
    {
        // 注册服务
        services.AddScoped<ArticleService>();

        // 注册控制器（通过 AddControllers 自动发现）
        services.AddControllers().AddApplicationPart(typeof(ArticleController).Assembly);

        return services;
    }

    /// <summary>
    /// 添加 Academy 模块
    /// </summary>
    public static IServiceCollection AddAcademyModule(this IServiceCollection services)
    {
        // 注册服务
        services.AddScoped<CourseService>();

        // 注册控制器
        services.AddControllers().AddApplicationPart(typeof(CourseController).Assembly);

        return services;
    }

    /// <summary>
    /// 添加 Project 模块
    /// </summary>
    public static IServiceCollection AddProjectModule(this IServiceCollection services)
    {
        // 注册服务
        services.AddScoped<ProjectService>();

        // 注册控制器
        services.AddControllers().AddApplicationPart(typeof(ProjectController).Assembly);

        return services;
    }

    /// <summary>
    /// 添加 Dashboard 模块
    /// </summary>
    public static IServiceCollection AddDashboardModule(this IServiceCollection services)
    {
        // 注册服务
        services.AddScoped<DashboardService>();
        services.AddScoped<ConfigService>();
        services.AddScoped<LogService>();
        services.AddScoped<MenuService>();
        services.AddScoped<NotificationService>();

        // 注册控制器
        services.AddControllers().AddApplicationPart(typeof(DashboardController).Assembly);

        return services;
    }
}
