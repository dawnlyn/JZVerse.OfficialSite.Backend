using System.Diagnostics;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Helpers;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Lifecycle;

/// <summary>
/// 资源生命周期管理器
/// </summary>
public sealed class ResourceLifecycleManager(ILogger<ResourceLifecycleManager> _logger) : IAsyncDisposable
{
    private readonly Dictionary<string, Process> _processes = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// 启动资源
    /// </summary>
    public async Task StartResourceAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (resource.State == ResourceState.Running)
            {
                _logger.LogWarning("Resource {Name} is already running", resource.Name);
                return;
            }

            resource.State = ResourceState.Starting;
            _logger.LogInformation("Starting resource: {Name} ({Type})", resource.Name, resource.Type);

            switch (resource)
            {
                case ProjectResource project:
                    await StartProjectAsync(project, cancellationToken);
                    break;
                case ExecutableResource executable:
                    await StartExecutableAsync(executable, cancellationToken);
                    break;
                case ContainerResource container:
                    await StartContainerAsync(container, cancellationToken);
                    break;
                case FrontendResource frontend:
                    await StartFrontendAsync(frontend, cancellationToken);
                    break;
                case ExternalServiceResource:
                    // 外部服务不需要启动
                    resource.State = ResourceState.Running;
                    break;
            }
        }
        catch (Exception ex)
        {
            resource.State = ResourceState.Failed;
            _logger.LogError(ex, "Failed to start resource: {Name}", resource.Name);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 停止资源
    /// </summary>
    public async Task StopResourceAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (resource.State is ResourceState.Stopped or ResourceState.Pending)
            {
                return;
            }

            resource.State = ResourceState.Stopping;
            _logger.LogInformation("Stopping resource: {Name}", resource.Name);

            if (_processes.TryGetValue(resource.Name, out var process))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                        await process.WaitForExitAsync(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping process for resource: {Name}", resource.Name);
                }
                finally
                {
                    _processes.Remove(resource.Name);
                    process.Dispose();
                }
            }

            if (resource is ContainerResource container)
            {
                await StopContainerAsync(container, cancellationToken);
            }
            else if (resource is FrontendResource frontend && frontend.DeploymentMode != DeploymentMode.LocalDevelopment)
            {
                await StopFrontendContainerAsync(frontend, cancellationToken);
            }

            resource.State = ResourceState.Stopped;
            _logger.LogInformation("Resource stopped: {Name}", resource.Name);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task StartProjectAsync(ProjectResource project, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = BuildDotnetRunArgs(project),
            WorkingDirectory = project.WorkingDirectory ?? Path.GetDirectoryName(project.ProjectPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var (key, value) in project.Environment)
        {
            startInfo.Environment[key] = value;
        }

        var process = new Process { StartInfo = startInfo };
        SetupProcessLogging(process, project.Name);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _processes[project.Name] = process;
        project.State = ResourceState.Running;

        _logger.LogInformation("Project started: {Name} (PID: {PID})", project.Name, process.Id);

        await Task.CompletedTask;
    }

    private async Task StartExecutableAsync(ExecutableResource executable, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable.ExecutablePath,
            Arguments = string.Join(" ", executable.Args),
            WorkingDirectory = executable.WorkingDirectory ?? Path.GetDirectoryName(executable.ExecutablePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var (key, value) in executable.Environment)
        {
            startInfo.Environment[key] = value;
        }

        var process = new Process { StartInfo = startInfo };
        SetupProcessLogging(process, executable.Name);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _processes[executable.Name] = process;
        executable.State = ResourceState.Running;

        _logger.LogInformation("Executable started: {Name} (PID: {PID})", executable.Name, process.Id);

        await Task.CompletedTask;
    }

    private async Task StartContainerAsync(ContainerResource container, CancellationToken cancellationToken)
    {
        var args = BuildDockerRunArgs(container);

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to start container: {error}");
        }

        container.State = ResourceState.Running;
        _logger.LogInformation("Container started: {Name} (ID: {ContainerId})", container.Name, output.Trim()[..12]);
    }

    private async Task StopContainerAsync(ContainerResource container, CancellationToken cancellationToken)
    {
        var containerName = container.ContainerName ?? container.Name;

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"stop {containerName}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        // 尝试删除容器
        startInfo.Arguments = $"rm {containerName}";
        process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync(cancellationToken);
    }

    #region Frontend Resource Methods

    private async Task StartFrontendAsync(FrontendResource frontend, CancellationToken cancellationToken)
    {
        switch (frontend.DeploymentMode)
        {
            case DeploymentMode.LocalDevelopment:
                await StartFrontendLocalAsync(frontend, cancellationToken);
                break;
            case DeploymentMode.ContainerExisting:
                await StartFrontendContainerExistingAsync(frontend, cancellationToken);
                break;
            case DeploymentMode.ContainerDockerfile:
                await StartFrontendWithDockerfileAsync(frontend, cancellationToken);
                break;
            case DeploymentMode.ContainerGit:
                await StartFrontendFromGitAsync(frontend, cancellationToken);
                break;
        }
    }

    private async Task StartFrontendLocalAsync(FrontendResource frontend, CancellationToken cancellationToken)
    {
        // 检测包管理器
        var packageManager = frontend.PackageManager;
        if (packageManager == PackageManager.Auto)
        {
            packageManager = await PackageManagerDetector.DetectAsync(frontend.ProjectPath);
            _logger.LogInformation("Detected package manager: {PackageManager} for {Name}", packageManager, frontend.Name);
        }

        // 检查包管理器是否可用
        if (!PackageManagerDetector.IsAvailable(packageManager))
        {
            _logger.LogWarning(
                "Package manager {PackageManager} not found in PATH, falling back to npm",
                packageManager
            );
            packageManager = PackageManager.Npm;
        }

        var executableName = PackageManagerDetector.GetExecutableName(packageManager);
        var runCommand = PackageManagerDetector.GetRunCommand(packageManager, frontend.StartCommand);

        var startInfo = new ProcessStartInfo
        {
            FileName = executableName,
            Arguments = runCommand,
            WorkingDirectory = frontend.WorkingDirectory ?? frontend.ProjectPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // 注入环境变量
        foreach (var (key, value) in frontend.Environment)
        {
            startInfo.Environment[key] = value;
        }

        var process = new Process { StartInfo = startInfo };
        SetupProcessLogging(process, frontend.Name);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _processes[frontend.Name] = process;
        frontend.State = ResourceState.Running;

        _logger.LogInformation(
            "Frontend started: {Name} (PID: {PID}) using {PackageManager} {Command}",
            frontend.Name,
            process.Id,
            executableName,
            runCommand
        );
    }

    private async Task StartFrontendContainerExistingAsync(FrontendResource frontend, CancellationToken cancellationToken)
    {
        var args = BuildFrontendDockerRunArgs(frontend);

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to start frontend container: {error}");
        }

        frontend.State = ResourceState.Running;
        var containerId = output.Trim().Length >= 12 ? output.Trim()[..12] : output.Trim();
        _logger.LogInformation("Frontend container started: {Name} (ID: {ContainerId})", frontend.Name, containerId);
    }

    private async Task StartFrontendWithDockerfileAsync(FrontendResource frontend, CancellationToken cancellationToken)
    {
        var dockerConfig = frontend.DockerConfig;
        var imageName = $"{frontend.Name}:{dockerConfig.ImageTag}";
        var contextPath = dockerConfig.ContextPath ?? Path.GetDirectoryName(dockerConfig.DockerfilePath) ?? ".";

        // Step 1: Build the image
        _logger.LogInformation("Building frontend image: {ImageName} from {Dockerfile}", imageName, dockerConfig.DockerfilePath);

        var buildArgs = new List<string>
        {
            "build",
            "-t", imageName,
            "-f", $"\"{dockerConfig.DockerfilePath}\"",
        };

        foreach (var (key, value) in dockerConfig.BuildArgs)
        {
            buildArgs.Add("--build-arg");
            buildArgs.Add($"{key}={value}");
        }

        // 注入环境变量作为构建参数
        foreach (var (key, value) in frontend.Environment)
        {
            buildArgs.Add("--build-arg");
            buildArgs.Add($"{key}={value}");
        }

        buildArgs.Add($"\"{contextPath}\"");

        var buildStartInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = string.Join(" ", buildArgs),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var buildProcess = new Process { StartInfo = buildStartInfo };
        SetupProcessLogging(buildProcess, $"{frontend.Name}-build");

        buildProcess.Start();
        buildProcess.BeginOutputReadLine();
        buildProcess.BeginErrorReadLine();

        await buildProcess.WaitForExitAsync(cancellationToken);

        if (buildProcess.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to build frontend image: {frontend.Name}");
        }

        _logger.LogInformation("Frontend image built: {ImageName}", imageName);

        // Step 2: Run the container
        dockerConfig.ContainerImage = frontend.Name;
        await StartFrontendContainerExistingAsync(frontend, cancellationToken);
    }

    private async Task StartFrontendFromGitAsync(FrontendResource frontend, CancellationToken cancellationToken)
    {
        var dockerConfig = frontend.DockerConfig;
        var tempDir = Path.Combine(Path.GetTempPath(), $"frontend-{frontend.Name}-{Guid.NewGuid():N}");

        try
        {
            // Step 1: Clone the repository
            _logger.LogInformation("Cloning repository: {Url} to {TempDir}", dockerConfig.GitRepositoryUrl, tempDir);

            var cloneArgs = $"clone --depth 1 --branch {dockerConfig.GitBranch} \"{dockerConfig.GitRepositoryUrl}\" \"{tempDir}\"";
            var cloneStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = cloneArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            var cloneProcess = new Process { StartInfo = cloneStartInfo };
            cloneProcess.Start();

            var cloneError = await cloneProcess.StandardError.ReadToEndAsync(cancellationToken);
            await cloneProcess.WaitForExitAsync(cancellationToken);

            if (cloneProcess.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to clone repository: {cloneError}");
            }

            _logger.LogInformation("Repository cloned: {Name}", frontend.Name);

            // Step 2: Detect framework and generate Dockerfile
            var framework = await FrontendFrameworkDetector.DetectAsync(tempDir);
            var packageManager = await PackageManagerDetector.DetectAsync(tempDir);

            _logger.LogInformation(
                "Detected framework: {Framework}, package manager: {PackageManager}",
                framework,
                packageManager
            );

            var dockerfilePath = Path.Combine(tempDir, "Dockerfile.generated");
            await DockerfileGenerator.GenerateAsync(tempDir, dockerfilePath, framework, packageManager, dockerConfig.BuildArgs);

            _logger.LogInformation("Generated Dockerfile: {Path}", dockerfilePath);

            // Step 3: Build and run
            dockerConfig.DockerfilePath = dockerfilePath;
            dockerConfig.ContextPath = tempDir;

            await StartFrontendWithDockerfileAsync(frontend, cancellationToken);
        }
        finally
        {
            // Cleanup temp directory (optional, might want to keep for debugging)
            // Directory.Delete(tempDir, recursive: true);
        }
    }

    private async Task StopFrontendContainerAsync(FrontendResource frontend, CancellationToken cancellationToken)
    {
        var containerName = frontend.DockerConfig.ContainerName ?? frontend.Name;

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"stop {containerName}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        // 尝试删除容器
        startInfo.Arguments = $"rm {containerName}";
        process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync(cancellationToken);
    }

    private static string BuildFrontendDockerRunArgs(FrontendResource frontend)
    {
        var dockerConfig = frontend.DockerConfig;
        var containerName = dockerConfig.ContainerName ?? frontend.Name;

        var args = new List<string>
        {
            "run",
            "-d",
            $"--name {containerName}",
        };

        foreach (var endpoint in frontend.Endpoints)
        {
            var hostPort = endpoint.Port ?? endpoint.ContainerPort ?? 80;
            var containerPort = endpoint.ContainerPort ?? hostPort;
            args.Add($"-p {hostPort}:{containerPort}");
        }

        foreach (var (key, value) in frontend.Environment)
        {
            args.Add($"-e {key}={value}");
        }

        foreach (var volume in dockerConfig.Volumes)
        {
            var mountFlag = volume.ReadOnly ? ":ro" : "";
            args.Add($"-v {volume.Source}:{volume.Target}{mountFlag}");
        }

        args.Add(dockerConfig.GetFullImageName());

        return string.Join(" ", args);
    }

    #endregion

    private static string BuildDotnetRunArgs(ProjectResource project)
    {
        var args = new List<string> { "run", "--project", $"\"{project.ProjectPath}\"" };

        if (!string.IsNullOrEmpty(project.LaunchProfile))
        {
            args.Add("--launch-profile");
            args.Add(project.LaunchProfile);
        }

        if (project.Args.Count > 0)
        {
            args.Add("--");
            args.AddRange(project.Args);
        }

        return string.Join(" ", args);
    }

    private static string BuildDockerRunArgs(ContainerResource container)
    {
        var args = new List<string>
        {
            "run",
            "-d",
            !string.IsNullOrEmpty(container.ContainerName)
                ? $"--name {container.ContainerName}"
                : $"--name {container.Name}",
        };

        foreach (var endpoint in container.Endpoints)
        {
            var hostPort = endpoint.Port ?? endpoint.ContainerPort;
            args.Add($"-p {hostPort}:{endpoint.ContainerPort}");
        }

        foreach (var (key, value) in container.Environment)
        {
            args.Add($"-e {key}={value}");
        }

        foreach (var volume in container.Volumes)
        {
            var mountFlag = volume.ReadOnly ? ":ro" : "";
            args.Add($"-v {volume.Source}:{volume.Target}{mountFlag}");
        }

        args.Add(container.GetFullImageName());

        if (!string.IsNullOrEmpty(container.Entrypoint))
        {
            args.Add($"--entrypoint {container.Entrypoint}");
        }

        args.AddRange(container.Args);

        return string.Join(" ", args);
    }

    private void SetupProcessLogging(Process process, string resourceName)
    {
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogInformation("[{Resource}] {Output}", resourceName, e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogWarning("[{Resource}] {Error}", resourceName, e.Data);
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (name, process) in _processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    await process.WaitForExitAsync();
                }
                process.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing process: {Name}", name);
            }
        }
        _processes.Clear();
        _semaphore.Dispose();
    }
}
