using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Security.Authorization;

/// <summary>
/// 策略引擎实现
/// </summary>
public sealed class PolicyEngine : IAuthorizationEngine
{
    private readonly IPolicyStore _policyStore;
    private readonly ILogger<PolicyEngine> _logger;

    // 策略缓存
    private readonly ConcurrentDictionary<string, CachedPolicy> _policyCache = new();
    private readonly ConcurrentDictionary<string, AccessDecision> _decisionCache = new();

    public PolicyEngine(
        IPolicyStore policyStore,
        ILogger<PolicyEngine> logger)
    {
        _policyStore = policyStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AccessDecision> EvaluateAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 检查决策缓存
            string cacheKey = GenerateDecisionCacheKey(request);
            if (_decisionCache.TryGetValue(cacheKey, out var cachedDecision))
            {
                _logger.LogDebug(
                    "授权决策缓存命中: {Subject} -> {Resource}:{Action}",
                    request.Subject.IdentityId, request.Resource, request.Action);
                return cachedDecision;
            }

            // 获取匹配的策略
            var policies = await GetMatchingPoliciesAsync(request, cancellationToken);

            // 按优先级排序（数值越大优先级越高）
            policies = policies.OrderByDescending(p => p.Priority).ToList();

            // 评估策略
            AccessDecision decision = AccessDecision.DefaultDeny;

            foreach (var policy in policies)
            {
                var policyResult = EvaluatePolicy(policy, request);

                if (policyResult.Matched && policy.Effect == PolicyEffect.Allow)
                {
                    decision = AccessDecision.Allow(
                        $"策略允许: {policy.Name}",
                        policies.Where(p => p.Effect == PolicyEffect.Allow).Select(p => p.Name).ToArray());
                    break;
                }

                if (policyResult.Matched && policy.Effect == PolicyEffect.Deny)
                {
                    decision = AccessDecision.Deny($"策略拒绝: {policy.Name}");
                    break;
                }
            }

            stopwatch.Stop();
            decision = decision with { ElapsedMilliseconds = stopwatch.ElapsedMilliseconds };

            // 缓存决策结果（短期缓存）
            CacheDecision(cacheKey, decision);

            _logger.LogInformation(
                "授权决策: {Subject} -> {Resource}:{Action} = {Allowed} ({Reason})",
                request.Subject.IdentityId, request.Resource, request.Action,
                decision.Allowed, decision.Reason);

            return decision;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "授权评估失败: {Subject} -> {Resource}:{Action}",
                request.Subject.IdentityId, request.Resource, request.Action);
            return AccessDecision.Deny("授权评估过程中发生错误");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccessDecision>> EvaluateBatchAsync(
        IReadOnlyList<AccessRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var tasks = requests.Select(r => EvaluateAsync(r, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results;
    }

    /// <inheritdoc />
    public async Task<bool> IsAllowedAsync(
        SecurityIdentity subject,
        string resource,
        string action,
        CancellationToken cancellationToken = default)
    {
        var request = AccessRequest.Create(subject, resource, action);
        var decision = await EvaluateAsync(request, cancellationToken);
        return decision.Allowed;
    }

    /// <inheritdoc />
    public async Task<bool> IsAllowedAsync(
        SecurityIdentity subject,
        string resource,
        string action,
        IReadOnlyDictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var request = new AccessRequest
        {
            Subject = subject,
            Resource = resource,
            Action = action,
            Context = context
        };
        var decision = await EvaluateAsync(request, cancellationToken);
        return decision.Allowed;
    }

    /// <inheritdoc />
    public Task WarmupCacheAsync(string? @namespace = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在预热授权策略缓存...");
        // 实际实现中应该异步加载策略到缓存
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearCacheAsync(string? @namespace = null)
    {
        if (string.IsNullOrEmpty(@namespace))
        {
            _policyCache.Clear();
            _decisionCache.Clear();
            _logger.LogInformation("已清除所有授权缓存");
        }
        else
        {
            // 清除指定命名空间的缓存
            var keysToRemove = _policyCache.Keys.Where(k => k.StartsWith(@namespace + ":")).ToList();
            foreach (var key in keysToRemove)
            {
                _policyCache.TryRemove(key, out _);
            }
            _logger.LogInformation("已清除命名空间 {Namespace} 的授权缓存", @namespace);
        }

        return Task.CompletedTask;
    }

    #region 私有方法

    /// <summary>
    /// 获取匹配的策略
    /// </summary>
    private async Task<IReadOnlyList<SecurityPolicy>> GetMatchingPoliciesAsync(
        AccessRequest request,
        CancellationToken cancellationToken)
    {
        // 获取命名空间下的所有策略
        var policies = await _policyStore.GetPoliciesAsync(request.Namespace, cancellationToken);

        // 过滤出启用的策略
        return policies.Where(p => p.IsEnabled).ToList();
    }

    /// <summary>
    /// 评估单个策略
    /// </summary>
    private PolicyEvaluationResult EvaluatePolicy(SecurityPolicy policy, AccessRequest request)
    {
        // 检查主体匹配
        if (!MatchesSubject(policy.Subjects, request.Subject))
        {
            return new PolicyEvaluationResult { Matched = false };
        }

        // 检查资源匹配
        if (!MatchesResource(policy.Resources, request.Resource))
        {
            return new PolicyEvaluationResult { Matched = false };
        }

        // 检查操作匹配
        if (!MatchesAction(policy.Actions, request.Action))
        {
            return new PolicyEvaluationResult { Matched = false };
        }

        // 检查条件（ABAC）
        if (!string.IsNullOrEmpty(policy.Condition))
        {
            if (!EvaluateCondition(policy.Condition, request))
            {
                return new PolicyEvaluationResult { Matched = false };
            }
        }

        return new PolicyEvaluationResult
        {
            Matched = true,
            Effect = policy.Effect
        };
    }

    /// <summary>
    /// 检查主体匹配
    /// </summary>
    private bool MatchesSubject(IReadOnlyList<string> subjects, SecurityIdentity identity)
    {
        if (subjects.Count == 0 || subjects.Contains("*"))
            return true;

        // 检查 ID 匹配
        if (subjects.Contains(identity.IdentityId))
            return true;

        // 检查角色匹配
        foreach (var role in identity.Roles)
        {
            if (subjects.Contains($"role:{role}"))
                return true;
        }

        // 检查类型匹配
        if (subjects.Contains($"type:{identity.Type}"))
            return true;

        // 检查 SPIFFE ID 匹配
        var spiffeId = identity.ToSpiffeId();
        foreach (var subject in subjects)
        {
            if (MatchesPattern(spiffeId, subject))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 检查资源匹配
    /// </summary>
    private bool MatchesResource(IReadOnlyList<string> resources, string resource)
    {
        if (resources.Count == 0 || resources.Contains("*"))
            return true;

        foreach (var pattern in resources)
        {
            if (MatchesPattern(resource, pattern))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 检查操作匹配
    /// </summary>
    private bool MatchesAction(IReadOnlyList<string> actions, string action)
    {
        if (actions.Count == 0 || actions.Contains("*"))
            return true;

        // 支持通配符匹配，如 "read:*" 匹配 "read:article"
        foreach (var pattern in actions)
        {
            if (MatchesPattern(action, pattern))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 模式匹配（支持 * 通配符）
    /// </summary>
    private bool MatchesPattern(string value, string pattern)
    {
        if (pattern == "*")
            return true;

        if (!pattern.Contains('*'))
            return value.Equals(pattern, StringComparison.OrdinalIgnoreCase);

        // 将通配符模式转换为正则表达式
        string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// 评估条件表达式（简化实现）
    /// </summary>
    private bool EvaluateCondition(string condition, AccessRequest request)
    {
        // 实际生产环境应使用表达式引擎（如动态 LINQ 或自建 DSL）
        // 这里提供简化实现
        try
        {
            // 支持的条件格式:
            // - time:between(09:00,18:00)
            // - source_ip:in(10.0.0.0/8)
            // - mfa:verified

            if (condition.Contains("time:between"))
            {
                // 时间范围检查
                var now = DateTimeOffset.UtcNow;
                // 简化：假设条件成立
                return true;
            }

            if (condition.Contains("source_ip"))
            {
                // IP 范围检查
                if (request.Context.TryGetValue("source_ip", out var ip))
                {
                    // 简化：假设条件成立
                    return true;
                }
                return false;
            }

            if (condition == "mfa:verified")
            {
                if (request.Context.TryGetValue("mfa_verified", out var mfa))
                {
                    return mfa is true;
                }
                return false;
            }

            // 默认允许未知条件
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 生成决策缓存键
    /// </summary>
    private string GenerateDecisionCacheKey(AccessRequest request)
    {
        // 基于主体、资源、操作生成缓存键
        return $"{request.Subject.IdentityId}:{request.Resource}:{request.Action}:{request.Namespace}";
    }

    /// <summary>
    /// 缓存决策结果
    /// </summary>
    private void CacheDecision(string key, AccessDecision decision)
    {
        // 短期缓存（5 分钟）
        _decisionCache[key] = decision;
        
        // 设置过期（简化实现）
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            _decisionCache.TryRemove(key, out _);
        });
    }

    #endregion

    #region 内部类

    /// <summary>
    /// 缓存的策略
    /// </summary>
    private class CachedPolicy
    {
        public SecurityPolicy Policy { get; set; } = null!;
        public DateTimeOffset CachedAt { get; set; }
    }

    /// <summary>
    /// 策略评估结果
    /// </summary>
    private class PolicyEvaluationResult
    {
        public bool Matched { get; set; }
        public PolicyEffect Effect { get; set; }
    }

    #endregion
}
