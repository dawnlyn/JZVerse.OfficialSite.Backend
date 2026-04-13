# 九州寰宇官网后端

基于 .NET 10 的模块化微服务架构后端系统，采用自研微服务治理框架，为个人官网提供后台服务支撑。

> 本文档似乎在Vibe Coding时，不留神被AI修改了，但AI的描述并没有太大的问题

# TODO:
- 现在微服务基建已经基础功能可用，打算迁移至独立项目，同时完善业务模块，计划制定中，开发人员目前整项目仅我一人，求职(计划换工作)，坐标宁波，wx:Dawnlyn-Lolico


[todo](./todo.md)

## 技术栈

- **框架**: .NET 10 / ASP.NET Core Web API
- **微服务框架**: JZVerse.MicroHuaxia（自研）
- **数据访问**: EF Core / Dapper（支持 PostgreSQL、MySQL、SQLite、MongoDB）
- **缓存**: Garnet
- **消息队列**: 自研消息队列（支持 InProc/TCP/gRPC 协议）
- **服务通信**: gRPC / JSON-RPC / HTTP
- **可观测性**: OpenTelemetry

## 项目结构

```
src/
├── JZVerse.MicroHuaxia/          # 自研微服务治理框架
│   ├── ServiceDiscovery/         # 服务注册发现中心
│   ├── ConfigCenter/             # 配置中心
│   ├── Gateway/                  # API 网关
│   ├── ServiceCommunication/     # 服务通信
│   ├── MessageQueue/             # 消息队列
│   ├── Saga/                     # 分布式事务
│   └── Dashboard/                # 微服务仪表盘
├── JZVerse.DataAccess/           # 数据访问层
│   ├── Abstractions/             # 抽象接口
│   ├── Core/                     # 核心实现
│   ├── Caching.*/                # 缓存集成（EFCore/Dapper）
│   └── [数据库驱动]/              # PostgreSQL/MySQL/SQLite/MongoDB
└── JZVerse.Business/             # 业务层
    ├── Abstractions/             # 业务抽象
    ├── Infrastructure/           # 基础设施
    ├── Central/                  # 能力中心
    │   ├── Security/             # 统一安全中心
    │   ├── User/                 # 统一用户中心
    │   └── File/                 # 统一附件中心
    └── Business/                 # 业务域
        ├── Blog/                 # 博客系统
        ├── Project/              # 项目公示
        ├── Academy/              # 在线教育
        └── Dashboard/            # 后台管理

tests/                            # 测试项目
├── ServiceDiscovery/             # 服务发现测试
├── ConfigCenter/                 # 配置中心测试
├── Gateway/                      # 网关测试
├── ServiceCommunication/         # 服务通信测试
├── MessageQueue/                 # 消息队列测试
├── Saga/                         # 分布式事务测试
└── Business/                     # 业务层测试
```

## 核心模块

### JZVerse.MicroHuaxia

自研微服务治理框架，提供以下核心能力：

- **服务注册发现**: 支持配置驱动的自动注册/发现，提供主动探测和被动心跳两种健康检查机制
- **配置中心**: 统一配置管理，支持热更新
- **API 网关**: 请求路由、负载均衡、接口鉴权、限流熔断
- **服务通信**: 支持 gRPC、JSON-RPC、HTTP 多种通信协议
- **消息队列**: 轻量级消息队列，支持 InProc、TCP、gRPC 协议
- **分布式事务**: 基于 Saga 模式的分布式事务协调
- **微服务仪表盘**: 服务状态监控与可视化

### JZVerse.DataAccess

统一数据访问层，特性包括：

- 支持多种数据库（PostgreSQL、MySQL、SQLite、MongoDB）
- 集成 EF Core 和 Dapper
- 内置缓存支持

### JZVerse.Business

业务层按职责划分为两大类：

**能力中心（Central）**
- Security: 接口鉴权、数据加密、操作审计、安全防护
- User: 管理员账号、访客统计、用户行为日志
- File: 文件上传、存储、预览、下载

**业务域（Business）**
- Blog: 博客系统
- Project: 项目公示
- Academy: 在线教育
- Dashboard: 后台管理

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 14+（或其他支持的数据库）
- Garnet（缓存服务）

## 快速开始

```bash
# 克隆仓库
git clone https://gitee.com/jzverse/JZVerse.OfficialSite.Backend.git

# 还原依赖
dotnet restore

# 构建项目
dotnet build

# 运行测试
dotnet test
```

## 本地开发

项目支持强本地开发与调试能力，强调服务编排、环境隔离、热重载、可观测性集成等本地优先的开发体验。

## Git 仓库

| 平台 | 地址 |
|------|------|
| Gitee（主） | https://gitee.com/jzverse/JZVerse.OfficialSite.Backend |
| 自建 GitLab | https://git.jzverse.com.cn/jzverse.com.cn/OfficialWebSite/JZVerse.OfficialSite.Backend |
| GitHub | https://github.com/dawnlyn/JZVerse.OfficialSite.Backend |
| GitLab | https://gitlab.com/Dawnlyn/JZVerse.OfficialSite.Backend |
| GitCode | https://gitcode.com/dawnlyn/JZVerse.OfficialSite.Backend |
| CNB | https://cnb.cool/dawnlyn/JZVerse.OfficialSite.Backend |

## 许可证

当前采用 MIT ，保留后续修改许可协议的权利。
