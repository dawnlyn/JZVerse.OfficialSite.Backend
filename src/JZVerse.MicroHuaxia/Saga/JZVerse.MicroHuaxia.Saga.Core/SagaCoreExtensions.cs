using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Core.Compensation;
using JZVerse.MicroHuaxia.Saga.Core.Orchestration;
using JZVerse.MicroHuaxia.Saga.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.Saga.Core;

/// <summary>
/// Saga 核心服务扩展方法
/// </summary>
public static class SagaCoreExtensions
{
    /// <summary>
    /// 添加 Saga 核心服务
    /// </summary>
    public static IServiceCollection AddSagaCore(this IServiceCollection services)
    {
        // 状态机
        services.TryAddSingleton<SagaStateMachine>();
        
        // 补偿引擎
        services.TryAddSingleton<CompensationEngine>();
        
        // 协调器
        services.TryAddSingleton<ISagaOrchestrator, SagaOrchestrator>();
        
        return services;
    }
    
    /// <summary>
    /// 添加内存 Saga 存储
    /// </summary>
    public static IServiceCollection AddMemorySagaStorage(this IServiceCollection services)
    {
        services.TryAddSingleton<ISagaStore, MemorySagaStore>();
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
}
