namespace JZVerse.MicroHuaxia.ServiceCommunication.Abstractions;

/// <summary>
/// 服务客户端接口 - 统一的服务调用抽象
/// </summary>
public interface IServiceClient
{
    /// <summary>
    /// 发送请求
    /// </summary>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="serviceName">目标服务名称</param>
    /// <param name="request">请求对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TResponse> SendAsync<TResponse>(
        string serviceName,
        ServiceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送请求（无返回值）
    /// </summary>
    Task SendAsync(
        string serviceName,
        ServiceRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 泛型服务客户端接口 - 类型化服务调用
/// </summary>
/// <typeparam name="TService">服务接口类型</typeparam>
public interface IServiceClient<TService> where TService : class
{
    /// <summary>
    /// 服务代理实例
    /// </summary>
    TService Service { get; }
}
