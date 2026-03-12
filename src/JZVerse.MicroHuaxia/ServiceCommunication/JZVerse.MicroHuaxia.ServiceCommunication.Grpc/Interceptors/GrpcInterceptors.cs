using System.Diagnostics;
using System.Text.Json;
using Grpc.Core;
using Grpc.Core.Interceptors;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using JZVerse.MicroHuaxia.Observability.Core.Configuration;
using JZVerse.MicroHuaxia.Observability.Core.Formatting;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Grpc.Interceptors;

/// <summary>
/// 重试拦截器
/// </summary>
public sealed class RetryInterceptor(
    IRetryPolicy retryPolicy,
    ILogger<RetryInterceptor> logger) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(request, context);

        async Task<TResponse> RetryableResponseAsync()
        {
            try
            {
                return await retryPolicy.ExecuteAsync(
                    async _ => await call.ResponseAsync,
                    context.Options.CancellationToken);
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "gRPC 调用重试后仍失败: {Method}", context.Method.FullName);
                throw;
            }
        }

        return new AsyncUnaryCall<TResponse>(
            RetryableResponseAsync(),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }
}

/// <summary>
/// 熔断器拦截器
/// </summary>
public sealed class CircuitBreakerInterceptor(
    ICircuitBreakerFactory circuitBreakerFactory,
    ILogger<CircuitBreakerInterceptor> logger) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var serviceName = context.Method.ServiceName;
        var circuitBreaker = circuitBreakerFactory.GetOrCreate(serviceName);

        if (circuitBreaker.State == CircuitBreakerState.Open)
        {
            logger.LogWarning("Circuit breaker is open for service: {ServiceName}", serviceName);
            throw new BrokenCircuitException($"Circuit breaker is open for service '{serviceName}'");
        }

        var call = continuation(request, context);

        async Task<TResponse> ProtectedResponseAsync()
        {
            try
            {
                var response = await call.ResponseAsync;
                // 成功不需要额外操作，熔断器会自动处理
                return response;
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "gRPC 调用失败，熔断器将记录此次失败: {Method}", context.Method.FullName);
                throw;
            }
        }

        return new AsyncUnaryCall<TResponse>(
            ProtectedResponseAsync(),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }
}

/// <summary>
/// 日志拦截器 — 发起方打印发出的 gRPC 请求和收到的响应
/// </summary>
public sealed class LoggingInterceptor(
    ILogger<LoggingInterceptor> logger,
    IOptions<ConsoleOptions> options,
    ConsoleLogFormatter formatter) : Interceptor
{
    private static readonly JsonSerializerOptions JsonSerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            return continuation(request, context);
        }

        var methodName = context.Method.FullName;

        // 打印请求行
        var requestJson = TrySerializeProtobuf(request);
        var requestLog = formatter.FormatGrpcRequest("⟹", methodName, requestJson);
        logger.Log(LogLevel.Debug, "{Message}", requestLog);

        var sw = Stopwatch.StartNew();
        var call = continuation(request, context);

        async Task<TResponse> LoggedResponseAsync()
        {
            try
            {
                var response = await call.ResponseAsync;
                sw.Stop();

                // 打印响应行
                var responseJson = TrySerializeProtobuf(response);
                var responseLog = formatter.FormatGrpcResponse("⟸", "OK", sw.ElapsedMilliseconds, responseJson);
                logger.Log(LogLevel.Debug, "{Message}", responseLog);

                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();

                var errorLog = formatter.FormatGrpcError(sw.ElapsedMilliseconds, ex);
                logger.Log(LogLevel.Warning, ex, "{Message}", errorLog);

                throw;
            }
        }

        return new AsyncUnaryCall<TResponse>(
            LoggedResponseAsync(),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private static string? TrySerializeProtobuf<T>(T message)
    {
        try
        {
            return JsonSerializer.Serialize(message, JsonSerializeOptions);
        }
        catch
        {
            return $"<{typeof(T).Name}>";
        }
    }
}
