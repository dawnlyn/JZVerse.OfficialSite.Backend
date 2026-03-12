using System.Net.WebSockets;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.WebSocket;

/// <summary>
/// WebSocket 协议转发器
/// </summary>
public sealed class WebSocketForwarder : IProtocolForwarder
{
    private readonly ILogger<WebSocketForwarder> _logger;
    private readonly IServiceDiscovery? _serviceDiscovery;

    public string Protocol => "websocket";

    public WebSocketForwarder(
        ILogger<WebSocketForwarder> logger,
        IServiceDiscovery? serviceDiscovery = null)
    {
        _logger = logger;
        _serviceDiscovery = serviceDiscovery;
    }

    public bool CanHandle(HttpContext context)
    {
        return context.WebSockets.IsWebSocketRequest;
    }

    public async Task ForwardAsync(HttpContext context, RouteMatchResult matchResult, CancellationToken cancellationToken = default)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var destination = matchResult.Route.Destination;
        var targetAddress = await ResolveTargetAddressAsync(destination, cancellationToken);

        if (string.IsNullOrEmpty(targetAddress))
        {
            _logger.LogError("无法解析 WebSocket 目标地址");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        // 构建 WebSocket URI
        var targetPath = matchResult.TransformedPath ?? context.Request.Path.Value ?? "/";
        var wsScheme = targetAddress.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        var targetUri = new Uri(targetAddress);
        var wsUri = new UriBuilder(wsScheme, targetUri.Host, targetUri.Port, targetPath, context.Request.QueryString.ToString()).Uri;

        _logger.LogDebug("WebSocket 转发: {OriginalPath} -> {TargetUri}", context.Request.Path, wsUri);

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();
        using var serverSocket = new ClientWebSocket();

        // 复制请求头
        foreach (var header in context.Request.Headers)
        {
            if (IsWebSocketHeader(header.Key))
                continue;

            try
            {
                serverSocket.Options.SetRequestHeader(header.Key, header.Value.ToString());
            }
            catch
            {
                // 忽略无法设置的头
            }
        }

        try
        {
            await serverSocket.ConnectAsync(wsUri, cancellationToken);
            await ProxyWebSocketAsync(clientSocket, serverSocket, cancellationToken);
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "WebSocket 连接失败: {Uri}", wsUri);
        }
    }

    private async Task ProxyWebSocketAsync(
        System.Net.WebSockets.WebSocket clientSocket,
        System.Net.WebSockets.WebSocket serverSocket,
        CancellationToken cancellationToken)
    {
        var clientToServer = TransferAsync(clientSocket, serverSocket, "Client->Server", cancellationToken);
        var serverToClient = TransferAsync(serverSocket, clientSocket, "Server->Client", cancellationToken);

        await Task.WhenAny(clientToServer, serverToClient);

        // 关闭连接
        if (clientSocket.State == WebSocketState.Open)
        {
            await clientSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "连接关闭", CancellationToken.None);
        }

        if (serverSocket.State == WebSocketState.Open)
        {
            await serverSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "连接关闭", CancellationToken.None);
        }
    }

    private async Task TransferAsync(
        System.Net.WebSockets.WebSocket source,
        System.Net.WebSockets.WebSocket destination,
        string direction,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (source.State == WebSocketState.Open && destination.State == WebSocketState.Open)
            {
                var result = await source.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogDebug("WebSocket 关闭: {Direction}", direction);
                    break;
                }

                await destination.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket 传输错误: {Direction}", direction);
        }
    }

    private async Task<string?> ResolveTargetAddressAsync(RouteDestination destination, CancellationToken cancellationToken)
    {
        // 1. 优先使用服务发现
        if (!string.IsNullOrEmpty(destination.ServiceName) && _serviceDiscovery is not null)
        {
            var instances = await _serviceDiscovery.GetInstancesAsync(destination.ServiceName, cancellationToken);
            var healthyInstances = instances.Where(i => i.Enabled).ToList();

            if (healthyInstances.Count > 0)
            {
                var index = Random.Shared.Next(healthyInstances.Count);
                return healthyInstances[index].Address;
            }

            // SD 未返回结果，降级到直接地址
            if (!string.IsNullOrEmpty(destination.DirectAddress))
            {
                _logger.LogWarning("服务发现未找到 {ServiceName} 的可用实例，WebSocket 降级到 DirectAddress: {DirectAddress}",
                    destination.ServiceName, destination.DirectAddress);
            }
        }

        // 2. 兜底：使用直接地址
        return destination.DirectAddress;
    }

    private static bool IsWebSocketHeader(string headerName)
    {
        return headerName.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Upgrade", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Sec-WebSocket-Version", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Sec-WebSocket-Extensions", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Sec-WebSocket-Protocol", StringComparison.OrdinalIgnoreCase);
    }
}
