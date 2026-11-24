---
status: archived
category: repository
superseded_by:
    - 2026-11-24-dotnet-delivery.zh.md
---
# 仓库 .NET LTS 基线

[English](2026-11-24-dotnet-lts.en.md) | 中文

开发、CI 与发布统一使用 .NET 10 LTS 最后一组 SDK 功能版本 `10.0.4xx`。由 [global.json](../../global.json) 指定 mise 安装的确切补丁版本。迁移时使用 10.0.404，配套运行时为 10.0.15。微软支持这一 LTS 版本至 2028 年 11 月 14 日。后续在同一功能版本组内更新补丁，通过重新发布自包含包和 Native AOT 包向用户交付运行时修复。[微软支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) · [SDK 功能版本支持周期](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs#lifecycle)

本决定替换[仓库初始化](2026-11-15-repository-initialization.zh.md)及 [API 协议](2026-11-15-protobuf-api-contract.zh.md) Note 中的 .NET 11 SDK 和目标框架选择。两个版本的支持截止日期相同。采用 LTS 后，常规发布与 Win7 发布共用同一代运行时，本地宿主能在冷启动时加载托管插件。既有包管理、API 契约与代码生成约定继续有效。

[Directory.Build.props](../../Directory.Build.props) 统一声明 `net10.0`，编译器据此选择 C# 14。解决方案、插件、产品测试和 Avalonia 样板均继承该目标，Uno 原型使用桌面专用的 `net10.0-desktop`。Docker 工作节点镜像和 API 生成器使用同一代运行时。[Win7 评估](2026-11-23-mvp-win7-cli.zh.md)覆盖带托管插件的自包含 JIT 宿主和独立 Native AOT CLI。Win7 兼容性由项目自行验证，超出微软的操作系统支持范围。
