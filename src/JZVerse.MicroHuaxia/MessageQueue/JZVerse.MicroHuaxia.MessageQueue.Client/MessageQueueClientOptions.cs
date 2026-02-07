namespace JZVerse.MicroHuaxia.MessageQueue.Client;

/// <summary>
/// 消息队列客户端配置选项
/// </summary>
public sealed class MessageQueueClientOptions
{
    /// <summary>服务器地址</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>服务器端口</summary>
    public int Port { get; set; } = 9527;

    /// <summary>客户端ID</summary>
    public string? ClientId { get; set; }

    /// <summary>消费者组</summary>
    public string? ConsumerGroup { get; set; }

    /// <summary>连接超时 (毫秒)</summary>
    public int ConnectTimeout { get; set; } = 10000;

    /// <summary>请求超时 (毫秒)</summary>
    public int RequestTimeout { get; set; } = 30000;

    /// <summary>心跳间隔 (秒)</summary>
    public int HeartbeatInterval { get; set; } = 30;

    /// <summary>自动重连</summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>重连间隔 (毫秒)</summary>
    public int ReconnectInterval { get; set; } = 5000;

    /// <summary>最大重连次数 (0 = 无限)</summary>
    public int MaxReconnectAttempts { get; set; } = 0;

    /// <summary>启用消息压缩</summary>
    public bool EnableCompression { get; set; } = false;
}
