using System.Net.Http.Json;
using System.Text.Json;
using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Saga.Core.Communication;

/// <summary>
/// HTTP Saga 通信适配器配置
/// </summary>
public sealed class HttpSagaAdapterOptions
{
    /// <summary>
    /// 服务端点映射（服务名 -> 基础 URL）
    /// </summary>
    public Dictionary<string, string> ServiceEndpoints { get; set; } = new();
    
    /// <summary>
    /// 默认超时时间
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// 执行步骤的路径模板（支持 {stepId}）
    /// </summary>
    public string ExecutePathTemplate { get; set; } = "/saga/steps/{stepId}/execute";
    
    /// <summary>
    /// 补偿步骤的路径模板（支持 {stepId}）
    /// </summary>
    public string CompensatePathTemplate { get; set; } = "/saga/steps/{stepId}/compensate";
}

/// <summary>
/// HTTP Saga 通信适配器
/// </summary>
public sealed class HttpSagaAdapter : ISagaCommunicationAdapter
{
    private readonly ILogger<HttpSagaAdapter> _logger;
    private readonly HttpClient _httpClient;
    private readonly HttpSagaAdapterOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public HttpSagaAdapter(
        ILogger<HttpSagaAdapter> logger,
        HttpClient httpClient,
        HttpSagaAdapterOptions? options = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _options = options ?? new HttpSagaAdapterOptions();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<StepExecutionResult> ExecuteStepAsync(
        string serviceName,
        StepExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetServiceUrl(serviceName);
        var path = _options.ExecutePathTemplate.Replace("{stepId}", request.StepId);
        var url = $"{baseUrl.TrimEnd('/')}{path}";
        
        _logger.LogDebug("Executing step {StepId} via HTTP: {Url}", request.StepId, url);
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(request.Timeout ?? _options.DefaultTimeout);
            
            var response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StepExecutionResult>(_jsonOptions, cts.Token);
                return result ?? StepExecutionResult.Failed("Empty response");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
            _logger.LogWarning("Step execution failed with status {StatusCode}: {Error}", 
                response.StatusCode, errorContent);
            
            return StepExecutionResult.Failed(
                $"HTTP {(int)response.StatusCode}: {errorContent}", 
                retryable: (int)response.StatusCode >= 500);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StepExecutionResult.Failed("Request timed out", retryable: true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for step {StepId}", request.StepId);
            return StepExecutionResult.Failed(ex.Message, retryable: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing step {StepId}", request.StepId);
            return StepExecutionResult.Failed(ex.Message, retryable: false);
        }
    }

    public async Task<CompensationResult> CompensateStepAsync(
        string serviceName,
        CompensationRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetServiceUrl(serviceName);
        var path = _options.CompensatePathTemplate.Replace("{stepId}", request.StepId);
        var url = $"{baseUrl.TrimEnd('/')}{path}";
        
        _logger.LogDebug("Compensating step {StepId} via HTTP: {Url}", request.StepId, url);
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.DefaultTimeout);
            
            var response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CompensationResult>(_jsonOptions, cts.Token);
                return result ?? CompensationResult.Failed("Empty response");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
            _logger.LogWarning("Step compensation failed with status {StatusCode}: {Error}", 
                response.StatusCode, errorContent);
            
            return CompensationResult.Failed(
                $"HTTP {(int)response.StatusCode}: {errorContent}",
                retryable: (int)response.StatusCode >= 500);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CompensationResult.Failed("Request timed out", retryable: true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for step {StepId} compensation", request.StepId);
            return CompensationResult.Failed(ex.Message, retryable: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error compensating step {StepId}", request.StepId);
            return CompensationResult.Failed(ex.Message, retryable: false);
        }
    }

    private string GetServiceUrl(string serviceName)
    {
        if (_options.ServiceEndpoints.TryGetValue(serviceName, out var url))
        {
            return url;
        }
        
        throw new InvalidOperationException($"No endpoint configured for service '{serviceName}'");
    }
}
