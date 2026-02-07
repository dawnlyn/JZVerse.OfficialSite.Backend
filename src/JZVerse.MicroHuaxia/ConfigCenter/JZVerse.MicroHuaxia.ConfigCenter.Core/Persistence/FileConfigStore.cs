using System.Text.Json;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ConfigCenter.Core.Persistence;

/// <summary>
/// 文件配置持久化存储实现
/// </summary>
public class FileConfigStore(string basePath, ILogger<FileConfigStore> _logger) : IConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public async Task SaveSnapshotAsync(
        string namespaceId,
        string environmentId,
        Dictionary<string, string> config,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(namespaceId, environmentId);
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var snapshot = new ConfigSnapshot
        {
            NamespaceId = namespaceId,
            EnvironmentId = environmentId,
            Timestamp = DateTimeOffset.UtcNow,
            Configs = config,
        };

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation(
            "Saved config snapshot for namespace {NamespaceId} env {EnvironmentId} to {FilePath}",
            namespaceId, environmentId, filePath);
    }

    public async Task<Dictionary<string, string>> LoadSnapshotAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(namespaceId, environmentId);

        if (!File.Exists(filePath))
        {
            _logger.LogDebug(
                "No snapshot file found for namespace {NamespaceId} env {EnvironmentId}",
                namespaceId, environmentId);
            return new Dictionary<string, string>();
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var snapshot = JsonSerializer.Deserialize<ConfigSnapshot>(json);

        _logger.LogInformation(
            "Loaded config snapshot for namespace {NamespaceId} env {EnvironmentId} with {Count} items",
            namespaceId, environmentId, snapshot?.Configs?.Count ?? 0);

        return snapshot?.Configs ?? new Dictionary<string, string>();
    }

    public Task ClearSnapshotAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(namespaceId, environmentId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation(
                "Cleared config snapshot for namespace {NamespaceId} env {EnvironmentId}",
                namespaceId, environmentId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(namespaceId, environmentId);
        return Task.FromResult(File.Exists(filePath));
    }

    private string GetFilePath(string namespaceId, string environmentId)
        => Path.Combine(basePath, environmentId, $"{namespaceId}.json");

    private sealed class ConfigSnapshot
    {
        public string NamespaceId { get; init; } = string.Empty;
        public string EnvironmentId { get; init; } = string.Empty;
        public DateTimeOffset Timestamp { get; init; }
        public Dictionary<string, string> Configs { get; init; } = new();
    }
}
