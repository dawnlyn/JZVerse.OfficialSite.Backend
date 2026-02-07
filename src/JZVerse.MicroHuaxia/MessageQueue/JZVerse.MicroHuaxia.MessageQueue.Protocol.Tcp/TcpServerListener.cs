using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Protocol.Tcp;

/// <summary>
/// TCP 服务端监听器
/// </summary>
public sealed class TcpServerListener : IAsyncDisposable
{
    private readonly ILogger<TcpServerListener> _logger;
    private readonly TcpFrameCodec _codec = new();
    private readonly ConcurrentDictionary<string, TcpServerSession> _sessions = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private bool _disposed;

    /// <summary>帧处理回调</summary>
    public Func<TcpServerSession, TcpFrame, Task<TcpFrame?>>? OnFrameReceived { get; set; }

    /// <summary>连接回调</summary>
    public Func<TcpServerSession, ConnectRequest, Task<ConnectResponse>>? OnClientConnected { get; set; }

    /// <summary>断开连接回调</summary>
    public Func<TcpServerSession, Task>? OnClientDisconnected { get; set; }

    /// <summary>当前活跃连接数</summary>
    public int ActiveConnections => _sessions.Count;

    public TcpServerListener(ILogger<TcpServerListener> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 启动监听
    /// </summary>
    public void Start(IPAddress address, int port)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TcpServerListener));
        }

        _listener = new TcpListener(address, port);
        _listener.Start();

        _cts = new CancellationTokenSource();
        _acceptTask = AcceptLoopAsync(_cts.Token);

        _logger.LogInformation("TCP Server started on {Address}:{Port}", address, port);
    }

    /// <summary>
    /// 停止监听
    /// </summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();

        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }

        // 关闭所有会话
        foreach (var session in _sessions.Values)
        {
            await session.CloseAsync();
        }
        _sessions.Clear();

        _logger.LogInformation("TCP Server stopped");
    }

    /// <summary>
    /// 向指定会话推送消息
    /// </summary>
    public async Task PushToSessionAsync(string sessionId, TcpFrame frame)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            await session.SendFrameAsync(frame);
        }
    }

    /// <summary>
    /// 广播消息到所有会话
    /// </summary>
    public async Task BroadcastAsync(TcpFrame frame)
    {
        var tasks = _sessions.Values.Select(s => s.SendFrameAsync(frame));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 获取所有会话ID
    /// </summary>
    public IEnumerable<string> GetSessionIds() => _sessions.Keys;

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting client connection");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var session = new TcpServerSession(sessionId, client, _codec, _logger);
        
        try
        {
            _logger.LogInformation("Client connected: {SessionId} from {RemoteEndPoint}", 
                sessionId, client.Client.RemoteEndPoint);

            // 等待连接请求
            var firstFrame = await session.ReceiveFrameAsync(cancellationToken);
            if (firstFrame?.Header.FrameType != TcpFrameType.Connect)
            {
                _logger.LogWarning("Expected Connect frame, got {FrameType}", firstFrame?.Header.FrameType);
                await session.CloseAsync();
                return;
            }

            // 处理连接请求
            var connectRequest = TcpPayloadSerializer.DeserializeConnectRequest(firstFrame.Body);
            ConnectResponse response;

            if (OnClientConnected != null && connectRequest != null)
            {
                response = await OnClientConnected(session, connectRequest);
            }
            else
            {
                response = new ConnectResponse
                {
                    Success = true,
                    SessionId = sessionId,
                    ServerVersion = TcpFrameHeader.CurrentVersion
                };
            }

            response.SessionId = sessionId;

            // 发送连接响应
            var responseFrame = new TcpFrame(
                TcpFrameType.ConnectAck,
                TcpPayloadSerializer.SerializeConnectResponse(response),
                firstFrame.Header.RequestId);
            await session.SendFrameAsync(responseFrame);

            if (!response.Success)
            {
                await session.CloseAsync();
                return;
            }

            // 添加到会话列表
            _sessions[sessionId] = session;

            // 开始接收循环
            await ReceiveLoopAsync(session, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client {SessionId}", sessionId);
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);

            if (OnClientDisconnected != null)
            {
                await OnClientDisconnected(session);
            }

            await session.CloseAsync();
            _logger.LogInformation("Client disconnected: {SessionId}", sessionId);
        }
    }

    private async Task ReceiveLoopAsync(TcpServerSession session, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && session.IsConnected)
        {
            var frame = await session.ReceiveFrameAsync(cancellationToken);
            if (frame == null)
            {
                break;
            }

            // 处理帧
            TcpFrame? responseFrame = null;

            switch (frame.Header.FrameType)
            {
                case TcpFrameType.Disconnect:
                    return;

                case TcpFrameType.Heartbeat:
                    responseFrame = new TcpFrame(TcpFrameType.HeartbeatAck, [], frame.Header.RequestId);
                    break;

                default:
                    if (OnFrameReceived != null)
                    {
                        responseFrame = await OnFrameReceived(session, frame);
                    }
                    break;
            }

            // 发送响应
            if (responseFrame != null)
            {
                await session.SendFrameAsync(responseFrame);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync();
        _cts?.Dispose();
    }
}

/// <summary>
/// TCP 服务端会话
/// </summary>
public sealed class TcpServerSession
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private readonly TcpFrameCodec _codec;
    private readonly ILogger _logger;
    private readonly Lock _writeLock = new();

    /// <summary>会话ID</summary>
    public string SessionId { get; }

    /// <summary>是否已连接</summary>
    public bool IsConnected => _client.Connected;

    /// <summary>远程端点</summary>
    public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

    /// <summary>用户数据</summary>
    public Dictionary<string, object> UserData { get; } = new();

    internal TcpServerSession(string sessionId, TcpClient client, TcpFrameCodec codec, ILogger logger)
    {
        SessionId = sessionId;
        _client = client;
        _codec = codec;
        _logger = logger;
        _stream = client.GetStream();
        _reader = PipeReader.Create(_stream);
        _writer = PipeWriter.Create(_stream);
    }

    /// <summary>
    /// 接收帧
    /// </summary>
    public async Task<TcpFrame?> ReceiveFrameAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await _reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (_codec.TryDecode(ref buffer, out var frame))
                {
                    _reader.AdvanceTo(buffer.Start, buffer.End);
                    return frame;
                }

                _reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving frame for session {SessionId}", SessionId);
        }

        return null;
    }

    /// <summary>
    /// 发送帧
    /// </summary>
    public async Task SendFrameAsync(TcpFrame frame)
    {
        var data = _codec.Encode(frame);
        
        lock (_writeLock)
        {
            _writer.WriteAsync(data).AsTask().Wait();
            _writer.FlushAsync().AsTask().Wait();
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// 关闭会话
    /// </summary>
    internal async Task CloseAsync()
    {
        await _reader.CompleteAsync();
        await _writer.CompleteAsync();
        _stream.Close();
        _client.Close();
    }
}
