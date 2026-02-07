namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions.Protocol;

/// <summary>
/// 消息协议类型
/// </summary>
public enum ProtocolType
{
    /// <summary>
    /// 进程内协议
    /// </summary>
    InProc = 0,

    /// <summary>
    /// TCP 自定义协议
    /// </summary>
    Tcp = 1,

    /// <summary>
    /// gRPC 协议
    /// </summary>
    Grpc = 2,

    /// <summary>
    /// HTTP 协议
    /// </summary>
    Http = 3
}

/// <summary>
/// 消息帧类型
/// </summary>
public enum MessageFrameType : byte
{
    /// <summary>
    /// 发布消息
    /// </summary>
    Publish = 0x01,

    /// <summary>
    /// 订阅
    /// </summary>
    Subscribe = 0x02,

    /// <summary>
    /// 取消订阅
    /// </summary>
    Unsubscribe = 0x03,

    /// <summary>
    /// 拉取消息
    /// </summary>
    Pull = 0x04,

    /// <summary>
    /// 消息投递
    /// </summary>
    Deliver = 0x05,

    /// <summary>
    /// 确认
    /// </summary>
    Ack = 0x06,

    /// <summary>
    /// 否认
    /// </summary>
    Nack = 0x07,

    /// <summary>
    /// 心跳
    /// </summary>
    Heartbeat = 0x08,

    /// <summary>
    /// 心跳响应
    /// </summary>
    HeartbeatAck = 0x09,

    /// <summary>
    /// 事务准备
    /// </summary>
    TransactionPrepare = 0x10,

    /// <summary>
    /// 事务提交
    /// </summary>
    TransactionCommit = 0x11,

    /// <summary>
    /// 事务回滚
    /// </summary>
    TransactionRollback = 0x12,

    /// <summary>
    /// 事务回查
    /// </summary>
    TransactionCheck = 0x13,

    /// <summary>
    /// 响应
    /// </summary>
    Response = 0xF0,

    /// <summary>
    /// 错误
    /// </summary>
    Error = 0xFF
}

/// <summary>
/// 协议连接接口
/// </summary>
public interface IProtocolConnection : IAsyncDisposable
{
    /// <summary>
    /// 连接ID
    /// </summary>
    string ConnectionId { get; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 远程端点
    /// </summary>
    string RemoteEndpoint { get; }

    /// <summary>
    /// 连接时间
    /// </summary>
    DateTimeOffset ConnectedAt { get; }

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="data">数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// 接收数据
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 关闭连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task CloseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 协议客户端接口
/// </summary>
public interface IProtocolClient : IAsyncDisposable
{
    /// <summary>
    /// 协议类型
    /// </summary>
    ProtocolType ProtocolType { get; }

    /// <summary>
    /// 连接到服务器
    /// </summary>
    /// <param name="endpoint">服务器地址</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IProtocolConnection> ConnectAsync(string endpoint, CancellationToken cancellationToken = default);
}

/// <summary>
/// 协议服务器接口
/// </summary>
public interface IProtocolServer : IAsyncDisposable
{
    /// <summary>
    /// 协议类型
    /// </summary>
    ProtocolType ProtocolType { get; }

    /// <summary>
    /// 是否正在运行
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 启动服务器
    /// </summary>
    /// <param name="endpoint">监听地址</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止服务器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 接受连接事件
    /// </summary>
    event Func<IProtocolConnection, Task>? OnConnectionAccepted;
}

/// <summary>
/// 消息编解码器接口
/// </summary>
public interface IMessageCodec
{
    /// <summary>
    /// 编码消息
    /// </summary>
    /// <param name="frameType">帧类型</param>
    /// <param name="payload">负载</param>
    /// <returns>编码后的字节</returns>
    byte[] Encode(MessageFrameType frameType, byte[] payload);

    /// <summary>
    /// 解码消息
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <returns>帧类型和负载</returns>
    (MessageFrameType FrameType, byte[] Payload) Decode(ReadOnlySpan<byte> data);
}
