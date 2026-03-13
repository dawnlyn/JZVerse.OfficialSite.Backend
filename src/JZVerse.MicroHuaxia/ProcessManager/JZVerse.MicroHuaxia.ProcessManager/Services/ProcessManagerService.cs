using System.Collections.Concurrent;
using System.Diagnostics;
using JZVerse.MicroHuaxia.ProcessManager.Configuration;
using JZVerse.MicroHuaxia.ProcessManager.Models;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ProcessManager.Services;

/// <summary>
/// 进程管理核心服务 —— 纯粹的启停执行器。
/// 仅负责：
///   1. 管理进程定义（启动配置），支持静态配置 + 动态注册
///   2. 按指令启动 / 停止 / 重启指定进程
/// 不负责健康检查和自动重启（由调用方自行决定）。
/// </summary>
public sealed class ProcessManagerService : IDisposable
{
    private readonly ProcessManagerOptions _options;
    private readonly ILogger<ProcessManagerService> _logger;
    private readonly ConcurrentDictionary<string, ManagedProcessContext> _processes = new();

    public ProcessManagerService(
        IOptions<ProcessManagerOptions> options,
        ILogger<ProcessManagerService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // 从配置文件加载预置进程定义
        foreach (var def in _options.Processes)
        {
            _processes[def.Id] = new ManagedProcessContext { Definition = def };
        }
    }

    // ────────────────── 进程定义管理 ──────────────────

    /// <summary>
    /// 获取所有进程定义
    /// </summary>
    public List<ManagedProcessDefinition> GetAllDefinitions()
    {
        return _processes.Values.Select(c => c.Definition).ToList();
    }

    /// <summary>
    /// 获取单个进程定义
    /// </summary>
    public ManagedProcessDefinition? GetDefinition(string processId)
    {
        return _processes.TryGetValue(processId, out var ctx) ? ctx.Definition : null;
    }

    /// <summary>
    /// 注册或更新进程定义。
    /// 如果进程正在运行，仅更新定义但不影响当前运行实例（下次启动时生效）。
    /// </summary>
    public void RegisterOrUpdate(ManagedProcessDefinition definition)
    {
        _processes.AddOrUpdate(
            definition.Id,
            _ => new ManagedProcessContext { Definition = definition },
            (_, existing) =>
            {
                existing.Definition = definition;
                return existing;
            });

        _logger.LogInformation("Process definition registered/updated: {Id} ({Name})",
            definition.Id, definition.DisplayName);
    }

    /// <summary>
    /// 注销进程定义。进程必须已停止才能注销。
    /// </summary>
    public bool Unregister(string processId)
    {
        if (!_processes.TryGetValue(processId, out var ctx))
            return false;

        if (ctx.State is ProcessState.Running or ProcessState.Starting)
        {
            _logger.LogWarning("Cannot unregister process {Id}: still running", processId);
            return false;
        }

        _processes.TryRemove(processId, out _);
        _logger.LogInformation("Process definition unregistered: {Id}", processId);
        return true;
    }

    // ────────────────── 状态查询 ──────────────────

    /// <summary>
    /// 获取所有进程状态
    /// </summary>
    public List<ManagedProcessStatus> GetAllStatus()
    {
        return _processes.Values.Select(BuildStatus).ToList();
    }

    /// <summary>
    /// 获取单个进程状态
    /// </summary>
    public ManagedProcessStatus? GetStatus(string processId)
    {
        return _processes.TryGetValue(processId, out var ctx)
            ? BuildStatus(ctx)
            : null;
    }

    // ────────────────── 启停操作 ──────────────────

    /// <summary>
    /// 启动进程
    /// </summary>
    public async Task<bool> StartAsync(string processId, CancellationToken ct = default)
    {
        if (!_processes.TryGetValue(processId, out var ctx))
            return false;

        if (ctx.State is ProcessState.Running or ProcessState.Starting)
        {
            _logger.LogWarning("Process {Id} is already {State}", processId, ctx.State);
            return true;
        }

        return await StartProcessInternalAsync(ctx, ct);
    }

    /// <summary>
    /// 停止进程
    /// </summary>
    public async Task<bool> StopAsync(string processId, CancellationToken ct = default)
    {
        if (!_processes.TryGetValue(processId, out var ctx))
            return false;

        if (ctx.State == ProcessState.Stopped)
            return true;

        return await StopProcessInternalAsync(ctx, ct);
    }

    /// <summary>
    /// 重启进程
    /// </summary>
    public async Task<bool> RestartAsync(string processId, CancellationToken ct = default)
    {
        if (!_processes.TryGetValue(processId, out var ctx))
            return false;

        if (ctx.State is ProcessState.Running or ProcessState.Starting)
        {
            await StopProcessInternalAsync(ctx, ct);
        }

        return await StartProcessInternalAsync(ctx, ct);
    }

    /// <summary>
    /// 启动所有进程（按 StartOrder 排序）
    /// </summary>
    public async Task StartAllAsync(CancellationToken ct = default)
    {
        var ordered = _processes.Values
            .OrderBy(p => p.Definition.StartOrder)
            .ToList();

        foreach (var ctx in ordered)
        {
            if (ctx.State != ProcessState.Running)
            {
                await StartProcessInternalAsync(ctx, ct);
                await Task.Delay(500, ct); // 间隔启动
            }
        }
    }

