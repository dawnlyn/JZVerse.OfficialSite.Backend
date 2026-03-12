using System.Text.Json;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Helpers;

/// <summary>
/// 前端框架检测器
/// </summary>
public static class FrontendFrameworkDetector
{
    /// <summary>
    /// 检测项目使用的前端框架
    /// </summary>
    /// <param name="projectPath">项目根目录路径</param>
    /// <returns>检测到的前端框架类型</returns>
    public static async Task<FrontendFramework> DetectAsync(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var packageJsonPath = Path.Combine(projectPath, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return FrontendFramework.Unknown;
        }

        try
        {
            var content = await File.ReadAllTextAsync(packageJsonPath);
            var packageJson = JsonDocument.Parse(content);
            var root = packageJson.RootElement;

            // 检查 dependencies 和 devDependencies
            var dependencies = GetDependencies(root, "dependencies");
            var devDependencies = GetDependencies(root, "devDependencies");
            var allDeps = dependencies.Concat(devDependencies).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 按优先级检测框架
            // Nuxt 优先于 Vue (Nuxt 基于 Vue)
            if (allDeps.Contains("nuxt") || allDeps.Contains("@nuxt/kit"))
            {
                return FrontendFramework.NuxtJs;
            }

            // Next.js 优先于 React (Next.js 基于 React)
            if (allDeps.Contains("next"))
            {
                return FrontendFramework.NextJs;
            }

            // Vite
            if (allDeps.Contains("vite"))
            {
                return FrontendFramework.Vite;
            }

            // Create React App
            if (allDeps.Contains("react-scripts"))
            {
                return FrontendFramework.CreateReactApp;
            }

            // Vue CLI
            if (allDeps.Contains("@vue/cli-service"))
            {
                return FrontendFramework.VueCli;
            }

            return FrontendFramework.Unknown;
        }
        catch
        {
            return FrontendFramework.Unknown;
        }
    }

    /// <summary>
    /// 获取框架对应的环境变量前缀
    /// </summary>
    public static string GetEnvironmentVariablePrefix(FrontendFramework framework)
    {
        return framework switch
        {
            FrontendFramework.Vite => "VITE_",
            FrontendFramework.NextJs => "NEXT_PUBLIC_",
            FrontendFramework.NuxtJs => "NUXT_PUBLIC_",
            FrontendFramework.CreateReactApp => "REACT_APP_",
            FrontendFramework.VueCli => "VUE_APP_",
            FrontendFramework.Unknown => string.Empty,
            _ => string.Empty,
        };
    }

    /// <summary>
    /// 为服务引用生成环境变量名称
    /// </summary>
    /// <param name="framework">前端框架</param>
    /// <param name="serviceName">服务名称</param>
    /// <param name="endpointName">端点名称</param>
    /// <returns>环境变量名称</returns>
    public static string GetServiceUrlEnvironmentVariableName(
        FrontendFramework framework,
        string serviceName,
        string endpointName = "http"
    )
    {
        var prefix = GetEnvironmentVariablePrefix(framework);
        var serviceNameUpper = serviceName.ToUpperInvariant().Replace("-", "_").Replace(".", "_");
        var endpointUpper = endpointName.ToUpperInvariant();

        // 如果框架有前缀，使用简洁的命名
        // 例如: VITE_API_URL, NEXT_PUBLIC_API_URL
        if (!string.IsNullOrEmpty(prefix))
        {
            return $"{prefix}{serviceNameUpper}_URL";
        }

        // 没有前缀时，使用与后端相同的格式
        // 例如: services__api__http__0
        return $"services__{serviceName}__{endpointName}__0";
    }

    /// <summary>
    /// 获取框架的默认输出目录
    /// </summary>
    public static string GetDefaultOutputDirectory(FrontendFramework framework)
    {
        return framework switch
        {
            FrontendFramework.Vite => "dist",
            FrontendFramework.NextJs => ".next",
            FrontendFramework.NuxtJs => ".output",
            FrontendFramework.CreateReactApp => "build",
            FrontendFramework.VueCli => "dist",
            FrontendFramework.Unknown => "dist",
            _ => "dist",
        };
    }

    /// <summary>
    /// 获取框架的默认开发服务器端口
    /// </summary>
    public static int GetDefaultDevPort(FrontendFramework framework)
    {
        return framework switch
        {
            FrontendFramework.Vite => 5173,
            FrontendFramework.NextJs => 3000,
            FrontendFramework.NuxtJs => 3000,
            FrontendFramework.CreateReactApp => 3000,
            FrontendFramework.VueCli => 8080,
            FrontendFramework.Unknown => 3000,
            _ => 3000,
        };
    }

    private static IEnumerable<string> GetDependencies(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var deps) && deps.ValueKind == JsonValueKind.Object)
        {
            foreach (var dep in deps.EnumerateObject())
            {
                yield return dep.Name;
            }
        }
    }
}
