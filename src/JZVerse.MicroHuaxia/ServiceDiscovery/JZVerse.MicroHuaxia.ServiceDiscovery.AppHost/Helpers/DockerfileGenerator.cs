using System.Text;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Helpers;

/// <summary>
/// Dockerfile 生成器
/// </summary>
public static class DockerfileGenerator
{
    /// <summary>
    /// 为前端项目生成 Dockerfile
    /// </summary>
    /// <param name="projectPath">项目根目录路径</param>
    /// <param name="outputPath">Dockerfile 输出路径</param>
    /// <param name="framework">前端框架类型</param>
    /// <param name="packageManager">包管理器类型</param>
    /// <param name="buildArgs">构建参数</param>
    public static async Task GenerateAsync(
        string projectPath,
        string outputPath,
        FrontendFramework framework,
        PackageManager packageManager,
        Dictionary<string, string>? buildArgs = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        // 如果未指定包管理器，自动检测
        if (packageManager == PackageManager.Auto)
        {
            packageManager = await PackageManagerDetector.DetectAsync(projectPath);
        }

        var dockerfile = framework switch
        {
            FrontendFramework.NextJs => GenerateNextJsDockerfile(packageManager, buildArgs),
            FrontendFramework.NuxtJs => GenerateNuxtJsDockerfile(packageManager, buildArgs),
            _ => GenerateStaticDockerfile(framework, packageManager, buildArgs),
        };

        await File.WriteAllTextAsync(outputPath, dockerfile);
    }

    /// <summary>
    /// 生成静态站点 Dockerfile (Vite, CRA, Vue CLI 等)
    /// </summary>
    private static string GenerateStaticDockerfile(
        FrontendFramework framework,
        PackageManager packageManager,
        Dictionary<string, string>? buildArgs
    )
    {
        var sb = new StringBuilder();
        var outputDir = FrontendFrameworkDetector.GetDefaultOutputDirectory(framework);
        var pm = PackageManagerDetector.GetExecutableName(packageManager);
        var installCmd = GetInstallCommand(packageManager);

        // Build stage
        sb.AppendLine("# Build stage");
        sb.AppendLine("FROM node:20-alpine AS builder");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();

        // 添加构建参数
        if (buildArgs?.Count > 0)
        {
            foreach (var (key, _) in buildArgs)
            {
                sb.AppendLine($"ARG {key}");
                sb.AppendLine($"ENV {key}=${{{key}}}");
            }
            sb.AppendLine();
        }

        // 安装依赖
        sb.AppendLine("# Install dependencies");
        AppendPackageManagerSetup(sb, packageManager);
        sb.AppendLine($"COPY package*.json ./");
        if (packageManager == PackageManager.Pnpm)
        {
            sb.AppendLine("COPY pnpm-lock.yaml* ./");
        }
        else if (packageManager == PackageManager.Yarn)
        {
            sb.AppendLine("COPY yarn.lock* ./");
        }
        sb.AppendLine($"RUN {pm} {installCmd}");
        sb.AppendLine();

        // 构建
        sb.AppendLine("# Build application");
        sb.AppendLine("COPY . .");
        sb.AppendLine($"RUN {pm} {GetBuildCommand(packageManager)}");
        sb.AppendLine();

        // Production stage
        sb.AppendLine("# Production stage");
        sb.AppendLine("FROM nginx:alpine");
        sb.AppendLine();
        sb.AppendLine($"COPY --from=builder /app/{outputDir} /usr/share/nginx/html");
        sb.AppendLine();

        // Nginx 配置 (支持 SPA 路由)
        sb.AppendLine("# Configure nginx for SPA");
        sb.AppendLine("RUN echo 'server {' > /etc/nginx/conf.d/default.conf && \\");
        sb.AppendLine("    echo '    listen 80;' >> /etc/nginx/conf.d/default.conf && \\");
        sb.AppendLine("    echo '    location / {' >> /etc/nginx/conf.d/default.conf && \\");
        sb.AppendLine("    echo '        root /usr/share/nginx/html;' >> /etc/nginx/conf.d/default.conf && \\");
        sb.AppendLine("    echo '        try_files $uri $uri/ /index.html;' >> /etc/nginx/conf.d/default.conf && \\");
        sb.AppendLine("    echo '    }' >> /etc/nginx/conf.d/default.conf && \\");
        sb.AppendLine("    echo '}' >> /etc/nginx/conf.d/default.conf");
        sb.AppendLine();
        sb.AppendLine("EXPOSE 80");
        sb.AppendLine("CMD [\"nginx\", \"-g\", \"daemon off;\"]");

        return sb.ToString();
    }

