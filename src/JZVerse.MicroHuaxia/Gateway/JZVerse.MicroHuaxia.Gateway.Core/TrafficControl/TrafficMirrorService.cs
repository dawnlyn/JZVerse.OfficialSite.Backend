using JZVerse.MicroHuaxia.Gateway.Abstractions.TrafficControl;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Core.TrafficControl;

/// <summary>
/// 流量镜像服务实现
/// </summary>
public sealed class TrafficMirrorService : ITrafficMirrorService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TrafficMirrorService> _logger;
    private static readonly Random Random = new();

    public TrafficMirrorService(
        IHttpClientFactory httpClientFactory,
        ILogger<TrafficMirrorService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TrafficMirror");
        _logger = logger;
    }

    public async Task MirrorAsync(
        HttpContext context,
        HttpRequestMessage originalRequest,
        RouteTrafficMirror mirrorConfig,
        CancellationToken cancellationToken = default)
    {
        // 检查是否需要镜像（基于采样比例）
        if (!ShouldMirror(mirrorConfig.SamplePercentage))
        {
            return;
        }

        // 异步执行镜像，不等待结果
        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteMirrorAsync(context, originalRequest, mirrorConfig);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "流量镜像失败: {TargetAddress}", mirrorConfig.TargetAddress);
            }
        }, CancellationToken.None);
    }

    private async Task ExecuteMirrorAsync(
        HttpContext context,
        HttpRequestMessage originalRequest,
        RouteTrafficMirror mirrorConfig)
    {
        using var cts = new CancellationTokenSource(mirrorConfig.Timeout);

        // 创建镜像请求
        var mirrorRequest = await CreateMirrorRequestAsync(context, originalRequest, mirrorConfig);

        _logger.LogDebug(
            "发送镜像请求: {Method} {Uri}",
            mirrorRequest.Method, mirrorRequest.RequestUri);

        try
        {
            using var response = await _httpClient.SendAsync(
                mirrorRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);

            if (!mirrorConfig.IgnoreResponse)
            {
                _logger.LogInformation(
                    "镜像请求响应: {StatusCode} for {Uri}",
                    (int)response.StatusCode, mirrorRequest.RequestUri);
            }
        }
        catch (TaskCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "镜像请求超时: {Uri}, 超时时间: {Timeout}ms",
                mirrorRequest.RequestUri, mirrorConfig.Timeout.TotalMilliseconds);
        }
    }

    private async Task<HttpRequestMessage> CreateMirrorRequestAsync(
        HttpContext context,
        HttpRequestMessage originalRequest,
        RouteTrafficMirror mirrorConfig)
    {
        // 构建目标 URI
        var targetUri = BuildTargetUri(originalRequest.RequestUri!, mirrorConfig);

        var mirrorRequest = new HttpRequestMessage(originalRequest.Method, targetUri);

        // 复制请求头
        foreach (var header in originalRequest.Headers)
        {
            // 跳过 Host 头，让 HttpClient 自动设置
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            mirrorRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // 添加镜像标记头
        if (mirrorConfig.AddMirrorHeader)
        {
            mirrorRequest.Headers.TryAddWithoutValidation(
                mirrorConfig.MirrorHeaderName,
                mirrorConfig.MirrorHeaderValue);
        }

        // 添加原始请求信息
        mirrorRequest.Headers.TryAddWithoutValidation("X-Original-Host", context.Request.Host.ToString());
        mirrorRequest.Headers.TryAddWithoutValidation("X-Original-Path", context.Request.Path.ToString());

        // 复制请求体
        if (mirrorConfig.CopyRequestBody && originalRequest.Content != null)
        {
            // 读取原始请求体内容
            var contentBytes = await originalRequest.Content.ReadAsByteArrayAsync();
            mirrorRequest.Content = new ByteArrayContent(contentBytes);

            // 复制 Content Headers
            foreach (var header in originalRequest.Content.Headers)
            {
                mirrorRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return mirrorRequest;
    }

    private static Uri BuildTargetUri(Uri originalUri, RouteTrafficMirror mirrorConfig)
    {
        var targetBase = new Uri(mirrorConfig.TargetAddress);
        var path = originalUri.PathAndQuery;

        // 应用路径转换（如果配置了）
        if (!string.IsNullOrEmpty(mirrorConfig.PathTransform))
        {
            // 简单的路径替换，格式: "old/path:new/path"
            var parts = mirrorConfig.PathTransform.Split(':');
            if (parts.Length == 2)
            {
                path = path.Replace(parts[0], parts[1]);
            }
        }

        return new Uri(targetBase, path);
    }

    private static bool ShouldMirror(int samplePercentage)
    {
        if (samplePercentage >= 100)
        {
            return true;
        }

        if (samplePercentage <= 0)
        {
            return false;
        }

        return Random.Next(100) < samplePercentage;
    }
}
