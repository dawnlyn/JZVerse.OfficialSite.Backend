namespace JZVerse.Business.Abstractions.Models;

/// <summary>
/// 统一 API 响应格式
/// </summary>
/// <typeparam name="T">响应数据类型</typeparam>
public sealed record ApiResponse<T>
{
    /// <summary>
    /// 业务状态码，正常统一返回 200
    /// </summary>
    public int Code { get; init; }

    /// <summary>
    /// 响应数据
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public string Message { get; init; } = "success";

    /// <summary>
    /// 时间戳（Unix 秒）
    /// </summary>
    public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    /// API 版本
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// 链路追踪 ID
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 请求 ID
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static ApiResponse<T> Success(T? data, string message = "success") =>
        new()
        {
            Code = 200,
            Data = data,
            Message = message
        };

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public static ApiResponse<T> Failure(int code, string message) =>
        new()
        {
            Code = code,
            Data = default,
            Message = message
        };
}

/// <summary>
/// 无数据的 API 响应
/// </summary>
public sealed record ApiResponse
{
    /// <summary>
    /// 业务状态码
    /// </summary>
    public int Code { get; init; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public string Message { get; init; } = "success";

    /// <summary>
    /// 时间戳（Unix 秒）
    /// </summary>
    public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    /// API 版本
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// 链路追踪 ID
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 请求 ID
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static ApiResponse Success(string message = "success") =>
        new()
        {
            Code = 200,
            Message = message
        };

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public static ApiResponse Failure(int code, string message) =>
        new()
        {
            Code = code,
            Message = message
        };
}
