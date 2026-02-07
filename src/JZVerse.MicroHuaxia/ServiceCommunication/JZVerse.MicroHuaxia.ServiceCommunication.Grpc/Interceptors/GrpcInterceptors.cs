using Grpc.Core;
using Grpc.Core.Interceptors;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;
using Microsoft.Extensions.Logging;

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
                logger.LogWarning(ex, "gRPC call failed after retries: {Method}", context.Method.FullName);
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
                logger.LogWarning(ex, "gRPC call failed, circuit breaker will record failure: {Method}", context.Method.FullName);
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
/// 日志拦截器
/// </summary>
public sealed class LoggingInterceptor(ILogger<LoggingInterceptor> logger) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var methodName = context.Method.FullName;
        var startTime = DateTimeOffset.UtcNow;

        logger.LogDebug("gRPC call started: {Method}", methodName);

        var call = continuation(request, context);

        async Task<TResponse> LoggedResponseAsync()
        {
            try
            {
                var response = await call.ResponseAsync;
                var duration = DateTimeOffset.UtcNow - startTime;
                logger.LogDebug("gRPC call completed: {Method} in {Duration}ms", methodName, duration.TotalMilliseconds);
                return response;
            }
            catch (Exception ex)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                logger.LogError(ex, "gRPC call failed: {Method} after {Duration}ms", methodName, duration.TotalMilliseconds);
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
}
