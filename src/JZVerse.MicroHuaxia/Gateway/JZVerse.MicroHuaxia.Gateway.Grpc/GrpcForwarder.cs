using Grpc.Core;
using Grpc.Net.Client;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Grpc;

/// <summary>
/// gRPC 协议转发器
/// </summary>
public sealed class GrpcForwarder : IProtocolForwarder
{
    private readonly ILogger<GrpcForwarder> _logger;
    private readonly IServiceDiscovery? _serviceDiscovery;

    public string Protocol => "grpc";

    public GrpcForwarder(
        ILogger<GrpcForwarder> logger,
        IServiceDiscovery? serviceDiscovery = null)
    {
        _logger = logger;
        _serviceDiscovery = serviceDiscovery;
    }

    public bool CanHandle(HttpContext context)
    {
        // gRPC 请求特征：HTTP/2 + content-type: application/grpc
        var contentType = context.Request.ContentType;
        return contentType is not null &&
               contentType.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase);
    }

    public async Task ForwardAsync(HttpContext context, RouteMatchResult matchResult, CancellationToken cancellationToken = default)
    {
        var destination = matchResult.Route.Destination;
        var targetAddress = await ResolveTargetAddressAsync(destination, cancellationToken);

        if (string.IsNullOrEmpty(targetAddress))
        {
            _logger.LogError("无法解析 gRPC 目标地址");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        _logger.LogDebug("gRPC 转发: {OriginalPath} -> {TargetAddress}", context.Request.Path, targetAddress);

        try
        {
            using var channel = GrpcChannel.ForAddress(targetAddress);

            // gRPC 代理需要使用 CallInvoker 进行透明转发
            // 这里提供一个简化实现，实际生产环境可能需要更复杂的处理
            var method = context.Request.Path.Value?.TrimStart('/') ?? "";
            var serviceName = method.Split('/').FirstOrDefault() ?? "";

            // 读取请求体
            using var memoryStream = new MemoryStream();
            await context.Request.Body.CopyToAsync(memoryStream, cancellationToken);
            var requestBytes = memoryStream.ToArray();

            // 创建 gRPC 调用
            var callOptions = new CallOptions(cancellationToken: cancellationToken);

            // 复制元数据
            var metadata = new Metadata();
            foreach (var header in context.Request.Headers)
            {
                if (IsGrpcHeader(header.Key))
                    continue;

                metadata.Add(header.Key, header.Value.ToString());
            }

            callOptions = callOptions.WithHeaders(metadata);

            // 注意：这是一个简化的 gRPC 代理实现
            // 完整实现需要处理不同的 gRPC 调用类型（Unary、ServerStreaming、ClientStreaming、Duplex）
            // 并且需要动态创建方法描述符

            _logger.LogWarning("gRPC 转发需要更完整的实现，当前仅支持基本的透传");

            context.Response.StatusCode = StatusCodes.Status501NotImplemented;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "NotImplemented",
                message = "gRPC 完整代理功能正在开发中"
            });
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC 调用失败");
            context.Response.StatusCode = MapGrpcStatusToHttp(ex.StatusCode);
            await context.Response.WriteAsJsonAsync(new
            {
                error = ex.StatusCode.ToString(),
                message = ex.Status.Detail
            });
        }
    }

    private async Task<string?> ResolveTargetAddressAsync(RouteDestination destination, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(destination.DirectAddress))
        {
            return destination.DirectAddress;
        }

        if (!string.IsNullOrEmpty(destination.ServiceName) && _serviceDiscovery is not null)
        {
            var instances = await _serviceDiscovery.GetInstancesAsync(destination.ServiceName, cancellationToken);
            var healthyInstances = instances.Where(i => i.Enabled).ToList();

            if (healthyInstances.Count > 0)
            {
                var index = Random.Shared.Next(healthyInstances.Count);
                return healthyInstances[index].Address;
            }
        }

        return null;
    }

    private static bool IsGrpcHeader(string headerName)
    {
        return headerName.StartsWith("grpc-", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("te", StringComparison.OrdinalIgnoreCase);
    }

    private static int MapGrpcStatusToHttp(StatusCode grpcStatus)
    {
        return grpcStatus switch
        {
            StatusCode.OK => StatusCodes.Status200OK,
            StatusCode.Cancelled => 499, // Client Closed Request
            StatusCode.Unknown => StatusCodes.Status500InternalServerError,
            StatusCode.InvalidArgument => StatusCodes.Status400BadRequest,
            StatusCode.DeadlineExceeded => StatusCodes.Status504GatewayTimeout,
            StatusCode.NotFound => StatusCodes.Status404NotFound,
            StatusCode.AlreadyExists => StatusCodes.Status409Conflict,
            StatusCode.PermissionDenied => StatusCodes.Status403Forbidden,
            StatusCode.ResourceExhausted => StatusCodes.Status429TooManyRequests,
            StatusCode.FailedPrecondition => StatusCodes.Status400BadRequest,
            StatusCode.Aborted => StatusCodes.Status409Conflict,
            StatusCode.OutOfRange => StatusCodes.Status400BadRequest,
            StatusCode.Unimplemented => StatusCodes.Status501NotImplemented,
            StatusCode.Internal => StatusCodes.Status500InternalServerError,
            StatusCode.Unavailable => StatusCodes.Status503ServiceUnavailable,
            StatusCode.DataLoss => StatusCodes.Status500InternalServerError,
            StatusCode.Unauthenticated => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };
    }
}
