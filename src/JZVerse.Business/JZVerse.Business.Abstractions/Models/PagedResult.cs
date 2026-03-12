namespace JZVerse.Business.Abstractions.Models;

/// <summary>
/// 分页响应
/// </summary>
/// <typeparam name="T">数据项类型</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>
    /// 数据列表
    /// </summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>
    /// 总记录数
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// 当前页码（从 1 开始）
    /// </summary>
    public int PageIndex { get; init; }

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// 总页数
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// 是否有上一页
    /// </summary>
    public bool HasPreviousPage => PageIndex > 1;

    /// <summary>
    /// 是否有下一页
    /// </summary>
    public bool HasNextPage => PageIndex < TotalPages;

    /// <summary>
    /// 创建分页结果
    /// </summary>
    public static PagedResult<T> Create(IReadOnlyList<T> items, long totalCount, int pageIndex, int pageSize) =>
        new()
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

    /// <summary>
    /// 创建空的分页结果
    /// </summary>
    public static PagedResult<T> Empty(int pageIndex = 1, int pageSize = 10) =>
        new()
        {
            Items = [],
            TotalCount = 0,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
}
