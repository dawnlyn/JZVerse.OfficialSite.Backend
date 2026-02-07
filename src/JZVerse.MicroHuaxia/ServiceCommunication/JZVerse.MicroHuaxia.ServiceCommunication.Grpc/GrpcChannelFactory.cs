using System.Collections.Concurrent;
using Grpc.Net.Client;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Configuration;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Grpc;

/// <summary>
/// gRPC Channel 工厂 - 基于服务发现创建 Channel
/// </summary>
public sealed class GrpcChannelFactory(
    IServiceInstanceSelector instanceSelector,
    IOptions<ServiceCommunicationOptions> options,
    ILogger<GrpcChannelFactory> logger) : IDisposable
{
    private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();
    private readonly ServiceCommunicationOptions _options = options.Value;
    private bool _disposed;

    /// <summary>
    /// 获取或创建 gRPC Channel
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<GrpcChannel> GetOrCreateChannelAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_channels.TryGetValue(serviceName, out var existingChannel))
        {
            return existingChannel;
        }

        // 获取服务端点配置
        var serviceConfig = _options.ServiceEndpoints.GetValueOrDefault(serviceName);

        // 如果配置了直连地址
        if (serviceConfig?.Addresses is { Count: > 0 })
        {
            var address = serviceConfig.Addresses[0];
            var channel = CreateChannel(address, serviceConfig.UseTls);
            _channels.TryAdd(serviceName, channel);
            return channel;
        }

        // 从服务发现获取实例
        var instance = await instanceSelector.SelectAsync(serviceName, null, cancellationToken);
        if (instance is null)
        {
            throw new InvalidOperationException($"No available instance for service '{serviceName}'");
        }

        var newChannel = CreateChannel(instance.Address, serviceConfig?.UseTls ?? false);

        if (_channels.TryAdd(serviceName, newChannel))
        {
            logger.LogInformation(
                "Created gRPC channel for service '{ServiceName}' to {Address}",
                serviceName,
                instance.Address);
            return newChannel;
        }

        // 另一个线程已经创建了 channel，关闭这个并返回已存在的
        newChannel.Dispose();
        return _channels[serviceName];
    }

    /// <summary>
    /// 刷新 Channel（当服务实例变更时调用）
    /// </summary>
    public void RefreshChannel(string serviceName)
    {
        if (_channels.TryRemove(serviceName, out var oldChannel))
        {
            oldChannel.Dispose();
            logger.LogInformation("Refreshed gRPC channel for service '{ServiceName}'", serviceName);
        }
    }

    private GrpcChannel CreateChannel(string address, bool useTls)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            EnableMultipleHttp2Connections = true,
        };

        var channelOptions = new GrpcChannelOptions
        {
            HttpHandler = handler,
            MaxReceiveMessageSize = 16 * 1024 * 1024, // 16 MB
            MaxSendMessageSize = 16 * 1024 * 1024,    // 16 MB
        };

        // 确保地址使用正确的协议
        if (!address.StartsWith("http://") && !address.StartsWith("https://"))
        {
            address = useTls ? $"https://{address}" : $"http://{address}";
        }

        return GrpcChannel.ForAddress(address, channelOptions);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var channel in _channels.Values)
        {
            channel.Dispose();
        }
        _channels.Clear();
    }
}
