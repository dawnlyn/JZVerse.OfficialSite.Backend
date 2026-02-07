using System.CommandLine;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Cli.Commands;

/// <summary>
/// init 命令 - 初始化配置文件
/// </summary>
public static class InitCommand
{
    public static Command Create()
    {
        var formatOption = new Option<string>(["-f", "--format"], () => "yaml", "配置文件格式 (yaml 或 json)");

        var outputOption = new Option<string?>(["-o", "--output"], "输出文件名");

        var command = new Command("init", "初始化配置文件") { formatOption, outputOption };

        command.SetHandler(Execute, formatOption, outputOption);

        return command;
    }

    private static void Execute(string format, string? output)
    {
        try
        {
            var extension = format.ToLowerInvariant() switch
            {
                "yaml" or "yml" => ".yaml",
                "json" => ".json",
                _ => throw new ArgumentException($"不支持的格式: {format}"),
            };

            var fileName = output ?? $"apphost{extension}";
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

            if (File.Exists(filePath))
            {
                Console.WriteLine($"文件已存在: {filePath}");
                Console.Write("是否覆盖? (y/N): ");
                var answer = Console.ReadLine();
                if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("已取消");
                    return;
                }
            }

            var content = extension == ".yaml" ? GetYamlTemplate() : GetJsonTemplate();
            File.WriteAllText(filePath, content);

            Console.WriteLine($"已创建配置文件: {filePath}");
            Console.WriteLine();
            Console.WriteLine("使用方法:");
            Console.WriteLine($"  mhx-sd run -c {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static string GetYamlTemplate() =>
        """
            # MicroHuaxia Service Discovery 配置文件
            # 文档: https://github.com/jzverse/microhuaxia

            name: MyDistributedApp

            # .NET 项目
            projects:
              - name: api-gateway
                path: src/ApiGateway/ApiGateway.csproj
                endpoints:
                  - name: http
                    scheme: http
                    port: 5000
                environment:
                  ASPNETCORE_ENVIRONMENT: Development
                depends_on:
                  - redis

              - name: user-service
                path: src/UserService/UserService.csproj
                endpoints:
                  - name: http
                    scheme: http
                    port: 5001
                depends_on:
                  - postgres

            # 容器
            containers:
              - name: redis
                image: redis
                tag: "7-alpine"
                endpoints:
                  - name: default
                    scheme: tcp
                    port: 6379
                    container_port: 6379

              - name: postgres
                image: postgres
                tag: "16-alpine"
                endpoints:
                  - name: default
                    scheme: tcp
                    port: 5432
                    container_port: 5432
                environment:
                  POSTGRES_USER: admin
                  POSTGRES_PASSWORD: password
                  POSTGRES_DB: myapp
                volumes:
                  - source: postgres-data
                    target: /var/lib/postgresql/data

            # 外部服务引用
            external_services:
              - name: external-api
                url: https://api.example.com
            """;

    private static string GetJsonTemplate() =>
        """
            {
              "name": "MyDistributedApp",
              "projects": [
                {
                  "name": "api-gateway",
                  "path": "src/ApiGateway/ApiGateway.csproj",
                  "endpoints": [
                    {
                      "name": "http",
                      "scheme": "http",
                      "port": 5000
                    }
                  ],
                  "environment": {
                    "ASPNETCORE_ENVIRONMENT": "Development"
                  },
                  "depends_on": ["redis"]
                }
              ],
              "containers": [
                {
                  "name": "redis",
                  "image": "redis",
                  "tag": "7-alpine",
                  "endpoints": [
                    {
                      "name": "default",
                      "scheme": "tcp",
                      "port": 6379,
                      "container_port": 6379
                    }
                  ]
                }
              ],
              "external_services": []
            }
            """;
}
