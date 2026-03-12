using JZVerse.MicroHuaxia.Observability.Core.Configuration;
using JZVerse.MicroHuaxia.Observability.Core.Formatting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Observability.AspNetCore.Startup;

/// <summary>
/// 服务启动信息输出 — 在应用启动后输出服务基本信息
/// </summary>
public sealed class StartupLogger : IHostedLifecycleService
{
    private readonly IOptions<ObservabilityOptions> _options;
    private readonly IServer _server;
    private readonly IHostEnvironment _env;

    public StartupLogger(
        IOptions<ObservabilityOptions> options,
        IServer server,
        IHostEnvironment env)
    {
        _options = options;
        _server = server;
        _env = env;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        var c = opts.Console.EnableColors && opts.Console.Enabled;

        var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];

        Console.WriteLine();
        Console.WriteLine(C(c, AnsiColors.BrightCyan, "  ┌─────────────────────────────────────────────"));
        Console.WriteLine(C(c, AnsiColors.BrightCyan, "  │ ") + C(c, AnsiColors.BoldWhite, opts.ServiceName) + C(c, AnsiColors.BrightWhite, $" v{opts.ServiceVersion}"));
        Console.WriteLine(C(c, AnsiColors.BrightCyan, "  │ ") + C(c, AnsiColors.BrightWhite, "Environment: ") + C(c, AnsiColors.BrightYellow, _env.EnvironmentName));

        foreach (var addr in addresses)
        {
            Console.WriteLine(C(c, AnsiColors.BrightCyan, "  │ ") + C(c, AnsiColors.BrightWhite, "Listening:   ") + C(c, AnsiColors.BrightGreen, addr));
        }

        Console.WriteLine(C(c, AnsiColors.BrightCyan, "  │ ") + C(c, AnsiColors.BrightWhite, "Console:     ") + EnabledText(c, opts.Console.Enabled));
        Console.WriteLine(C(c, AnsiColors.BrightCyan, "  │ ") + C(c, AnsiColors.BrightWhite, "File Log:    ") + EnabledText(c, opts.File.Enabled)
            + (opts.File.Enabled ? C(c, AnsiColors.BrightWhite, $" ({opts.File.Format}, {opts.File.Directory})") : ""));
        Console.WriteLine(C(c, AnsiColors.BrightCyan, "  │ ") + C(c, AnsiColors.BrightWhite, "OpenTelemetry:") + EnabledText(c, opts.OpenTelemetry.Enabled)
            + (opts.OpenTelemetry.Enabled ? C(c, AnsiColors.BrightWhite, $" ({opts.OpenTelemetry.Endpoint})") : ""));
        Console.WriteLine(C(c, AnsiColors.BrightCyan, "  └─────────────────────────────────────────────"));
        Console.WriteLine();

        return Task.CompletedTask;
    }

    private static string EnabledText(bool c, bool enabled)
    {
        return enabled
            ? " " + C(c, AnsiColors.BrightGreen, "已启用")
            : " " + C(c, AnsiColors.BrightYellow, "已禁用");
    }

    private static string C(bool enableColors, string colorCode, string text)
    {
        return enableColors ? $"{colorCode}{text}{AnsiColors.Reset}" : text;
    }
}