    /// <summary>
    /// 停止所有进程（按 StartOrder 倒序）
    /// </summary>
    public async Task StopAllAsync(CancellationToken ct = default)
    {
        var ordered = _processes.Values
            .OrderByDescending(p => p.Definition.StartOrder)
            .ToList();

        foreach (var ctx in ordered)
        {
            if (ctx.State is ProcessState.Running or ProcessState.Starting)
            {
                await StopProcessInternalAsync(ctx, ct);
            }
        }
    }

    /// <summary>
    /// 重启所有进程（先按倒序停止，再按正序启动）
    /// </summary>
    public async Task RestartAllAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Restarting all processes...");
        await StopAllAsync(ct);
        await StartAllAsync(ct);
        _logger.LogInformation("All processes restarted");
    }

    // ────────────────── 内部实现 ──────────────────

    private async Task<bool> StartProcessInternalAsync(ManagedProcessContext ctx, CancellationToken ct)
    {
        var def = ctx.Definition;
        ctx.State = ProcessState.Starting;
        ctx.LastError = null;

        try
        {
            var startInfo = BuildProcessStartInfo(def);
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    _logger.LogDebug("[{ProcessId}] {Output}", def.Id, e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    _logger.LogWarning("[{ProcessId}] {Error}", def.Id, e.Data);
            };

            if (!process.Start())
            {
                ctx.State = ProcessState.Failed;
                ctx.LastError = "进程启动失败";
                return false;
            }

            if (startInfo.RedirectStandardOutput) process.BeginOutputReadLine();
            if (startInfo.RedirectStandardError) process.BeginErrorReadLine();

            ctx.Process = process;
            ctx.State = ProcessState.Running;
            ctx.StartedAt = DateTime.UtcNow;
            ctx.StoppedAt = null;

            _logger.LogInformation("Process {Id} started, PID={Pid}", def.Id, process.Id);
            return true;
        }
        catch (Exception ex)
        {
            ctx.State = ProcessState.Failed;
            ctx.LastError = ex.Message;
            _logger.LogError(ex, "Failed to start process {Id}", def.Id);
            return false;
        }
    }

    private async Task<bool> StopProcessInternalAsync(ManagedProcessContext ctx, CancellationToken ct)
    {
        ctx.State = ProcessState.Stopping;
        var process = ctx.Process;

        if (process is null || process.HasExited)
        {
            ctx.State = ProcessState.Stopped;
            ctx.StoppedAt = DateTime.UtcNow;
            ctx.Process = null;
            return true;
        }

        try
        {
            _logger.LogInformation("Stopping process {Id}, PID={Pid}", ctx.Definition.Id, process.Id);

            process.Kill(entireProcessTree: true);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.StopTimeoutSeconds));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Process {Id} did not exit within timeout, force killing", ctx.Definition.Id);
            }

            ctx.State = ProcessState.Stopped;
            ctx.StoppedAt = DateTime.UtcNow;
            ctx.Process = null;

            _logger.LogInformation("Process {Id} stopped", ctx.Definition.Id);
            return true;
        }
        catch (Exception ex)
        {
            ctx.State = ProcessState.Failed;
            ctx.LastError = ex.Message;
            ctx.Process = null;
            _logger.LogError(ex, "Failed to stop process {Id}", ctx.Definition.Id);
            return false;
        }
    }

    private static ProcessStartInfo BuildProcessStartInfo(ManagedProcessDefinition def)
    {
        string fileName;
        string arguments;

        if (!string.IsNullOrEmpty(def.ProjectPath))
        {
            fileName = "dotnet";
            arguments = $"run --project \"{def.ProjectPath}\" --no-launch-profile";
            if (!string.IsNullOrEmpty(def.Arguments))
                arguments += $" -- {def.Arguments}";
        }
        else if (!string.IsNullOrEmpty(def.ExecutablePath))
        {
            fileName = def.ExecutablePath;
            arguments = def.Arguments ?? string.Empty;
        }
        else
        {
            throw new InvalidOperationException($"Process {def.Id} has neither ProjectPath nor ExecutablePath");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrEmpty(def.WorkingDirectory))
        {
            startInfo.WorkingDirectory = def.WorkingDirectory;
        }

        foreach (var (key, value) in def.Environment)
        {
            startInfo.Environment[key] = value;
        }

        return startInfo;
    }

    private static ManagedProcessStatus BuildStatus(ManagedProcessContext ctx) => new()
    {
        Id = ctx.Definition.Id,
        DisplayName = ctx.Definition.DisplayName,
        State = ctx.State,
        Pid = ctx.Process is { HasExited: false } ? ctx.Process.Id : null,
        StartedAt = ctx.StartedAt,
        StoppedAt = ctx.StoppedAt,
        LastError = ctx.LastError,
    };

    public void Dispose()
    {
        foreach (var (_, ctx) in _processes)
        {
            if (ctx.Process is { HasExited: false })
            {
                try { ctx.Process.Kill(true); } catch { /* best effort */ }
            }
        }
    }

    /// <summary>
    /// 进程运行时上下文（内部）
    /// </summary>
    private sealed class ManagedProcessContext
    {
        public required ManagedProcessDefinition Definition { get; set; }
        public Process? Process { get; set; }
        public ProcessState State { get; set; } = ProcessState.Stopped;
        public DateTime? StartedAt { get; set; }
        public DateTime? StoppedAt { get; set; }
        public string? LastError { get; set; }
    }
}
