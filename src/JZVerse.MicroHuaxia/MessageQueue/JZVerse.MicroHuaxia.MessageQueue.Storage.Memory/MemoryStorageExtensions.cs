using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;

/// <summary>
/// 内存存储服务扩展
/// </summary>
public static class MemoryStorageExtensions
{
    /// <summary>
    /// 添加内存消息存储
    /// </summary>
    public static IServiceCollection AddMemoryMessageStorage(
        this IServiceCollection services,
        Action<MemoryStoreOptions>? configure = null)
    {
        var options = new MemoryStoreOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton<IMessageStore, MemoryMessageStore>();
        services.TryAddSingleton<IOffsetManager, MemoryOffsetManager>();

        return services;
    }
}
