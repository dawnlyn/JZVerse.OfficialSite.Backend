using System.Net.Http.Json;
using System.Text.Json;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Http;

/// <summary>
/// HttpClient 扩展方法 - 支持基于服务名的调用
/// </summary>
public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 发送 GET 请求
    /// </summary>
    public static async Task<T?> GetFromServiceAsync<T>(
        this HttpClient client,
        string path,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    /// <summary>
    /// 发送 POST 请求
    /// </summary>
    public static async Task<TResponse?> PostToServiceAsync<TRequest, TResponse>(
        this HttpClient client,
        string path,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(path, request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>
    /// 发送 POST 请求（无返回值）
    /// </summary>
    public static async Task PostToServiceAsync<TRequest>(
        this HttpClient client,
        string path,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(path, request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// 发送 PUT 请求
    /// </summary>
    public static async Task<TResponse?> PutToServiceAsync<TRequest, TResponse>(
        this HttpClient client,
        string path,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PutAsJsonAsync(path, request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>
    /// 发送 DELETE 请求
    /// </summary>
    public static async Task<bool> DeleteFromServiceAsync(
        this HttpClient client,
        string path,
        CancellationToken cancellationToken = default)
    {
        var response = await client.DeleteAsync(path, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
