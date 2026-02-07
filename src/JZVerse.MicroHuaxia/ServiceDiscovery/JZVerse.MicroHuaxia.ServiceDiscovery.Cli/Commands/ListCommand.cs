using System.CommandLine;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Configuration;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Cli.Commands;

/// <summary>
/// list 命令 - 列出配置中的资源
/// </summary>
public static class ListCommand
{
    public static Command Create()
    {
        var configOption = new Option<FileInfo?>(["-c", "--config"], "配置文件路径");

        var typeOption = new Option<string?>(
            ["-t", "--type"],
            "资源类型过滤 (project, container, executable, external)"
        );

        var command = new Command("list", "列出配置中的资源") { configOption, typeOption };

        command.SetHandler(Execute, configOption, typeOption);

        return command;
    }

    private static void Execute(FileInfo? configFile, string? resourceType)
    {
        try
        {
            var configPath = configFile?.FullName ?? FindConfigFile();

            if (string.IsNullOrEmpty(configPath))
            {
                Console.WriteLine("错误: 未找到配置文件");
                Console.WriteLine("请使用 -c 指定配置文件，或运行 'mhx-sd init' 创建配置");
                Environment.ExitCode = 1;
                return;
            }

            var config = ConfigurationLoader.LoadFromFile(configPath);

            Console.WriteLine($"配置文件: {configPath}");
            Console.WriteLine($"应用名称: {config.Name}");
            Console.WriteLine();

            var showAll = string.IsNullOrEmpty(resourceType);

            if (showAll || resourceType == "project")
            {
                ShowProjects(config);
            }

            if (showAll || resourceType == "container")
            {
                ShowContainers(config);
            }

            if (showAll || resourceType == "executable")
            {
                ShowExecutables(config);
            }

            if (showAll || resourceType == "external")
            {
                ShowExternalServices(config);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static void ShowProjects(AppHostConfiguration config)
    {
        if (config.Projects.Count == 0)
            return;

        Console.WriteLine("项目:");
        Console.WriteLine($"  {"名称", -25} {"路径", -40} {"副本", -5}");
        Console.WriteLine($"  {new string('-', 75)}");

        foreach (var project in config.Projects)
        {
            Console.WriteLine($"  {project.Name, -25} {project.Path, -40} {project.Replicas, -5}");
        }
        Console.WriteLine();
    }

    private static void ShowContainers(AppHostConfiguration config)
    {
        if (config.Containers.Count == 0)
            return;

        Console.WriteLine("容器:");
        Console.WriteLine($"  {"名称", -25} {"镜像", -40}");
        Console.WriteLine($"  {new string('-', 70)}");

        foreach (var container in config.Containers)
        {
            var image = $"{container.Image}:{container.Tag}";
            Console.WriteLine($"  {container.Name, -25} {image, -40}");
        }
        Console.WriteLine();
    }

    private static void ShowExecutables(AppHostConfiguration config)
    {
        if (config.Executables.Count == 0)
            return;

        Console.WriteLine("可执行程序:");
        Console.WriteLine($"  {"名称", -25} {"路径", -50}");
        Console.WriteLine($"  {new string('-', 80)}");

        foreach (var executable in config.Executables)
        {
            Console.WriteLine($"  {executable.Name, -25} {executable.Path, -50}");
        }
        Console.WriteLine();
    }

    private static void ShowExternalServices(AppHostConfiguration config)
    {
        if (config.ExternalServices.Count == 0)
            return;

        Console.WriteLine("外部服务:");
        Console.WriteLine($"  {"名称", -25} {"URL", -50}");
        Console.WriteLine($"  {new string('-', 80)}");

        foreach (var external in config.ExternalServices)
        {
            Console.WriteLine($"  {external.Name, -25} {external.Url, -50}");
        }
        Console.WriteLine();
    }

    private static string? FindConfigFile()
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

        var currentDir = Directory.GetCurrentDirectory();
        foreach (var candidate in candidates)
        {
            var path = Path.Combine(currentDir, candidate);
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
