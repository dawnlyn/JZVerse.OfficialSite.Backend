using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.TrafficControl;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Core.TrafficControl;

/// <summary>
/// 流量染色服务实现
/// </summary>
public sealed class TrafficColoringService : ITrafficColoringService
{
    private readonly ILogger<TrafficColoringService> _logger;

    public TrafficColoringService(ILogger<TrafficColoringService> logger)
    {
        _logger = logger;
    }

    public Task<TrafficColoringResult> ApplyColoringAsync(
        HttpContext context,
        RouteTrafficColoring coloringConfig,
        CancellationToken cancellationToken = default)
    {
        if (!coloringConfig.Enabled || coloringConfig.Rules.Count == 0)
        {
            return Task.FromResult(TrafficColoringResult.Empty());
        }

        var tags = new HashSet<string>();
        string? matchedRule = null;

        // 按优先级排序规则
        var sortedRules = coloringConfig.Rules.OrderBy(r => r.Priority);

        foreach (var rule in sortedRules)
        {
            if (EvaluateRule(context, rule))
            {
                tags.Add(rule.Tag);
                matchedRule ??= rule.Name;

                _logger.LogDebug(
                    "流量染色规则匹配: {RuleName}, 标签: {Tag}",
                    rule.Name, rule.Tag);
            }
        }

        if (tags.Count > 0)
        {
            _logger.LogInformation(
                "请求已染色, 标签: [{Tags}], 匹配规则: {MatchedRule}",
                string.Join(", ", tags), matchedRule);
        }

        return Task.FromResult(TrafficColoringResult.WithTags(tags, matchedRule));
    }

    private bool EvaluateRule(HttpContext context, TrafficColoringRule rule)
    {
        return rule.Type switch
        {
            TrafficColoringType.Percentage => EvaluatePercentageRule(context, rule),
            TrafficColoringType.HeaderMatch => EvaluateHeaderMatchRule(context, rule),
            TrafficColoringType.UserWhitelist => EvaluateUserWhitelistRule(context, rule),
            TrafficColoringType.QueryMatch => EvaluateQueryMatchRule(context, rule),
            TrafficColoringType.CookieMatch => EvaluateCookieMatchRule(context, rule),
            _ => false
        };
    }

    private bool EvaluatePercentageRule(HttpContext context, TrafficColoringRule rule)
    {
        var hashSource = GetHashSource(context, rule.HashSource);
        if (string.IsNullOrEmpty(hashSource))
        {
            return false;
        }

        // 使用稳定的哈希算法确保同一用户/IP 始终得到相同结果
        var hashValue = GetStableHash(hashSource);
        var bucket = hashValue % 100;

        return bucket < rule.Percentage;
    }

    private bool EvaluateHeaderMatchRule(HttpContext context, TrafficColoringRule rule)
    {
        if (string.IsNullOrEmpty(rule.HeaderName))
        {
            return false;
        }

        if (!context.Request.Headers.TryGetValue(rule.HeaderName, out var headerValue))
        {
            return false;
        }

        var value = headerValue.ToString();

        // 精确匹配
        if (!string.IsNullOrEmpty(rule.HeaderValue))
        {
            return string.Equals(value, rule.HeaderValue, StringComparison.OrdinalIgnoreCase);
        }

        // 正则匹配
        if (!string.IsNullOrEmpty(rule.HeaderPattern))
        {
            try
            {
                return Regex.IsMatch(value, rule.HeaderPattern, RegexOptions.IgnoreCase);
            }
            catch (RegexParseException ex)
            {
                _logger.LogWarning(ex, "无效的正则表达式: {Pattern}", rule.HeaderPattern);
                return false;
            }
        }

        // 只检查 Header 是否存在
        return true;
    }

    private bool EvaluateUserWhitelistRule(HttpContext context, TrafficColoringRule rule)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        return rule.UserIds.Contains(userId);
    }

    private bool EvaluateQueryMatchRule(HttpContext context, TrafficColoringRule rule)
    {
        if (string.IsNullOrEmpty(rule.HeaderName)) // 复用 HeaderName 作为 Query 参数名
        {
            return false;
        }

        if (!context.Request.Query.TryGetValue(rule.HeaderName, out var queryValue))
        {
            return false;
        }

        var value = queryValue.ToString();

        if (!string.IsNullOrEmpty(rule.HeaderValue))
        {
            return string.Equals(value, rule.HeaderValue, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private bool EvaluateCookieMatchRule(HttpContext context, TrafficColoringRule rule)
    {
        if (string.IsNullOrEmpty(rule.HeaderName)) // 复用 HeaderName 作为 Cookie 名
        {
            return false;
        }

        if (!context.Request.Cookies.TryGetValue(rule.HeaderName, out var cookieValue))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.HeaderValue))
        {
            return string.Equals(cookieValue, rule.HeaderValue, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private string? GetHashSource(HttpContext context, TrafficColoringHashSource source)
    {
        return source switch
        {
            TrafficColoringHashSource.ClientIp => GetClientIp(context),
            TrafficColoringHashSource.UserId => GetUserId(context),
            TrafficColoringHashSource.RequestId => context.TraceIdentifier,
            TrafficColoringHashSource.SessionId => context.Session?.Id,
            _ => GetClientIp(context)
        };
    }

    private static string? GetClientIp(HttpContext context)
    {
        // 优先从代理头获取
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var ip = forwardedFor.ToString().Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }
        }

        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            return realIp.ToString();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string? GetUserId(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("sub")
               ?? user.Identity.Name;
    }

    private static int GetStableHash(string input)
    {
        // 使用 MD5 的前 4 字节作为稳定的哈希值
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = MD5.HashData(bytes);
        return Math.Abs(BitConverter.ToInt32(hash, 0));
    }
}
