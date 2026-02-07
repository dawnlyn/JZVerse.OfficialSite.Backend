using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Routing;
using JZVerse.MicroHuaxia.MessageQueue.Core;
using JZVerse.MicroHuaxia.MessageQueue.Protocol.InProc;
using JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.MessageQueue.AspNetCore;

/// <summary>
/// 消息队列服务扩展
/// </summary>
public static class MessageQueueExtensions
{
    /// <summary>
    /// 添加消息队列服务
    /// </summary>
    public static IServiceCollection AddMessageQueue(
        this IServiceCollection services,
        Action<MessageQueueOptions>? configure = null)
    {
        var options = new MessageQueueOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // 添加存储层
        switch (options.Storage)
        {
            case StorageType.Memory:
                services.AddMemoryMessageStorage(opts =>
                {
                    opts.MaxMessagesPerPartition = options.MaxMessagesPerPartition;
                });
                break;

            case StorageType.FileLog:
            case StorageType.Hybrid:
                // TODO: 添加 FileLog 存储
                services.AddMemoryMessageStorage();
                break;
        }

        // 添加核心服务
        services.AddMessageQueueCore();

        // 添加协议层
        switch (options.Mode)
        {
            case MessageQueueMode.InProc:
                services.AddInProcMessageProtocol();
                break;

            case MessageQueueMode.Broker:
                // TODO: 添加 Broker 客户端
                services.AddInProcMessageProtocol();
                break;

            case MessageQueueMode.Brokerless:
                // TODO: 添加 Brokerless 协议
                services.AddInProcMessageProtocol();
                break;
        }

        return services;
    }

    /// <summary>
    /// 添加消息队列服务（从配置读取）
    /// </summary>
    public static IServiceCollection AddMessageQueue(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new MessageQueueOptions();
        configuration.GetSection("MessageQueue").Bind(options);

        return services.AddMessageQueue(opts =>
        {
            opts.Mode = options.Mode;
            opts.Protocol = options.Protocol;
            opts.Storage = options.Storage;
            opts.BrokerEndpoints = options.BrokerEndpoints;
            opts.EnableTransactional = options.EnableTransactional;
            opts.EnableTracing = options.EnableTracing;
            opts.EnableDelayMessage = options.EnableDelayMessage;
            opts.MaxMessagesPerPartition = options.MaxMessagesPerPartition;
            opts.DataDirectory = options.DataDirectory;
        });
    }
}
