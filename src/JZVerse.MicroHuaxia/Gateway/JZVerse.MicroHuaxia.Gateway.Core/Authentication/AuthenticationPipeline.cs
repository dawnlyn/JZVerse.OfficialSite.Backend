using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Core.Authentication;

/// <summary>
/// 认证管道实现
/// </summary>
public sealed class AuthenticationPipeline : IAuthenticationPipeline
{
    private readonly ILogger<AuthenticationPipeline> _logger;
    private readonly IAuthenticationStrategyRepository _strategyRepository;
    private readonly ConcurrentDictionary<string, IAuthenticationHandler> _handlers = new();

    public AuthenticationPipeline(
        ILogger<AuthenticationPipeline> logger,
        IAuthenticationStrategyRepository strategyRepository)
    {
        _logger = logger;
        _strategyRepository = strategyRepository;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        HttpContext context,
        RouteAuthentication config,
        CancellationToken cancellationToken = default)
    {
        if (!config.Required)
        {
            return AuthenticationResult.Skip();
        }

        if (config.Strategies.Count == 0)
        {
            _logger.LogWarning("路由要求认证但未配置认证策略");
            return AuthenticationResult.Failure("未配置认证策略");
        }

        var results = new List<(string StrategyName, AuthenticationResult Result)>();

        foreach (var strategyName in config.Strategies)
        {
            var strategy = await _strategyRepository.GetByNameAsync(strategyName, cancellationToken);
            if (strategy is null)
            {
                _logger.LogWarning("认证策略不存在: {StrategyName}", strategyName);
                continue;
            }

            if (!_handlers.TryGetValue(GetHandlerKey(strategy.Type), out var handler))
            {
                _logger.LogWarning("未找到认证处理器: {Type}", strategy.Type);
                continue;
            }

            var result = await handler.AuthenticateAsync(context, strategy.Configuration, cancellationToken);
            results.Add((strategyName, result));

            if (config.CombineMode == AuthenticationCombineMode.Or && result.IsAuthenticated)
            {
                // OR 模式：任一成功即可
                context.User = result.Principal!;
                return result;
            }

            if (config.CombineMode == AuthenticationCombineMode.And && !result.IsAuthenticated)
            {
                // AND 模式：任一失败则失败
                if (strategy.FailureMode == AuthenticationFailureMode.Break)
                {
                    return result;
                }
            }
        }

        // 处理最终结果
        if (config.CombineMode == AuthenticationCombineMode.Or)
        {
            // OR 模式：所有策略都失败
            var failureReasons = results
                .Where(r => !r.Result.IsAuthenticated)
                .Select(r => $"{r.StrategyName}: {r.Result.FailureReason}")
                .ToList();

            return AuthenticationResult.Failure(string.Join("; ", failureReasons));
        }
        else
        {
            // AND 模式：检查是否所有策略都成功
            var allSucceeded = results.All(r => r.Result.IsAuthenticated);
            if (allSucceeded && results.Count > 0)
            {
                var lastSuccess = results.Last(r => r.Result.IsAuthenticated);
                context.User = lastSuccess.Result.Principal!;
                return lastSuccess.Result;
            }

            var failureReasons = results
                .Where(r => !r.Result.IsAuthenticated)
                .Select(r => $"{r.StrategyName}: {r.Result.FailureReason}")
                .ToList();

            return AuthenticationResult.Failure(string.Join("; ", failureReasons));
        }
    }

    public void RegisterHandler(IAuthenticationHandler handler)
    {
        var key = handler.Name.ToLowerInvariant();
        _handlers[key] = handler;
        _logger.LogInformation("注册认证处理器: {Name}", handler.Name);
    }

    public IReadOnlyList<IAuthenticationHandler> GetHandlers()
    {
        return _handlers.Values.OrderBy(h => h.Priority).ToList();
    }

    private static string GetHandlerKey(AuthenticationStrategyType type)
    {
        return type switch
        {
            AuthenticationStrategyType.Jwt => "jwt",
            AuthenticationStrategyType.ApiKey => "api-key",
            AuthenticationStrategyType.Basic => "basic",
            AuthenticationStrategyType.Custom => "custom",
            _ => type.ToString().ToLowerInvariant()
        };
    }
}
