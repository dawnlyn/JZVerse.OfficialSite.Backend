using System.Text.Json.Serialization;

namespace JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Protocol;

/// <summary>
/// JSON-RPC 2.0 请求
/// </summary>
public sealed record JsonRpcRequest
{
    /// <summary>
    /// JSON-RPC 版本（必须为 "2.0"）
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>
    /// 方法名
    /// </summary>
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    /// <summary>
    /// 参数（可以是对象或数组）
    /// </summary>
    [JsonPropertyName("params")]
    public object? Params { get; init; }

    /// <summary>
    /// 请求 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

/// <summary>
/// JSON-RPC 2.0 响应
/// </summary>
public sealed record JsonRpcResponse
{
    /// <summary>
    /// JSON-RPC 版本
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>
    /// 结果（成功时）
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }

    /// <summary>
    /// 错误（失败时）
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; init; }

    /// <summary>
    /// 请求 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static JsonRpcResponse Success(object? result, string? id) => new()
    {
        Result = result,
        Id = id,
    };

    /// <summary>
    /// 创建错误响应
    /// </summary>
    public static JsonRpcResponse Failure(JsonRpcError error, string? id) => new()
    {
        Error = error,
        Id = id,
    };
}

/// <summary>
/// JSON-RPC 2.0 错误
/// </summary>
public sealed record JsonRpcError
{
    /// <summary>
    /// 错误码
    /// </summary>
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// 附加数据
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; init; }

    // 标准错误码
    public static JsonRpcError ParseError(string? data = null) => new()
    {
        Code = -32700,
        Message = "Parse error",
        Data = data,
    };

    public static JsonRpcError InvalidRequest(string? data = null) => new()
    {
        Code = -32600,
        Message = "Invalid Request",
        Data = data,
    };

    public static JsonRpcError MethodNotFound(string? data = null) => new()
    {
        Code = -32601,
        Message = "Method not found",
        Data = data,
    };

    public static JsonRpcError InvalidParams(string? data = null) => new()
    {
        Code = -32602,
        Message = "Invalid params",
        Data = data,
    };

    public static JsonRpcError InternalError(string? data = null) => new()
    {
        Code = -32603,
        Message = "Internal error",
        Data = data,
    };

    public static JsonRpcError ServerError(int code, string message, string? data = null) => new()
    {
        Code = code,
        Message = message,
        Data = data,
    };
}
