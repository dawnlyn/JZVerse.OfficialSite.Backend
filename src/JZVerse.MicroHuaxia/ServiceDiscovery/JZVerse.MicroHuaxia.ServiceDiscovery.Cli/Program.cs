using System.CommandLine;
using JZVerse.MicroHuaxia.ServiceDiscovery.Cli.Commands;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Cli;

/// <summary>
/// MicroHuaxia 服务发现 CLI 入口
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("MicroHuaxia Service Discovery CLI (mhx-sd)")
        {
            RunCommand.Create(),
            StatusCommand.Create(),
            InitCommand.Create(),
            ListCommand.Create(),
        };

        return await rootCommand.InvokeAsync(args);
    }
}
