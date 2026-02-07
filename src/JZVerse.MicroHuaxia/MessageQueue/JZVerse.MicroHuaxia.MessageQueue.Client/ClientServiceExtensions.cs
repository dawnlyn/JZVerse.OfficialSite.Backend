using JZVerse.MicroHuaxia.MessageQueue.Protocol.Tcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.MessageQueue.Client;

/// <summary>
/// 客户端服务扩展
/// </summary>
public static class ClientServiceExtensions
{
    /// <summary>
    /// 添加消息队列客户端
    /// </summary>
    public static IServiceCollection AddMessageQueueClient(
        this IServiceCollection services,
        Action<MessageQueueClientOptions>? configure = null)
    {
        var options = new MessageQueueClientOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddTcpProtocolClient();
        services.TryAddSingleton<MessageQueueClient>();

        return services;
    }

    /// <summary>
    /// 添加消息队列客户端（瞬态）
    /// </summary>
    public static IServiceCollection AddMessageQueueClientTransient(
        this IServiceCollection services,
        Action<MessageQueueClientOptions>? configure = null)
    {
        var options = new MessageQueueClientOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddTcpProtocolClient();
        services.TryAddTransient<MessageQueueClient>();

        return services;
    }
}
