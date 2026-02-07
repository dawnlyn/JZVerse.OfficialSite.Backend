using JZVerse.MicroHuaxia.ConfigCenter.Client;
using Microsoft.Extensions.Configuration;

namespace JZVerse.MicroHuaxia.ConfigCenter.AspNetCore;

/// <summary>
/// 配置中心配置提供器
/// </summary>
public class ConfigCenterConfigurationProvider(
    IConfigCenterClient _client,
    string _namespaceId,
    string _environmentId
) : ConfigurationProvider
{
    /// <inheritdoc />
    public override void Load()
    {
        try
        {
            var config = _client.GetConfigAsync(_namespaceId, _environmentId)
                .GetAwaiter()
                .GetResult();

            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var (key, value) in config)
            {
                // 将配置键转换为 IConfiguration 格式（:分隔符）
                var configKey = key.Replace(".", ":");
                Data[configKey] = value;
            }
        }
        catch
        {
            // 加载失败时使用空配置
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 触发配置重新加载
    /// </summary>
    public void Reload()
    {
        Load();
        OnReload();
    }
}

/// <summary>
/// 配置中心配置源
/// </summary>
public class ConfigCenterConfigurationSource(
    IConfigCenterClient _client,
    string _namespaceId,
    string _environmentId
) : IConfigurationSource
{
    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new ConfigCenterConfigurationProvider(_client, _namespaceId, _environmentId);
    }
}

/// <summary>
/// 配置构建器扩展方法
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// 添加配置中心配置源
    /// </summary>
    public static IConfigurationBuilder AddConfigCenter(
        this IConfigurationBuilder builder,
        IConfigCenterClient client,
        string namespaceId,
        string environmentId)
    {
        builder.Add(new ConfigCenterConfigurationSource(client, namespaceId, environmentId));
        return builder;
    }
}
