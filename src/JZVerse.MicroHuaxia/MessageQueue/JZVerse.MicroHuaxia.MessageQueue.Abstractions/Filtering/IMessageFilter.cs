namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions.Filtering;

/// <summary>
/// 消息过滤器接口
/// </summary>
public interface IMessageFilter
{
    /// <summary>
    /// 过滤器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 是否匹配消息
    /// </summary>
    /// <param name="context">过滤上下文</param>
    bool Match(FilterContext context);
}

/// <summary>
/// 过滤上下文
/// </summary>
public sealed record FilterContext
{
    /// <summary>
    /// 消息标签
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// 消息头
    /// </summary>
    public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// 用户属性（用于 SQL92 过滤）
    /// </summary>
    public IDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Tag 过滤器
/// </summary>
public interface ITagFilter : IMessageFilter
{
    /// <summary>
    /// 标签表达式（支持 * 通配符和 || 分隔多个标签）
    /// </summary>
    string TagExpression { get; }
}

/// <summary>
/// SQL92 过滤器
/// </summary>
public interface ISql92Filter : IMessageFilter
{
    /// <summary>
    /// SQL92 表达式
    /// </summary>
    string SqlExpression { get; }
}

/// <summary>
/// 过滤器工厂接口
/// </summary>
public interface IMessageFilterFactory
{
    /// <summary>
    /// 创建 Tag 过滤器
    /// </summary>
    /// <param name="expression">标签表达式</param>
    IMessageFilter CreateTagFilter(string expression);

    /// <summary>
    /// 创建 SQL92 过滤器
    /// </summary>
    /// <param name="expression">SQL92 表达式</param>
    IMessageFilter CreateSql92Filter(string expression);
}
