using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Security.Authorization;

namespace JZVerse.MicroHuaxia.Security.Storage;

/// <summary>
/// 内存策略存储（用于开发和测试）
/// </summary>
public sealed class InMemoryPolicyStore : IPolicyStore
{
    private readonly ConcurrentDictionary<string, SecurityPolicy> _policies = new();
    private readonly List<Func<PolicyChangeEvent, Task>> _changeHandlers = new();

    public InMemoryPolicyStore()
    {
        // 初始化默认策略
        InitializeDefaultPolicies();
    }

    /// <inheritdoc />
    public Task<SecurityPolicy?> GetPolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        _policies.TryGetValue(policyId, out var policy);
        return Task.FromResult(policy);
    }

    /// <inheritdoc />
    public Task<SecurityPolicy?> GetPolicyByNameAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        var policy = _policies.Values.FirstOrDefault(p =>
            p.Name == name &&
            (string.IsNullOrEmpty(@namespace) || p.Namespace == @namespace));
        return Task.FromResult(policy);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SecurityPolicy>> GetPoliciesAsync(string? @namespace = null, CancellationToken cancellationToken = default)
    {
        var policies = _policies.Values
            .Where(p => string.IsNullOrEmpty(@namespace) || p.Namespace == @namespace)
            .ToList();
        return Task.FromResult<IReadOnlyList<SecurityPolicy>>(policies);
    }

    /// <inheritdoc />
    public Task<SecurityPolicy> CreatePolicyAsync(SecurityPolicy policy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(policy.PolicyId))
        {
            policy = policy with { PolicyId = Guid.NewGuid().ToString("N") };
        }

        _policies[policy.PolicyId] = policy;

        // 触发变更事件
        _ = NotifyChangeAsync(new PolicyChangeEvent
        {
            ChangeType = PolicyChangeType.Created,
            PolicyId = policy.PolicyId,
            PolicyName = policy.Name,
            Namespace = policy.Namespace,
            CurrentPolicy = policy
        });

        return Task.FromResult(policy);
    }

    /// <inheritdoc />
    public Task<SecurityPolicy> UpdatePolicyAsync(SecurityPolicy policy, CancellationToken cancellationToken = default)
    {
        if (!_policies.ContainsKey(policy.PolicyId))
        {
            throw new InvalidOperationException($"策略不存在: {policy.PolicyId}");
        }

        var previousPolicy = _policies[policy.PolicyId];
        _policies[policy.PolicyId] = policy;

        // 触发变更事件
        _ = NotifyChangeAsync(new PolicyChangeEvent
        {
            ChangeType = PolicyChangeType.Updated,
            PolicyId = policy.PolicyId,
            PolicyName = policy.Name,
            Namespace = policy.Namespace,
            PreviousPolicy = previousPolicy,
            CurrentPolicy = policy
        });

        return Task.FromResult(policy);
    }

    /// <inheritdoc />
    public Task<bool> DeletePolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        if (_policies.TryRemove(policyId, out var policy))
        {
            // 触发变更事件
            _ = NotifyChangeAsync(new PolicyChangeEvent
            {
                ChangeType = PolicyChangeType.Deleted,
                PolicyId = policyId,
                PolicyName = policy.Name,
                Namespace = policy.Namespace,
                PreviousPolicy = policy
            });
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async Task<bool> EnablePolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(policyId, cancellationToken);
        if (policy == null) return false;

        var updatedPolicy = policy with { IsEnabled = true };
        await UpdatePolicyAsync(updatedPolicy, cancellationToken);

        // 触发变更事件
        _ = NotifyChangeAsync(new PolicyChangeEvent
        {
            ChangeType = PolicyChangeType.Enabled,
            PolicyId = policyId,
            PolicyName = policy.Name,
            Namespace = policy.Namespace,
            PreviousPolicy = policy,
            CurrentPolicy = updatedPolicy
        });

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DisablePolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(policyId, cancellationToken);
        if (policy == null) return false;

        var updatedPolicy = policy with { IsEnabled = false };
        await UpdatePolicyAsync(updatedPolicy, cancellationToken);

        // 触发变更事件
        _ = NotifyChangeAsync(new PolicyChangeEvent
        {
            ChangeType = PolicyChangeType.Disabled,
            PolicyId = policyId,
            PolicyName = policy.Name,
            Namespace = policy.Namespace,
            PreviousPolicy = policy,
            CurrentPolicy = updatedPolicy
        });

        return true;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PolicyChangeEvent> WatchPolicyChangesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 简化实现：使用 Channel 或队列实现实时推送
        // 这里返回一个空枚举，实际生产环境应实现 WebSocket 推送
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// 初始化默认策略
    /// </summary>
    private void InitializeDefaultPolicies()
    {
        // 默认允许管理员访问所有资源
        _policies["default-admin"] = new SecurityPolicy
        {
            PolicyId = "default-admin",
            Name = "默认管理员权限",
            Description = "允许管理员角色访问所有资源",
            Effect = PolicyEffect.Allow,
            Priority = 100,
            Subjects = new List<string> { "role:admin" },
            Resources = new List<string> { "*" },
            Actions = new List<string> { "*" },
            IsEnabled = true
        };

        // 默认拒绝所有访问
        _policies["default-deny"] = new SecurityPolicy
        {
            PolicyId = "default-deny",
            Name = "默认拒绝",
            Description = "默认拒绝所有访问请求",
            Effect = PolicyEffect.Deny,
            Priority = int.MinValue,
            Subjects = new List<string> { "*" },
            Resources = new List<string> { "*" },
            Actions = new List<string> { "*" },
            IsEnabled = true
        };
    }

    /// <summary>
    /// 通知策略变更
    /// </summary>
    private async Task NotifyChangeAsync(PolicyChangeEvent changeEvent)
    {
        foreach (var handler in _changeHandlers)
        {
            try
            {
                await handler(changeEvent);
            }
            catch
            {
                // 忽略处理器的异常
            }
        }
    }
}
