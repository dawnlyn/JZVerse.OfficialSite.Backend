using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.MessageQueue.Storage.FileLog;

/// <summary>
/// FileLog 存储服务扩展
/// </summary>
public static class FileLogStorageExtensions
{
    /// <summary>
    /// 添加 FileLog 消息存储
    /// </summary>
    public static IServiceCollection AddFileLogMessageStorage(
        this IServiceCollection services,
        Action<FileLogStoreOptions>? configure = null)
    {
        var options = new FileLogStoreOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton<IMessageStore, FileLogMessageStore>();
        services.TryAddSingleton<IOffsetManager, FileLogOffsetManager>();

        return services;
    }
}
