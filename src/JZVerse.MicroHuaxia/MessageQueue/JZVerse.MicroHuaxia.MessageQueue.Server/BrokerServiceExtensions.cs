using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Core;
using JZVerse.MicroHuaxia.MessageQueue.Core.Transaction;
using JZVerse.MicroHuaxia.MessageQueue.Protocol.Tcp;
using JZVerse.MicroHuaxia.MessageQueue.Storage.FileLog;
using JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.MessageQueue.Server;

/// <summary>
/// Broker 服务扩展
/// </summary>
public static class BrokerServiceExtensions
{
    /// <summary>
    /// 添加消息队列 Broker 服务
    /// </summary>
    public static IServiceCollection AddMessageQueueBroker(
        this IServiceCollection services,
        Action<BrokerOptions>? configure = null)
    {
        var options = new BrokerOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // 添加核心服务
        services.AddMessageQueueCore();

        // 根据配置选择存储
        switch (options.StorageType)
        {
            case StorageType.Memory:
                services.AddMemoryMessageStorage();
                break;

            case StorageType.FileLog:
                services.AddFileLogMessageStorage(opt =>
                {
                    opt.DataDirectory = options.DataDirectory;
                    opt.MessageRetention = TimeSpan.FromHours(options.MessageRetentionHours);
                });
                break;

            case StorageType.Hybrid:
                // 混合模式：先添加内存，FileLog 作为持久化层
                services.AddMemoryMessageStorage();
                // TODO: 实现混合存储适配器
                break;
        }

        // 添加 TCP 协议服务端
        services.AddTcpProtocolServer();

        // 添加 Broker 服务
        services.TryAddSingleton<BrokerServer>();

        // 注册为 HostedService
        services.AddHostedService(sp => sp.GetRequiredService<BrokerServer>());

        return services;
    }

    /// <summary>
    /// 添加消息队列 Broker 服务（使用配置节）
    /// </summary>
    public static IServiceCollection AddMessageQueueBroker(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var options = new BrokerOptions();
        configuration.Bind(options);

        return services.AddMessageQueueBroker(opt =>
        {
            opt.Host = options.Host;
            opt.Port = options.Port;
            opt.StorageType = options.StorageType;
            opt.DataDirectory = options.DataDirectory;
            opt.MaxConnections = options.MaxConnections;
            opt.HeartbeatTimeout = options.HeartbeatTimeout;
            opt.MessageRetentionHours = options.MessageRetentionHours;
            opt.DefaultPartitions = options.DefaultPartitions;
            opt.EnableTracing = options.EnableTracing;
            opt.EnableCompression = options.EnableCompression;
        });
    }

    /// <summary>
    /// 启用 Broker 事务消息支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">事务配置选项</param>
    public static IServiceCollection AddBrokerTransactionSupport(
        this IServiceCollection services,
        Action<TransactionCheckOptions>? configureOptions = null)
    {
        // 添加核心层事务支持
        services.AddTransactionSupport(configureOptions);
        
        return services;
    }
}