    /// <summary>
    /// 生成 Next.js Dockerfile (支持 SSR)
    /// </summary>
    private static string GenerateNextJsDockerfile(PackageManager packageManager, Dictionary<string, string>? buildArgs)
    {
        var sb = new StringBuilder();
        var pm = PackageManagerDetector.GetExecutableName(packageManager);
        var installCmd = GetInstallCommand(packageManager);

        // Dependencies stage
        sb.AppendLine("# Dependencies stage");
        sb.AppendLine("FROM node:20-alpine AS deps");
        sb.AppendLine("RUN apk add --no-cache libc6-compat");
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        AppendPackageManagerSetup(sb, packageManager);
        sb.AppendLine("COPY package*.json ./");
        if (packageManager == PackageManager.Pnpm)
        {
            sb.AppendLine("COPY pnpm-lock.yaml* ./");
        }
        else if (packageManager == PackageManager.Yarn)
        {
            sb.AppendLine("COPY yarn.lock* ./");
        }
        sb.AppendLine($"RUN {pm} {installCmd}");
        sb.AppendLine();

        // Builder stage
        sb.AppendLine("# Builder stage");
        sb.AppendLine("FROM node:20-alpine AS builder");
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();

        if (buildArgs?.Count > 0)
        {
            foreach (var (key, _) in buildArgs)
            {
                sb.AppendLine($"ARG {key}");
                sb.AppendLine($"ENV {key}=${{{key}}}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("COPY --from=deps /app/node_modules ./node_modules");
        sb.AppendLine("COPY . .");
        sb.AppendLine("ENV NEXT_TELEMETRY_DISABLED=1");
        sb.AppendLine($"RUN {pm} {GetBuildCommand(packageManager)}");
        sb.AppendLine();

        // Runner stage
        sb.AppendLine("# Runner stage");
        sb.AppendLine("FROM node:20-alpine AS runner");
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("ENV NODE_ENV=production");
        sb.AppendLine("ENV NEXT_TELEMETRY_DISABLED=1");
        sb.AppendLine();
        sb.AppendLine("RUN addgroup --system --gid 1001 nodejs");
        sb.AppendLine("RUN adduser --system --uid 1001 nextjs");
        sb.AppendLine();
        sb.AppendLine("COPY --from=builder /app/public ./public");
        sb.AppendLine("COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./");
        sb.AppendLine("COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static");
        sb.AppendLine();
        sb.AppendLine("USER nextjs");
        sb.AppendLine("EXPOSE 3000");
        sb.AppendLine("ENV PORT=3000");
        sb.AppendLine("ENV HOSTNAME=\"0.0.0.0\"");
        sb.AppendLine("CMD [\"node\", \"server.js\"]");

        return sb.ToString();
    }

    /// <summary>
    /// 生成 Nuxt.js Dockerfile (支持 SSR)
    /// </summary>
    private static string GenerateNuxtJsDockerfile(PackageManager packageManager, Dictionary<string, string>? buildArgs)
    {
        var sb = new StringBuilder();
        var pm = PackageManagerDetector.GetExecutableName(packageManager);
        var installCmd = GetInstallCommand(packageManager);

        // Build stage
        sb.AppendLine("# Build stage");
        sb.AppendLine("FROM node:20-alpine AS builder");
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();

        if (buildArgs?.Count > 0)
        {
            foreach (var (key, _) in buildArgs)
            {
                sb.AppendLine($"ARG {key}");
                sb.AppendLine($"ENV {key}=${{{key}}}");
            }
            sb.AppendLine();
        }

        AppendPackageManagerSetup(sb, packageManager);
        sb.AppendLine("COPY package*.json ./");
        if (packageManager == PackageManager.Pnpm)
        {
            sb.AppendLine("COPY pnpm-lock.yaml* ./");
        }
        else if (packageManager == PackageManager.Yarn)
        {
            sb.AppendLine("COPY yarn.lock* ./");
        }
        sb.AppendLine($"RUN {pm} {installCmd}");
        sb.AppendLine();
        sb.AppendLine("COPY . .");
        sb.AppendLine($"RUN {pm} {GetBuildCommand(packageManager)}");
        sb.AppendLine();

        // Production stage
        sb.AppendLine("# Production stage");
        sb.AppendLine("FROM node:20-alpine");
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("ENV NODE_ENV=production");
        sb.AppendLine();
        sb.AppendLine("COPY --from=builder /app/.output ./");
        sb.AppendLine();
        sb.AppendLine("EXPOSE 3000");
        sb.AppendLine("CMD [\"node\", \"server/index.mjs\"]");

        return sb.ToString();
    }

    private static void AppendPackageManagerSetup(StringBuilder sb, PackageManager packageManager)
    {
        if (packageManager == PackageManager.Pnpm)
        {
            sb.AppendLine("RUN corepack enable && corepack prepare pnpm@latest --activate");
        }
        else if (packageManager == PackageManager.Yarn)
        {
            sb.AppendLine("RUN corepack enable");
        }
    }

    private static string GetInstallCommand(PackageManager packageManager)
    {
        return packageManager switch
        {
            PackageManager.Pnpm => "install --frozen-lockfile",
            PackageManager.Yarn => "install --frozen-lockfile",
            PackageManager.Npm => "ci",
            _ => "ci",
        };
    }

    private static string GetBuildCommand(PackageManager packageManager)
    {
        return packageManager switch
        {
            PackageManager.Pnpm => "build",
            PackageManager.Yarn => "build",
            PackageManager.Npm => "run build",
            _ => "run build",
        };
    }
}
