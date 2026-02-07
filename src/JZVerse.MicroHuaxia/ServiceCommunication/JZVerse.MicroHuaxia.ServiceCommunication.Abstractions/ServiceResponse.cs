using System.Net;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Abstractions;

/// <summary>
/// 服务响应
/// </summary>
public sealed record ServiceResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// HTTP 状态码（仅 HTTP 协议）
    /// </summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>
    /// 响应头
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = [];

    /// <summary>
    /// 响应体（原始）
    /// </summary>
    public byte[]? RawBody { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 服务端地址
    /// </summary>
    public string? ServerAddress { get; init; }

    /// <summary>
    /// 请求耗时
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; init; }
}

/// <summary>
/// 泛型服务响应
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public sealed record ServiceResponse<T>
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// HTTP 状态码（仅 HTTP 协议）
    /// </summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>
    /// 响应数据
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// 响应头
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = [];

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 服务端地址
    /// </summary>
    public string? ServerAddress { get; init; }

    /// <summary>
    /// 请求耗时
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; init; }
}
