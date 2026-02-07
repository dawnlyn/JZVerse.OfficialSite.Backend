using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;

namespace JZVerse.MicroHuaxia.Gateway.Core.Repositories;

/// <summary>
/// 内存认证策略存储实现
/// </summary>
public sealed class InMemoryAuthenticationStrategyRepository : IAuthenticationStrategyRepository
{
    private readonly ConcurrentDictionary<string, AuthenticationStrategy> _strategies = new();

    public Task<IReadOnlyList<AuthenticationStrategy>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AuthenticationStrategy>>(_strategies.Values.ToList());
    }

    public Task<AuthenticationStrategy?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_strategies.TryGetValue(name, out var strategy) ? strategy : null);
    }

    public Task AddAsync(AuthenticationStrategy strategy, CancellationToken cancellationToken = default)
    {
        if (!_strategies.TryAdd(strategy.Name, strategy))
        {
            throw new InvalidOperationException($"认证策略 {strategy.Name} 已存在");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(AuthenticationStrategy strategy, CancellationToken cancellationToken = default)
    {
        _strategies[strategy.Name] = strategy;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_strategies.TryRemove(name, out _));
    }
}
