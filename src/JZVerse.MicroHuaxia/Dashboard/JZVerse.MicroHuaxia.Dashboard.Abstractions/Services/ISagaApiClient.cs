using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;

/// <summary>
/// Saga API 客户端接口
/// </summary>
public interface ISagaApiClient
{
    /// <summary>获取 Saga 统计信息</summary>
    Task<SagaStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>查询 Saga 实例列表</summary>
    Task<List<SagaInstanceInfo>> QueryInstancesAsync(string? sagaId = null, string? status = null, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>获取 Saga 实例详情</summary>
    Task<SagaInstanceInfo?> GetInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>触发补偿</summary>
    Task CompensateAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>取消 Saga</summary>
    Task CancelAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>恢复执行</summary>
    Task ResumeAsync(string instanceId, CancellationToken cancellationToken = default);
}
