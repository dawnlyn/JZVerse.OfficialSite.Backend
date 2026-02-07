using System.Net.Http.Json;
using System.Text.Json;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Protocol;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Client;

/// <summary>
/// JSON-RPC 客户端
/// </summary>
public sealed class JsonRpcClient(
    IHttpClientFactory httpClientFactory,
    IServiceInstanceSelector instanceSelector,
    IResiliencePipelineFactory pipelineFactory,
    ILogger<JsonRpcClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 调用 JSON-RPC 方法
    /// </summary>
    public async Task<TResult?> CallAsync<TResult>(
        string serviceName,
        string method,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var pipeline = pipelineFactory.GetOrCreate(serviceName);

        return await pipeline.ExecuteAsync(async ct =>
        {
            var instance = await instanceSelector.SelectAsync(serviceName, null, ct)
                ?? throw new InvalidOperationException($"No available instance for service '{serviceName}'");

            var request = new JsonRpcRequest
            {
                Method = method,
                Params = parameters,
                Id = Guid.NewGuid().ToString("N"),
            };

            var httpClient = httpClientFactory.CreateClient("JsonRpc");
            httpClient.BaseAddress = new Uri(instance.Address);

            logger.LogDebug("Calling JSON-RPC method {Method} on {ServiceName}", method, serviceName);

            var httpResponse = await httpClient.PostAsJsonAsync("/jsonrpc", request, JsonOptions, ct);
            httpResponse.EnsureSuccessStatusCode();

            var response = await httpResponse.Content.ReadFromJsonAsync<JsonRpcResponse>(JsonOptions, ct);

            if (response?.Error is not null)
            {
                throw new JsonRpcException(response.Error);
            }

            if (response?.Result is JsonElement element)
            {
                return element.Deserialize<TResult>(JsonOptions);
            }

            return default;
        }, cancellationToken);
    }

    /// <summary>
    /// 发送 JSON-RPC 通知（无返回值）
    /// </summary>
    public async Task NotifyAsync(
        string serviceName,
        string method,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var pipeline = pipelineFactory.GetOrCreate(serviceName);

        await pipeline.ExecuteAsync(async ct =>
        {
            var instance = await instanceSelector.SelectAsync(serviceName, null, ct)
                ?? throw new InvalidOperationException($"No available instance for service '{serviceName}'");

            var request = new JsonRpcRequest
            {
                Method = method,
                Params = parameters,
                Id = null, // 通知没有 ID
            };

            var httpClient = httpClientFactory.CreateClient("JsonRpc");
            httpClient.BaseAddress = new Uri(instance.Address);

            var httpResponse = await httpClient.PostAsJsonAsync("/jsonrpc", request, JsonOptions, ct);
            httpResponse.EnsureSuccessStatusCode();
        }, cancellationToken);
    }

    /// <summary>
    /// 批量调用 JSON-RPC 方法
    /// </summary>
    public async Task<JsonRpcResponse[]> BatchCallAsync(
        string serviceName,
        IEnumerable<JsonRpcRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var pipeline = pipelineFactory.GetOrCreate(serviceName);

        return await pipeline.ExecuteAsync(async ct =>
        {
            var instance = await instanceSelector.SelectAsync(serviceName, null, ct)
                ?? throw new InvalidOperationException($"No available instance for service '{serviceName}'");

            var httpClient = httpClientFactory.CreateClient("JsonRpc");
            httpClient.BaseAddress = new Uri(instance.Address);

            var httpResponse = await httpClient.PostAsJsonAsync("/jsonrpc", requests.ToArray(), JsonOptions, ct);
            httpResponse.EnsureSuccessStatusCode();

            var responses = await httpResponse.Content.ReadFromJsonAsync<JsonRpcResponse[]>(JsonOptions, ct);
            return responses ?? [];
        }, cancellationToken);
    }
}

/// <summary>
/// JSON-RPC 异常
/// </summary>
public sealed class JsonRpcException(JsonRpcError error) : Exception(error.Message)
{
    public JsonRpcError Error { get; } = error;
    public int Code => Error.Code;
}
