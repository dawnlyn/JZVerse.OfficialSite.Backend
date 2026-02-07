using System.Collections.Concurrent;
using System.Text.Json;
using JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Protocol;

namespace JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Server;

/// <summary>
/// JSON-RPC 方法处理器接口
/// </summary>
public interface IJsonRpcHandler
{
    /// <summary>
    /// 方法名
    /// </summary>
    string Method { get; }

    /// <summary>
    /// 处理请求
    /// </summary>
    Task<object?> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken);
}

/// <summary>
/// 泛型 JSON-RPC 方法处理器
/// </summary>
public abstract class JsonRpcHandler<TParams, TResult> : IJsonRpcHandler where TParams : class
{
    public abstract string Method { get; }

    public async Task<object?> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        TParams? typedParams = default;

        if (parameters.HasValue && parameters.Value.ValueKind != JsonValueKind.Null)
        {
            typedParams = parameters.Value.Deserialize<TParams>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }

        return await ExecuteAsync(typedParams, cancellationToken);
    }

    protected abstract Task<TResult?> ExecuteAsync(TParams? parameters, CancellationToken cancellationToken);
}

/// <summary>
/// 无参数 JSON-RPC 方法处理器
/// </summary>
public abstract class JsonRpcHandler<TResult> : IJsonRpcHandler
{
    public abstract string Method { get; }

    public async Task<object?> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(cancellationToken);
    }

    protected abstract Task<TResult?> ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>
/// JSON-RPC 方法注册表
/// </summary>
public sealed class JsonRpcMethodRegistry
{
    private readonly ConcurrentDictionary<string, IJsonRpcHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 注册处理器
    /// </summary>
    public void Register(IJsonRpcHandler handler)
    {
        _handlers[handler.Method] = handler;
    }

    /// <summary>
    /// 获取处理器
    /// </summary>
    public IJsonRpcHandler? GetHandler(string method)
    {
        return _handlers.GetValueOrDefault(method);
    }

    /// <summary>
    /// 获取所有注册的方法名
    /// </summary>
    public IEnumerable<string> GetMethods() => _handlers.Keys;
}
