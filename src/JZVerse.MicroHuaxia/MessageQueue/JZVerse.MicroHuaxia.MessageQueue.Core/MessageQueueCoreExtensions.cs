using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Delay;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Routing;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Tracing;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Transaction;
using JZVerse.MicroHuaxia.MessageQueue.Core.Consumers;
using JZVerse.MicroHuaxia.MessageQueue.Core.Delay;
using JZVerse.MicroHuaxia.MessageQueue.Core.Producers;
using JZVerse.MicroHuaxia.MessageQueue.Core.Routing;
using JZVerse.MicroHuaxia.MessageQueue.Core.Subscription;
using JZVerse.MicroHuaxia.MessageQueue.Core.Tracing;
using JZVerse.MicroHuaxia.MessageQueue.Core.Transaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.MessageQueue.Core;

/// <summary>
/// Core 层服务扩展
/// </summary>
public static class MessageQueueCoreExtensions
{
    /// <summary>
    /// 添加消息队列核心服务
    /// </summary>
    public static IServiceCollection AddMessageQueueCore(this IServiceCollection services)
    {
        // 订阅管理
        services.TryAddSingleton<ISubscriptionManager, SubscriptionManager>();

        // 路由引擎
        services.TryAddSingleton<IRoutingEngine, RoutingEngine>();

        // 交换机和队列管理
        services.TryAddSingleton<IExchangeManager, ExchangeManager>();
        services.TryAddSingleton<IQueueManager, QueueManager>();

        // 生产者和消费者
        services.TryAddTransient<IMessageProducer, MessageProducer>();
        services.TryAddTransient<IMessageConsumer, MessageConsumer>();

        return services;
    }
    
    /// <summary>
    /// 添加事务消息支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置回查选项</param>
    public static IServiceCollection AddTransactionSupport(
        this IServiceCollection services,
        Action<TransactionCheckOptions>? configureOptions = null)
    {
        // 事务存储
        services.TryAddSingleton<ITransactionStore, MemoryTransactionStore>();
        
        // 事务生产者
        services.TryAddTransient<ITransactionProducer, TransactionProducer>();
        
        // 配置回查选项
        var options = new TransactionCheckOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);
        
        // 事务回查后台服务
        if (options.Enabled)
        {
            services.AddHostedService<TransactionCheckService>();
        }
        
        return services;
    }
    
    /// <summary>
    /// 注册事务回查监听器
    /// </summary>
    /// <typeparam name="TListener">监听器类型</typeparam>
    public static IServiceCollection AddTransactionCheckListener<TListener>(this IServiceCollection services)
        where TListener : class, ITransactionCheckListener
    {
        services.AddSingleton<ITransactionCheckListener, TListener>();
        return services;
    }
    
    /// <summary>
    /// 添加延迟消息支持（时间轮算法）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    public static IServiceCollection AddDelayMessageSupport(
        this IServiceCollection services,
        Action<DelaySchedulerOptions>? configureOptions = null)
    {
        // 配置选项
        var options = new DelaySchedulerOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton(options.TimeWheel);
        
        // 延迟消息存储
        services.TryAddSingleton<IDelayMessageStore, MemoryDelayMessageStore>();
        
        // 延迟消息调度器
        services.TryAddSingleton<IDelayMessageScheduler, TimeWheelDelayScheduler>();
        
        // 后台服务
        if (options.Enabled)
        {
            services.AddHostedService<DelayMessageService>();
        }
        
        return services;
    }
    
    /// <summary>
    /// 添加消息追踪支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    public static IServiceCollection AddTracingSupport(
        this IServiceCollection services,
        Action<MessageTracerOptions>? configureOptions = null)
    {
        // 配置选项
        var tracerOptions = new MessageTracerOptions();
        configureOptions?.Invoke(tracerOptions);
        services.AddSingleton(tracerOptions);
        
        // 追踪存储配置
        services.TryAddSingleton<MessageTraceStoreOptions>();
        
        // 追踪存储
        services.TryAddSingleton<IMessageTraceStore, MemoryMessageTraceStore>();
        
        // 追踪服务
        services.TryAddSingleton<IMessageTracer, MessageTracer>();
        services.TryAddSingleton<MessageTracer>();
        
        // 追踪拦截器
        services.TryAddSingleton<TracingConsumeInterceptor>();
        
        return services;
    }
    
    /// <summary>
    /// 添加追踪消息存储装饰器
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddTracingMessageStoreDecorator(this IServiceCollection services)
    {
        // 手动装饰器模式：包装现有的 IMessageStore
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMessageStore));
        if (descriptor != null)
        {
            services.Remove(descriptor);
            
            services.AddSingleton<IMessageStore>(sp =>
            {
                // 创建原始存储实例
                IMessageStore innerStore;
                if (descriptor.ImplementationInstance != null)
                {
                    innerStore = (IMessageStore)descriptor.ImplementationInstance;
                }
                else if (descriptor.ImplementationFactory != null)
                {
                    innerStore = (IMessageStore)descriptor.ImplementationFactory(sp);
                }
                else if (descriptor.ImplementationType != null)
                {
                    innerStore = (IMessageStore)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
                }
                else
                {
                    throw new InvalidOperationException("Cannot resolve IMessageStore implementation");
                }
                
                // 创建装饰器
                var tracer = sp.GetRequiredService<MessageTracer>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TracingMessageStoreDecorator>>();
                return new TracingMessageStoreDecorator(innerStore, tracer, logger);
            });
        }
        
        return services;
    }
}
