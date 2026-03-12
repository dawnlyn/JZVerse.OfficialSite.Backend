using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Helpers;

/// <summary>
/// 包管理器检测器
/// </summary>
public static class PackageManagerDetector
{
    /// <summary>
    /// 自动检测项目使用的包管理器
    /// </summary>
    /// <param name="projectPath">项目根目录路径</param>
    /// <returns>检测到的包管理器类型</returns>
    public static async Task<PackageManager> DetectAsync(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        // 优先根据 lock 文件检测
        var pnpmLockPath = Path.Combine(projectPath, "pnpm-lock.yaml");
        if (File.Exists(pnpmLockPath))
        {
            return PackageManager.Pnpm;
        }

        var yarnLockPath = Path.Combine(projectPath, "yarn.lock");
        if (File.Exists(yarnLockPath))
        {
            return PackageManager.Yarn;
        }

        var npmLockPath = Path.Combine(projectPath, "package-lock.json");
        if (File.Exists(npmLockPath))
        {
            return PackageManager.Npm;
        }

        // 如果没有 lock 文件，检查 package.json 中的 packageManager 字段
        var packageJsonPath = Path.Combine(projectPath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            var content = await File.ReadAllTextAsync(packageJsonPath);
            if (content.Contains("\"packageManager\""))
            {
                if (content.Contains("pnpm@"))
                    return PackageManager.Pnpm;
                if (content.Contains("yarn@"))
                    return PackageManager.Yarn;
                if (content.Contains("npm@"))
                    return PackageManager.Npm;
            }
        }

        // 默认使用 npm
        return PackageManager.Npm;
    }

    /// <summary>
    /// 获取包管理器的可执行文件名
    /// </summary>
    public static string GetExecutableName(PackageManager manager)
    {
        return manager switch
        {
            PackageManager.Npm => "npm",
            PackageManager.Yarn => "yarn",
            PackageManager.Pnpm => "pnpm",
            PackageManager.Auto => "npm", // 默认
            _ => "npm",
        };
    }

    /// <summary>
    /// 获取运行脚本的命令格式
    /// </summary>
    /// <param name="manager">包管理器</param>
    /// <param name="scriptName">脚本名称</param>
    /// <returns>完整的命令行参数</returns>
    public static string GetRunCommand(PackageManager manager, string scriptName)
    {
        return manager switch
        {
            // npm 需要 "run" 关键字
            PackageManager.Npm => $"run {scriptName}",
            // yarn 和 pnpm 可以直接运行脚本名
            PackageManager.Yarn => scriptName,
            PackageManager.Pnpm => scriptName,
            PackageManager.Auto => $"run {scriptName}",
            _ => $"run {scriptName}",
        };
    }

    /// <summary>
    /// 获取安装依赖的命令
    /// </summary>
    public static string GetInstallCommand(PackageManager manager)
    {
        return manager switch
        {
            PackageManager.Npm => "install",
            PackageManager.Yarn => "install",
            PackageManager.Pnpm => "install",
            PackageManager.Auto => "install",
            _ => "install",
        };
    }

    /// <summary>
    /// 检查包管理器是否在系统中可用
    /// </summary>
    public static bool IsAvailable(PackageManager manager)
    {
        var executableName = GetExecutableName(manager);
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathEnv.Split(Path.PathSeparator);

        foreach (var path in paths)
        {
            var fullPath = Path.Combine(path, executableName);
            if (File.Exists(fullPath))
                return true;

            // Windows 上检查 .cmd 和 .exe 后缀
            if (OperatingSystem.IsWindows())
            {
                if (File.Exists(fullPath + ".cmd") || File.Exists(fullPath + ".exe"))
                    return true;
            }
        }

        return false;
    }
}
