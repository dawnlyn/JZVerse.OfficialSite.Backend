using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Choreography;
using JZVerse.MicroHuaxia.Saga.Core;
using JZVerse.MicroHuaxia.Saga.Core.BackgroundServices;
using JZVerse.MicroHuaxia.Saga.Core.Choreography;
using JZVerse.MicroHuaxia.Saga.Core.Communication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.Saga.AspNetCore;

/// <summary>
/// Saga ASP.NET Core 扩展方法
/// </summary>
public static class SagaExtensions
{
    /// <summary>
    /// 添加 Saga 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    public static IServiceCollection AddSaga(
        this IServiceCollection services,
        Action<SagaOptions>? configure = null)
    {
        var options = new SagaOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        
        // 核心服务
        services.AddSagaCore();
        
        // 存储层
        switch (options.Storage)
        {
            case SagaStorageType.Memory:
                services.AddMemorySagaStorage();
                break;
            case SagaStorageType.FileLog:
                // TODO: 添加文件日志存储实现
                services.AddMemorySagaStorage();
                break;
        }
        
        // 通信层
        switch (options.CommunicationMode)
        {
            case SagaCommunicationMode.Local:
                // 本地模式不需要通信适配器
                break;
            case SagaCommunicationMode.Http:
                services.AddHttpSagaCommunication(options.HttpOptions);
                break;
            case SagaCommunicationMode.Mq:
                services.AddMqSagaCommunication(options.MqOptions);
                break;
        }
        
        // 编排式基础设施（Choreography 模式）
        if (options.Mode == SagaMode.Choreography)
        {
            services.AddChoreographyInfrastructure(options.ChoreographyOptions);
        }
        
        // 后台服务
        if (options.EnableTimeoutDetection)
        {
            services.AddSingleton(options.TimeoutOptions);
            services.AddHostedService<SagaTimeoutService>();
        }
        
        if (options.EnableRecovery)
        {
            services.AddSingleton(options.RecoveryOptions);
            services.AddHostedService<SagaRecoveryService>();
        }
        
        return services;
    }
    
    /// <summary>
    /// 添加 HTTP Saga 通信
    /// </summary>
    public static IServiceCollection AddHttpSagaCommunication(
        this IServiceCollection services,
        HttpSagaAdapterOptions? options = null)
    {
        services.AddSingleton(options ?? new HttpSagaAdapterOptions());
        services.AddHttpClient<HttpSagaAdapter>();
        services.TryAddSingleton<ISagaCommunicationAdapter, HttpSagaAdapter>();
        return services;
    }
    
    /// <summary>
    /// 添加 MQ Saga 通信
    /// </summary>
    public static IServiceCollection AddMqSagaCommunication(
        this IServiceCollection services,
        MqSagaAdapterOptions? options = null)
    {
        services.AddSingleton(options ?? new MqSagaAdapterOptions());
        services.TryAddSingleton<ISagaCommunicationAdapter, MqSagaAdapter>();
        return services;
    }
    
    /// <summary>
    /// 注册 Saga 定义
    /// </summary>
    /// <typeparam name="TSagaDefinition">Saga 定义类型</typeparam>
    public static IServiceCollection AddSagaDefinition<TSagaDefinition>(this IServiceCollection services)
        where TSagaDefinition : class, ISagaDefinition
    {
        services.AddSingleton<ISagaDefinition, TSagaDefinition>();
        services.AddSingleton<TSagaDefinition>();
        return services;
    }
    
    /// <summary>
    /// 注册编排式 Saga 定义
    /// </summary>
    /// <typeparam name="TDefinition">编排式 Saga 定义类型</typeparam>
    public static IServiceCollection AddChoreographySagaDefinition<TDefinition>(this IServiceCollection services)
        where TDefinition : class, IChoreographySagaDefinition
    {
        services.AddSingleton<IChoreographySagaDefinition, TDefinition>();
        services.AddSingleton<TDefinition>();
        return services;
    }
    
    /// <summary>
    /// 注册 Saga 参与者
    /// </summary>
    /// <typeparam name="TParticipant">参与者类型</typeparam>
    public static IServiceCollection AddSagaParticipant<TParticipant>(this IServiceCollection services)
        where TParticipant : class, ISagaParticipant
    {
        services.AddSingleton<ISagaParticipant, TParticipant>();
        services.AddSingleton<TParticipant>();
        return services;
    }
    
    /// <summary>
    /// 添加编排式基础设施
    /// </summary>
    private static IServiceCollection AddChoreographyInfrastructure(
        this IServiceCollection services,
        ChoreographyOptions options)
    {
        // 事件总线
        switch (options.EventBusType)
        {
            case ChoreographyEventBusType.InMemory:
                services.TryAddSingleton<InMemorySagaEventBus>();
                services.TryAddSingleton<ISagaEventPublisher>(sp => sp.GetRequiredService<InMemorySagaEventBus>());
                break;
            case ChoreographyEventBusType.MessageQueue:
                // TODO: 添加 MQ 事件总线实现
                services.TryAddSingleton<InMemorySagaEventBus>();
                services.TryAddSingleton<ISagaEventPublisher>(sp => sp.GetRequiredService<InMemorySagaEventBus>());
                break;
        }
        
        // 编排式协调器（可选）
        if (options.EnableCoordinator)
        {
            services.TryAddSingleton<ChoreographySagaCoordinator>();
        }
        
        return services;
    }
}
