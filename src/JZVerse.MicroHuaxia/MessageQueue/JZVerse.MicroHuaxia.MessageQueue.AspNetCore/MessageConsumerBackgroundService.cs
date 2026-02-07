using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.AspNetCore;

/// <summary>
/// 消息消费者后台服务基类
/// </summary>
public abstract class MessageConsumerBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    protected MessageConsumerBackgroundService(
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 获取要订阅的主题
    /// </summary>
    protected abstract string Topic { get; }

    /// <summary>
    /// 获取消费者组
    /// </summary>
    protected virtual string? ConsumerGroup => null;

    /// <summary>
    /// 获取消费选项
    /// </summary>
    protected virtual ConsumeOptions GetConsumeOptions() => new()
    {
        ConsumerGroup = ConsumerGroup,
        Mode = ConsumeMode.Push,
        AutoAck = false,
        MaxRetries = 3
    };

    /// <summary>
    /// 处理消息
    /// </summary>
    protected abstract Task<ConsumeResult> HandleMessageAsync(IMessageEnvelope envelope, CancellationToken cancellationToken);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message consumer starting for topic {Topic}", Topic);

        using var scope = _serviceProvider.CreateScope();
        var consumer = scope.ServiceProvider.GetRequiredService<IMessageConsumer>();

        try
        {
            await consumer.SubscribeAsync(
                Topic,
                HandleMessageAsync,
                GetConsumeOptions(),
                stoppingToken);

            _logger.LogInformation("Message consumer subscribed to topic {Topic}", Topic);

            // 保持运行直到取消
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message consumer stopping for topic {Topic}", Topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message consumer error for topic {Topic}", Topic);
            throw;
        }
        finally
        {
            await consumer.UnsubscribeAsync(Topic, CancellationToken.None);
        }
    }
}

/// <summary>
/// 消费者选项
/// </summary>
public sealed class ConsumerOptions
{
    /// <summary>
    /// 主题
    /// </summary>
    public required string Topic { get; set; }

    /// <summary>
    /// 消费者组
    /// </summary>
    public string? ConsumerGroup { get; set; }

    /// <summary>
    /// 消费模式
    /// </summary>
    public ConsumeMode ConsumeMode { get; set; } = ConsumeMode.Push;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 并发数
    /// </summary>
    public int Concurrency { get; set; } = 1;
}

/// <summary>
/// 消费者注册扩展
/// </summary>
public static class ConsumerExtensions
{
    /// <summary>
    /// 添加消息消费者
    /// </summary>
    public static IServiceCollection AddMessageQueueConsumer<TConsumer>(
        this IServiceCollection services,
        Action<ConsumerOptions>? configure = null)
        where TConsumer : MessageConsumerBackgroundService
    {
        var options = new ConsumerOptions { Topic = typeof(TConsumer).Name };
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddHostedService<TConsumer>();

        return services;
    }
}
