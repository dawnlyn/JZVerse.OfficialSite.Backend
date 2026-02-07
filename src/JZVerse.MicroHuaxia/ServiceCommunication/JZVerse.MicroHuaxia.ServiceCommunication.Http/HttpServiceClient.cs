using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Configuration;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Http;

/// <summary>
/// HTTP 服务客户端实现
/// </summary>
public sealed class HttpServiceClient(
    IHttpClientFactory httpClientFactory,
    IServiceInstanceSelector instanceSelector,
    IResiliencePipelineFactory pipelineFactory,
    IOptions<ServiceCommunicationOptions> options,
    ILogger<HttpServiceClient> logger) : IServiceClient
{
    private readonly ServiceCommunicationOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<TResponse> SendAsync<TResponse>(
        string serviceName,
        ServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var pipeline = pipelineFactory.GetOrCreate(serviceName);
        var context = CreateLoadBalancerContext(request);

        return await pipeline.ExecuteAsync(async ct =>
        {
            var instance = await instanceSelector.SelectAsync(serviceName, context, ct)
                ?? throw new InvalidOperationException($"No available instance for service '{serviceName}'");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var httpClient = httpClientFactory.CreateClient("ServiceCommunication");
                httpClient.BaseAddress = new Uri(instance.Address);

                var httpRequest = CreateHttpRequest(request);
                var response = await httpClient.SendAsync(httpRequest, ct);

                response.EnsureSuccessStatusCode();

                stopwatch.Stop();
                instanceSelector.ReportInstanceStatus(instance, true, stopwatch.Elapsed);

                var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
                return result ?? throw new InvalidOperationException("Failed to deserialize response");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                instanceSelector.ReportInstanceStatus(instance, false, stopwatch.Elapsed, ex);
                logger.LogWarning(ex, "Request to {ServiceName} ({Address}) failed", serviceName, instance.Address);
                throw;
            }
        }, cancellationToken);
    }

    public async Task SendAsync(
        string serviceName,
        ServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var pipeline = pipelineFactory.GetOrCreate(serviceName);
        var context = CreateLoadBalancerContext(request);

        await pipeline.ExecuteAsync(async ct =>
        {
            var instance = await instanceSelector.SelectAsync(serviceName, context, ct)
                ?? throw new InvalidOperationException($"No available instance for service '{serviceName}'");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var httpClient = httpClientFactory.CreateClient("ServiceCommunication");
                httpClient.BaseAddress = new Uri(instance.Address);

                var httpRequest = CreateHttpRequest(request);
                var response = await httpClient.SendAsync(httpRequest, ct);

                response.EnsureSuccessStatusCode();

                stopwatch.Stop();
                instanceSelector.ReportInstanceStatus(instance, true, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                instanceSelector.ReportInstanceStatus(instance, false, stopwatch.Elapsed, ex);
                throw;
            }
        }, cancellationToken);
    }

    private static HttpRequestMessage CreateHttpRequest(ServiceRequest request)
    {
        var method = request.Method ?? HttpMethod.Get;
        var path = request.Path;

        // 添加查询参数
        if (request.QueryParams.Count > 0)
        {
            var query = string.Join("&", request.QueryParams
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            path = $"{path}?{query}";
        }

        var httpRequest = new HttpRequestMessage(method, path);

        // 添加请求头
        foreach (var header in request.Headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // 添加请求体
        if (request.Body is not null)
        {
            httpRequest.Content = JsonContent.Create(request.Body, options: JsonOptions);
        }

        return httpRequest;
    }

    private static LoadBalancerContext? CreateLoadBalancerContext(ServiceRequest request)
    {
        if (string.IsNullOrEmpty(request.TargetVersion) && request.TargetTags is null)
        {
            return null;
        }

        return new LoadBalancerContext
        {
            PreferredVersion = request.TargetVersion,
            PreferredTags = request.TargetTags,
        };
    }
}
