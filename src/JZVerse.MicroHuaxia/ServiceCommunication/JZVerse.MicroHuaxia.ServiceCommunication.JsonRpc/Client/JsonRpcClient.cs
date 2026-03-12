using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using JZVerse.MicroHuaxia.Observability.Core.Configuration;
using JZVerse.MicroHuaxia.Observability.Core.Formatting;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Client;

/// <summary>
/// JSON-RPC 客户端
/// </summary>
public sealed class JsonRpcClient(
    IHttpClientFactory httpClientFactory,
    IServiceInstanceSelector instanceSelector,
    IResiliencePipelineFactory pipelineFactory,
    ILogger<JsonRpcClient> logger,
    IOptions<ConsoleOptions> diagnosticsOptions,
    ConsoleLogFormatter diagnosticsFormatter)
{
    private readonly ConsoleOptions _diagOptions = diagnosticsOptions.Value;

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
                ?? throw new InvalidOperationException($"服务 '{serviceName}' 没有可用实例");

            var request = new JsonRpcRequest
            {
                Method = method,
                Params = parameters,
                Id = Guid.NewGuid().ToString("N"),
            };

            var httpClient = httpClientFactory.CreateClient("JsonRpc");
            httpClient.BaseAddress = new Uri(instance.Address);

            logger.LogDebug("正在调用 JSON-RPC 方法 {Method}, 目标服务: {ServiceName}", method, serviceName);

            // 诊断日志 — 请求行
            var diagEnabled = _diagOptions.Enabled;
            if (diagEnabled)
            {
                var paramsJson = parameters is not null ? JsonSerializer.Serialize(parameters, JsonOptions) : null;
                Console.WriteLine(diagnosticsFormatter.FormatJsonRpcRequest("⟹", method, serviceName, paramsJson));
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var httpResponse = await httpClient.PostAsJsonAsync("/jsonrpc", request, JsonOptions, ct);
                httpResponse.EnsureSuccessStatusCode();

                var response = await httpResponse.Content.ReadFromJsonAsync<JsonRpcResponse>(JsonOptions, ct);
                sw.Stop();

                // 诊断日志 — 响应行
                if (diagEnabled)
                {
                    var resultJson = response?.Result is JsonElement re ? re.GetRawText() : null;
                    var errorJson = response?.Error is not null ? JsonSerializer.Serialize(response.Error, JsonOptions) : null;
                    Console.WriteLine(diagnosticsFormatter.FormatJsonRpcResponse("⟸", sw.ElapsedMilliseconds, resultJson, errorJson));
                }

                if (response?.Error is not null)
                {
                    throw new JsonRpcException(response.Error);
                }

                if (response?.Result is JsonElement element)
                {
                    return element.Deserialize<TResult>(JsonOptions);
                }

                return default;
            }
            catch (JsonRpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                if (diagEnabled)
                {
                    Console.WriteLine(diagnosticsFormatter.FormatJsonRpcResponse("⟸", sw.ElapsedMilliseconds, null, ex.Message));
                }
                throw;
            }
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
                ?? throw new InvalidOperationException($"服务 '{serviceName}' 没有可用实例");

            var request = new JsonRpcRequest
            {
                Method = method,
                Params = parameters,
                Id = null, // 通知没有 ID
            };

            var httpClient = httpClientFactory.CreateClient("JsonRpc");
            httpClient.BaseAddress = new Uri(instance.Address);

            // 诊断日志 — 请求行
            var diagEnabled = _diagOptions.Enabled;
            if (diagEnabled)
            {
                var paramsJson = parameters is not null ? JsonSerializer.Serialize(parameters, JsonOptions) : null;
                Console.WriteLine(diagnosticsFormatter.FormatJsonRpcRequest("⟹", method, serviceName, paramsJson));
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var httpResponse = await httpClient.PostAsJsonAsync("/jsonrpc", request, JsonOptions, ct);
                httpResponse.EnsureSuccessStatusCode();
                sw.Stop();

                if (diagEnabled)
                {
                    Console.WriteLine(diagnosticsFormatter.FormatJsonRpcResponse("⟸", sw.ElapsedMilliseconds, null, null));
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                if (diagEnabled)
                {
                    Console.WriteLine(diagnosticsFormatter.FormatJsonRpcResponse("⟸", sw.ElapsedMilliseconds, null, ex.Message));
                }
                throw;
            }
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

            var requestArray = requests.ToArray();

            // 诊断日志 — 请求行
            var diagEnabled = _diagOptions.Enabled;
            if (diagEnabled)
            {
                var batchInfo = $"[batch: {requestArray.Length} calls]";
                var methods = string.Join(", ", requestArray.Select(r => r.Method));
                Console.WriteLine(diagnosticsFormatter.FormatJsonRpcRequest("⟹", methods, serviceName, batchInfo));
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var httpResponse = await httpClient.PostAsJsonAsync("/jsonrpc", requestArray, JsonOptions, ct);
                httpResponse.EnsureSuccessStatusCode();

                var responses = await httpResponse.Content.ReadFromJsonAsync<JsonRpcResponse[]>(JsonOptions, ct);
                sw.Stop();

                if (diagEnabled)
                {
                    var resultCount = responses?.Length ?? 0;
                    Console.WriteLine(diagnosticsFormatter.FormatJsonRpcResponse("⟸", sw.ElapsedMilliseconds, $"[批量: {resultCount} 个响应]", null));
                }

                return responses ?? [];
            }
            catch (Exception ex)
            {
                sw.Stop();
                if (diagEnabled)
                {
                    Console.WriteLine(diagnosticsFormatter.FormatJsonRpcResponse("⟸", sw.ElapsedMilliseconds, null, ex.Message));
                }
                throw;
            }
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
