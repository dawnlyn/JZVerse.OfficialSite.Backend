namespace JZVerse.MicroHuaxia.Gateway.Tracing.Storage;

/// <summary>
/// 追踪存储接口
/// </summary>
public interface ITraceStore
{
    /// <summary>
    /// 添加 Span
    /// </summary>
    void AddSpan(TraceSpan span);

    /// <summary>
    /// 批量添加 Span
    /// </summary>
    void AddSpans(IEnumerable<TraceSpan> spans);

    /// <summary>
    /// 获取完整追踪链
    /// </summary>
    /// <param name="traceId">追踪 ID</param>
    /// <returns>该追踪的所有 Span</returns>
    IReadOnlyList<TraceSpan> GetTrace(string traceId);

    /// <summary>
    /// 获取单个 Span
    /// </summary>
    /// <param name="traceId">追踪 ID</param>
    /// <param name="spanId">Span ID</param>
    /// <returns>Span 详情</returns>
    TraceSpan? GetSpan(string traceId, string spanId);

    /// <summary>
    /// 查询追踪
    /// </summary>
    /// <param name="query">查询条件</param>
    /// <returns>查询结果</returns>
    TraceQueryResult Query(TraceQuery query);

    /// <summary>
    /// 获取统计信息
    /// </summary>
    TraceStatistics GetStatistics();

    /// <summary>
    /// 获取最近的追踪
    /// </summary>
    /// <param name="count">数量（默认 20）</param>
    IReadOnlyList<TraceSummary> GetLatestTraces(int count = 20);

    /// <summary>
    /// 清理过期数据
    /// </summary>
    void Cleanup();

    /// <summary>
    /// 清空所有数据
    /// </summary>
    void Clear();
}
