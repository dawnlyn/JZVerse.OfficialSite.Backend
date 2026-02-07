using System.CommandLine;
using System.Net.Http.Json;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Cli.Commands;

/// <summary>
/// status 命令 - 查看服务状态
/// </summary>
public static class StatusCommand
{
    public static Command Create()
    {
        var serverOption = new Option<string>(["-s", "--server"], () => "http://localhost:5100", "服务发现服务器地址");

        var serviceOption = new Option<string?>(["--service"], "指定服务名称");

        var command = new Command("status", "查看服务状态") { serverOption, serviceOption };

        command.SetHandler(
            async (serverUrl, serviceName) =>
            {
                await ExecuteAsync(serverUrl, serviceName);
            },
            serverOption,
            serviceOption
        );

        return command;
    }

    private static async Task ExecuteAsync(string serverUrl, string? serviceName)
    {
        try
        {
            using var client = new HttpClient();
            client.BaseAddress = new(serverUrl);

            Console.WriteLine($"正在连接服务器: {serverUrl}");
            Console.WriteLine();

            if (string.IsNullOrEmpty(serviceName))
            {
                // 获取所有服务
                await ShowAllServicesAsync(client);
            }
            else
            {
                // 获取指定服务
                await ShowServiceDetailsAsync(client, serviceName);
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"连接失败: {ex.Message}");
            Environment.ExitCode = 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static async Task ShowAllServicesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/services");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"请求失败: {response.StatusCode}");
            return;
        }

        var services = await response.Content.ReadFromJsonAsync<ServiceListResponse>();

        if (services?.Services == null || services.Services.Count == 0)
        {
            Console.WriteLine("当前没有注册的服务");
            return;
        }

        Console.WriteLine($"{"服务名称", -30} {"实例数", -10} {"健康/不健康", -15}");
        Console.WriteLine(new string('-', 60));

        foreach (var service in services.Services)
        {
            var healthStatus = $"{service.HealthyCount}/{service.UnhealthyCount}";
            Console.WriteLine($"{service.ServiceName, -30} {service.InstanceCount, -10} {healthStatus, -15}");
        }

        Console.WriteLine();
        Console.WriteLine($"总计: {services.Services.Count} 个服务");
    }

    private static async Task ShowServiceDetailsAsync(HttpClient client, string serviceName)
    {
        var response = await client.GetAsync($"/api/v1/services/{serviceName}/instances");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"请求失败: {response.StatusCode}");
            return;
        }

        var instances = await response.Content.ReadFromJsonAsync<InstanceListResponse>();

        if (instances?.Instances == null || instances.Instances.Count == 0)
        {
            Console.WriteLine($"服务 '{serviceName}' 没有注册的实例");
            return;
        }

        Console.WriteLine($"服务: {serviceName}");
        Console.WriteLine($"实例数: {instances.Instances.Count}");
        Console.WriteLine();
        Console.WriteLine($"{"实例ID", -40} {"地址", -25} {"状态", -10}");
        Console.WriteLine(new string('-', 80));

        foreach (var instance in instances.Instances)
        {
            var address = $"{instance.Host}:{instance.Port}";
            var status = instance.Health switch
            {
                "Healthy" => "健康",
                "Unhealthy" => "不健康",
                "Unknown" => "未知",
                _ => instance.Health,
            };
            Console.WriteLine($"{instance.InstanceId, -40} {address, -25} {status, -10}");
        }
    }

    private record ServiceListResponse(List<ServiceInfo> Services);

    private record ServiceInfo(string ServiceName, int InstanceCount, int HealthyCount, int UnhealthyCount);

    private record InstanceListResponse(List<InstanceInfo> Instances);

    private record InstanceInfo(string InstanceId, string Host, int Port, string Health);
}
