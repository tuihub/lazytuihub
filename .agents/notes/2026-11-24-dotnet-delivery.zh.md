---
status: implemented
category: repository
---
# .NET 工具链与产品交付

[English](2026-11-24-dotnet-delivery.en.md) | 中文

开发与 CI 使用 mise 管理的 .NET 10 LTS SDK。[global.json](../../global.json) 锁定最后一组 SDK 功能版本 `10.0.4xx` 中的补丁版本。初始版本为 SDK 10.0.404 和运行时 10.0.15。微软支持 .NET 10 至 2028 年 11 月 14 日。本基线继承此前替代[仓库初始化](2026-11-15-repository-initialization.zh.md)和 [Proto API 契约](2026-11-15-protobuf-api-contract.zh.md)中 SDK 与目标框架选择的决定。前后两代运行时的支持截止日期相同，原有包管理、契约和生成约定继续生效。[微软支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) · [SDK 功能版本支持周期](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs#lifecycle)

[Directory.Build.props](../../Directory.Build.props) 为解决方案、插件、产品测试和 Avalonia 样板统一指定 `net10.0`，编译器默认使用 C# 14。Uno 比较原型使用 `net10.0-desktop`。API 生成器和 Docker 工作节点测试使用同一代运行时。

产品正式发布仅交付面向目标操作系统和架构的自包含 JIT 宿主及远程 CLI。每个包自带一份运行时。宿主在冷启动时将托管业务插件加载到同一运行时，远程 CLI 通过 HTTPS 连接宿主。开发使用 mise 提供的共享运行时。维护者更新 SDK 补丁后，重新发布各平台的产品包来交付运行时修复。[发布指南](../../docs/development/release.zh.md)列出命令和产物位置。

Windows 7 使用常规 Windows x64 包。[Win7 评估](2026-11-23-mvp-win7-cli.zh.md)记录了项目验证的功能与前置依赖。Win7 超出微软的操作系统支持范围。
