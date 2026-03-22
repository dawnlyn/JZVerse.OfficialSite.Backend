using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Encryption;
using JZVerse.Business.Abstractions.Validation;
using JZVerse.Business.Infrastructure.Authentication;
using JZVerse.Business.Infrastructure.Encryption;
using JZVerse.Business.Infrastructure.Filters;
using JZVerse.Business.Infrastructure.Middleware;
using JZVerse.Business.Infrastructure.Validation;
using JZVerse.MicroHuaxia.Security;
using JZVerse.MicroHuaxia.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.Business.Infrastructure.Extensions;

/// <summary>
/// 业务基础设施扩展方法
/// </summary>
public static class BusinessInfrastructureExtensions
{
    /// <summary>
    /// 添加业务基础设施服务
    /// </summary>
    public static IServiceCollection AddBusinessInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 配置选项
        services.Configure<Sm2Options>(configuration.GetSection(Sm2Options.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // 注册 Security 核心服务
        services.AddSingleton<ISm2Provider, Sm2Provider>();
        services.AddSingleton<ISm3Provider, Sm3Provider>();
        services.AddSingleton<ISm4Provider, Sm4Provider>();

        // 加密服务（适配器模式，使用 Security 模块实现）
        services.AddSingleton<ISm2Encryptor, Sm2Encryptor>();

        // 认证服务
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IUserContext, UserContext>();

        // 校验服务
        services.AddSingleton<IModelValidator, ModelValidator>();

        // HTTP 上下文
        services.AddHttpContextAccessor();

        // TODO: 注册 Security Client（待 Security.Client 扩展方法完善后启用）
        // var securityServerAddress = configuration["Security:ServerAddress"];
        // if (!string.IsNullOrEmpty(securityServerAddress))
        // {
        //     services.AddSecurityClient(...);
        // }

        return services;
    }

    /// <summary>
    /// 添加业务 MVC 过滤器
    /// </summary>
    public static IMvcBuilder AddBusinessFilters(this IMvcBuilder builder)
    {
        builder.AddMvcOptions(options =>
        {
            options.Filters.Add<ValidationFilter>();
            options.Filters.Add<ResponseWrapperFilter>();
        });

        return builder;
    }

    /// <summary>
    /// 使用业务异常处理中间件
    /// </summary>
    public static IApplicationBuilder UseBusinessExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
