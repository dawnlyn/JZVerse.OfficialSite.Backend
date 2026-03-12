using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Encryption;
using JZVerse.Business.Abstractions.Validation;
using JZVerse.Business.Infrastructure.Authentication;
using JZVerse.Business.Infrastructure.Encryption;
using JZVerse.Business.Infrastructure.Filters;
using JZVerse.Business.Infrastructure.Middleware;
using JZVerse.Business.Infrastructure.Validation;
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

        // 加密服务
        services.AddSingleton<ISm2Encryptor, Sm2Encryptor>();

        // 认证服务
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IUserContext, UserContext>();

        // 校验服务
        services.AddSingleton<IModelValidator, ModelValidator>();

        // HTTP 上下文
        services.AddHttpContextAccessor();

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
