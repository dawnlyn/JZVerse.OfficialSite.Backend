using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.MessageQueue.Protocol.InProc;

/// <summary>
/// 进程内协议服务扩展
/// </summary>
public static class InProcProtocolExtensions
{
    /// <summary>
    /// 添加进程内消息协议
    /// </summary>
    public static IServiceCollection AddInProcMessageProtocol(this IServiceCollection services)
    {
        services.TryAddTransient<IMessageProducer, InProcMessageProducer>();
        services.TryAddSingleton<InProcMessageConsumerFactory>();

        return services;
    }
}
