using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Http.Handlers;

/// <summary>
/// 服务发现 HTTP 处理器 - 将服务名解析为实际地址
/// </summary>
public sealed class ServiceDiscoveryHandler(
    IServiceInstanceSelector instanceSelector,
    ILogger<ServiceDiscoveryHandler> logger) : DelegatingHandler
{
    private const string ServiceNameHeader = "X-Service-Name";
    private const string InstanceIdHeader = "X-Instance-Id";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 从请求头获取服务名
        if (!request.Headers.TryGetValues(ServiceNameHeader, out var serviceNameValues))
        {
            // 没有服务名，直接发送
            return await base.SendAsync(request, cancellationToken);
        }

        var serviceName = serviceNameValues.FirstOrDefault();
        if (string.IsNullOrEmpty(serviceName))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // 移除服务名头
        request.Headers.Remove(ServiceNameHeader);

        // 选择服务实例
        var context = CreateLoadBalancerContext(request);
        var instance = await instanceSelector.SelectAsync(serviceName, context, cancellationToken);

        if (instance is null)
        {
            logger.LogError("No available instance for service '{ServiceName}'", serviceName);
            throw new InvalidOperationException($"No available instance for service '{serviceName}'");
        }

        // 更新请求 URI
        var originalUri = request.RequestUri!;
        var newUri = new UriBuilder(instance.Address)
        {
            Path = originalUri.AbsolutePath,
            Query = originalUri.Query,
        }.Uri;

        request.RequestUri = newUri;
        request.Headers.Add(InstanceIdHeader, instance.InstanceId);

        logger.LogDebug(
            "Resolved service '{ServiceName}' to instance {InstanceId} ({Address})",
            serviceName,
            instance.InstanceId,
            instance.Address);

        return await base.SendAsync(request, cancellationToken);
    }

    private static LoadBalancerContext? CreateLoadBalancerContext(HttpRequestMessage request)
    {
        string? version = null;
        HashSet<string>? tags = null;

        if (request.Headers.TryGetValues("X-Service-Version", out var versionValues))
        {
            version = versionValues.FirstOrDefault();
            request.Headers.Remove("X-Service-Version");
        }

        if (request.Headers.TryGetValues("X-Service-Tags", out var tagValues))
        {
            var tagString = tagValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(tagString))
            {
                tags = [.. tagString.Split(',', StringSplitOptions.RemoveEmptyEntries)];
            }
            request.Headers.Remove("X-Service-Tags");
        }

        if (string.IsNullOrEmpty(version) && tags is null)
        {
            return null;
        }

        return new LoadBalancerContext
        {
            PreferredVersion = version,
            PreferredTags = tags,
        };
    }
}
