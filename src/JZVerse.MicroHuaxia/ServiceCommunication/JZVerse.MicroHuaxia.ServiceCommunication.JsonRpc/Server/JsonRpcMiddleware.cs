using System.Text.Json;
using JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Protocol;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Server;

/// <summary>
/// JSON-RPC 中间件
/// </summary>
public sealed class JsonRpcMiddleware(
    RequestDelegate next,
    JsonRpcMethodRegistry registry,
    ILogger<JsonRpcMiddleware> logger)
{
    private const string JsonRpcPath = "/jsonrpc";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task InvokeAsync(HttpContext context)
    {
        // 检查是否是 JSON-RPC 请求
        if (!IsJsonRpcRequest(context))
        {
            await next(context);
            return;
        }

        context.Response.ContentType = "application/json";

        try
        {
            // 读取请求体
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                await WriteResponseAsync(context, JsonRpcResponse.Failure(
                    JsonRpcError.InvalidRequest("Empty request body"), null));
                return;
            }

            // 检查是否是批量请求
            var trimmedBody = body.TrimStart();
            if (trimmedBody.StartsWith('['))
            {
                await HandleBatchRequestAsync(context, body);
            }
            else
            {
                await HandleSingleRequestAsync(context, body);
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse JSON-RPC request");
            await WriteResponseAsync(context, JsonRpcResponse.Failure(
                JsonRpcError.ParseError(ex.Message), null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing JSON-RPC request");
            await WriteResponseAsync(context, JsonRpcResponse.Failure(
                JsonRpcError.InternalError(ex.Message), null));
        }
    }

    private async Task HandleSingleRequestAsync(HttpContext context, string body)
    {
        var request = JsonSerializer.Deserialize<JsonRpcRequest>(body, JsonOptions);

        if (request is null || request.JsonRpc != "2.0" || string.IsNullOrEmpty(request.Method))
        {
            await WriteResponseAsync(context, JsonRpcResponse.Failure(
                JsonRpcError.InvalidRequest(), null));
            return;
        }

        var response = await ProcessRequestAsync(request, context.RequestAborted);
        await WriteResponseAsync(context, response);
    }

    private async Task HandleBatchRequestAsync(HttpContext context, string body)
    {
        var requests = JsonSerializer.Deserialize<JsonRpcRequest[]>(body, JsonOptions);

        if (requests is null || requests.Length == 0)
        {
            await WriteResponseAsync(context, JsonRpcResponse.Failure(
                JsonRpcError.InvalidRequest("Empty batch request"), null));
            return;
        }

        var responses = await Task.WhenAll(
            requests.Select(r => ProcessRequestAsync(r, context.RequestAborted)));

        // 过滤掉通知（没有 ID 的请求）
        var filteredResponses = responses.Where(r => r.Id is not null).ToArray();

        await WriteResponseAsync(context, filteredResponses);
    }

    private async Task<JsonRpcResponse> ProcessRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Processing JSON-RPC request: {Method}", request.Method);

        // 查找处理器
        var handler = registry.GetHandler(request.Method);

        if (handler is null)
        {
            logger.LogWarning("Method not found: {Method}", request.Method);
            return JsonRpcResponse.Failure(
                JsonRpcError.MethodNotFound($"Method '{request.Method}' not found"),
                request.Id);
        }

        try
        {
            // 转换参数为 JsonElement
            JsonElement? parameters = null;
            if (request.Params is JsonElement element)
            {
                parameters = element;
            }
            else if (request.Params is not null)
            {
                var json = JsonSerializer.Serialize(request.Params, JsonOptions);
                parameters = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            }

            var result = await handler.HandleAsync(parameters, cancellationToken);
            return JsonRpcResponse.Success(result, request.Id);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid params for method: {Method}", request.Method);
            return JsonRpcResponse.Failure(
                JsonRpcError.InvalidParams(ex.Message),
                request.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing method: {Method}", request.Method);
            return JsonRpcResponse.Failure(
                JsonRpcError.InternalError(ex.Message),
                request.Id);
        }
    }

    private static bool IsJsonRpcRequest(HttpContext context)
    {
        return context.Request.Method == HttpMethods.Post &&
               context.Request.Path.StartsWithSegments(JsonRpcPath);
    }

    private static async Task WriteResponseAsync(HttpContext context, JsonRpcResponse response)
    {
        await context.Response.WriteAsJsonAsync(response, JsonOptions);
    }

    private static async Task WriteResponseAsync(HttpContext context, JsonRpcResponse[] responses)
    {
        await context.Response.WriteAsJsonAsync(responses, JsonOptions);
    }
}
