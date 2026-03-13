using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;

/// <summary>
/// 配置迁移服务接口
/// </summary>
public interface IConfigMigrationService
{
    /// <summary>
    /// 获取所有可迁移的服务配置文件
    /// </summary>
    List<ConfigMigrationProfile> GetProfiles();

    /// <summary>
    /// 预览某个服务的可迁移配置项（区分 bootstrap / 非 bootstrap）
    /// </summary>
    Task<List<ConfigMigrationPreviewItem>> PreviewAsync(string serviceName, CancellationToken ct = default);

    /// <summary>
    /// 执行迁移：将非 bootstrap 配置推送到 ConfigCenter
    /// </summary>
    Task<ConfigMigrationResult> MigrateAsync(string serviceName, CancellationToken ct = default);
}
