namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

/// <summary>
/// 前端项目部署模式
/// </summary>
public enum DeploymentMode
{
    /// <summary>
    /// 本地开发模式 (使用包管理器运行 dev 命令)
    /// </summary>
    LocalDevelopment,

    /// <summary>
    /// 容器模式 - 引用已有镜像
    /// </summary>
    ContainerExisting,

    /// <summary>
    /// 容器模式 - 从 Dockerfile 构建
    /// </summary>
    ContainerDockerfile,

    /// <summary>
    /// 容器模式 - 从 Git 仓库自动构建
    /// </summary>
    ContainerGit,
}

/// <summary>
/// 包管理器类型
/// </summary>
public enum PackageManager
{
    /// <summary>
    /// 自动检测 (根据 lock 文件判断)
    /// </summary>
    Auto,

    /// <summary>
    /// npm
    /// </summary>
    Npm,

    /// <summary>
    /// Yarn
    /// </summary>
    Yarn,

    /// <summary>
    /// pnpm
    /// </summary>
    Pnpm,
}

/// <summary>
/// 前端框架类型
/// </summary>
public enum FrontendFramework
{
    /// <summary>
    /// 未知框架
    /// </summary>
    Unknown,

    /// <summary>
    /// Vite (环境变量前缀: VITE_)
    /// </summary>
    Vite,

    /// <summary>
    /// Next.js (环境变量前缀: NEXT_PUBLIC_)
    /// </summary>
    NextJs,

    /// <summary>
    /// Nuxt.js (环境变量前缀: NUXT_PUBLIC_)
    /// </summary>
    NuxtJs,

    /// <summary>
    /// Create React App (环境变量前缀: REACT_APP_)
    /// </summary>
    CreateReactApp,

    /// <summary>
    /// Vue CLI (环境变量前缀: VUE_APP_)
    /// </summary>
    VueCli,
}
