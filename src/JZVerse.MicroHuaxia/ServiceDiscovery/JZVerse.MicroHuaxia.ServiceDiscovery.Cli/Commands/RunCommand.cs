using System.CommandLine;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Configuration;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Extensions;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Cli.Commands;

/// <summary>
/// run 命令 - 运行分布式应用
/// </summary>
public static class RunCommand
{
    public static Command Create()
    {
        var configOption = new Option<FileInfo?>(["-c", "--config"], "配置文件路径 (YAML 或 JSON)");

        var projectOption = new Option<DirectoryInfo?>(["-p", "--project"], "AppHost 项目目录");

        var command = new Command("run", "运行分布式应用") { configOption, projectOption };

        command.SetHandler(
            async (configFile, projectDir) =>
            {
                await ExecuteAsync(configFile, projectDir);
            },
            configOption,
            projectOption
        );

        return command;
    }

    private static async Task ExecuteAsync(FileInfo? configFile, DirectoryInfo? projectDir)
    {
        try
        {
            string? configPath;

            // 优先使用配置文件
            if (configFile?.Exists == true)
            {
                configPath = configFile.FullName;
            }
            else
            {
                // 查找默认配置文件
                var searchDir = projectDir?.FullName ?? Directory.GetCurrentDirectory();
                configPath = FindConfigFile(searchDir);
            }

            if (string.IsNullOrEmpty(configPath))
            {
                Console.WriteLine("错误: 未找到配置文件");
                Console.WriteLine("请使用 -c 指定配置文件，或在当前目录创建 apphost.yaml");
                return;
            }

            Console.WriteLine($"正在加载配置: {configPath}");
            var config = ConfigurationLoader.LoadFromFile(configPath);

            Console.WriteLine($"应用名称: {config.Name}");
            Console.WriteLine($"项目数量: {config.Projects.Count}");
            Console.WriteLine($"容器数量: {config.Containers.Count}");
            Console.WriteLine();

            var basePath = Path.GetDirectoryName(Path.GetFullPath(configPath));
            var builder = config.ToBuilder(basePath);
            var app = builder.Build();

            Console.WriteLine("正在启动分布式应用...");
            Console.WriteLine("按 Ctrl+C 停止");
            Console.WriteLine();

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n正在停止...");
                cts.Cancel();
            };

            await AppHostExtensions.RunAsync(app, cts.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static string? FindConfigFile(string directory)
    {
        var candidates = new[]
        {
            "apphost.yaml",
            "apphost.yml",
            "apphost.json",
            "microhuaxia.yaml",
            "microhuaxia.yml",
            "microhuaxia.json",
        };

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(directory, candidate);
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
