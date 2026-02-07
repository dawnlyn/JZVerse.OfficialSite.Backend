using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Protocol.Tcp;

/// <summary>
/// TCP 客户端连接器
/// </summary>
public sealed class TcpClientConnection : IAsyncDisposable
{
    private readonly ILogger<TcpClientConnection> _logger;
    private readonly TcpFrameCodec _codec = new();
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<TcpFrame>> _pendingRequests = new();
    
    private TcpClient? _client;
    private NetworkStream? _stream;
    private PipeReader? _reader;
    private PipeWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    
    private uint _requestIdCounter;
    private bool _disposed;

    /// <summary>是否已连接</summary>
    public bool IsConnected => _client?.Connected == true;

    /// <summary>会话ID</summary>
    public string? SessionId { get; private set; }

    /// <summary>消息推送回调</summary>
    public Func<TcpFrame, Task>? OnMessagePushed { get; set; }

    public TcpClientConnection(ILogger<TcpClientConnection> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 连接到服务器
    /// </summary>
    public async Task<ConnectResponse> ConnectAsync(
        string host, 
        int port, 
        ConnectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TcpClientConnection));
        }

        _client = new TcpClient();
        await _client.ConnectAsync(host, port, cancellationToken);

        _stream = _client.GetStream();
        var pipe = new Pipe();
        _reader = PipeReader.Create(_stream);
        _writer = PipeWriter.Create(_stream);

        _cts = new CancellationTokenSource();

        // 启动接收循环
        _receiveTask = ReceiveLoopAsync(_cts.Token);

        // 发送连接请求
        var payload = TcpPayloadSerializer.SerializeConnectRequest(request);
        var response = await SendAndWaitAsync(TcpFrameType.Connect, payload, cancellationToken);

        var connectResponse = TcpPayloadSerializer.DeserializeConnectResponse(response.Body);
        if (connectResponse?.Success == true)
        {
            SessionId = connectResponse.SessionId;
            _logger.LogInformation("Connected to {Host}:{Port}, SessionId: {SessionId}", host, port, SessionId);
        }

        return connectResponse ?? new ConnectResponse { Success = false, ErrorMessage = "Invalid response" };
    }

    /// <summary>
    /// 发送帧并等待响应
    /// </summary>
    public async Task<TcpFrame> SendAndWaitAsync(
        TcpFrameType frameType, 
        byte[] payload, 
        CancellationToken cancellationToken = default,
        int timeoutMs = 30000)
    {
        var requestId = Interlocked.Increment(ref _requestIdCounter);
        var tcs = new TaskCompletionSource<TcpFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        _pendingRequests[requestId] = tcs;

        try
        {
            var frame = new TcpFrame(frameType, payload, requestId, TcpFrameFlags.RequiresAck);
            await SendFrameAsync(frame, cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            return await tcs.Task.WaitAsync(cts.Token);
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// 发送帧 (不等待响应)
    /// </summary>
    public async Task SendFrameAsync(TcpFrame frame, CancellationToken cancellationToken = default)
    {
        if (_writer == null)
        {
            throw new InvalidOperationException("Not connected");
        }

        var data = _codec.Encode(frame);
        await _writer.WriteAsync(data, cancellationToken);
        await _writer.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// 发送心跳
    /// </summary>
    public async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        var frame = new TcpFrame(TcpFrameType.Heartbeat, [], Interlocked.Increment(ref _requestIdCounter));
        await SendFrameAsync(frame, cancellationToken);
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_client?.Connected == true && _writer != null)
        {
            try
            {
                var frame = new TcpFrame(TcpFrameType.Disconnect, []);
                await SendFrameAsync(frame);
            }
            catch
            {
                // 忽略断开连接时的错误
            }
        }

        await CloseAsync();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _reader != null)
            {
                var result = await _reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                while (_codec.TryDecode(ref buffer, out var frame))
                {
                    if (frame != null)
                    {
                        await HandleFrameAsync(frame);
                    }
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
            _logger.LogError(ex, "Error in receive loop");
        }
    }

    private async Task HandleFrameAsync(TcpFrame frame)
    {
        // 检查是否是响应帧
        if (_pendingRequests.TryRemove(frame.Header.RequestId, out var tcs))
        {
            tcs.TrySetResult(frame);
            return;
        }

        // 处理服务器推送
        switch (frame.Header.FrameType)
        {
            case TcpFrameType.Push:
                if (OnMessagePushed != null)
                {
                    await OnMessagePushed(frame);
                }
                break;

            case TcpFrameType.HeartbeatAck:
                _logger.LogDebug("Heartbeat acknowledged");
                break;

            case TcpFrameType.Disconnect:
                _logger.LogInformation("Server requested disconnect");
                await CloseAsync();
                break;

            default:
                _logger.LogWarning("Unhandled frame type: {FrameType}", frame.Header.FrameType);
                break;
        }
    }

    private async Task CloseAsync()
    {
        _cts?.Cancel();

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch
            {
                // 忽略
            }
        }

        _reader?.Complete();
        _writer?.Complete();
        _stream?.Close();
        _client?.Close();

        // 取消所有等待中的请求
        foreach (var kv in _pendingRequests)
        {
            kv.Value.TrySetCanceled();
        }
        _pendingRequests.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await CloseAsync();

        _cts?.Dispose();
        _stream?.Dispose();
        _client?.Dispose();
    }
}
