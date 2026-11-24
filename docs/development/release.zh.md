# 发布 LazyTuiHub

[English](release.en.md) | 中文

开发时用 `mise run run:desktop` 或 `mise run run:cli` 启动。以下命令为当前机器构建两个可分发包。

```sh
mise run release
```

指定 .NET 运行时标识可面向其他系统或架构构建，例如 `mise run release win-x64`、`mise run release linux-arm64` 或 `mise run release osx-arm64`。从 `artifacts/release/<rid>/` 复制所需的完整包。每个包自带 JIT 运行时，用户无需安装 mise、.NET SDK 或共享运行时。

| 产品包 | 启动入口 |
| --- | --- |
| Windows、Linux 宿主 | 运行 `host/` 内的可执行文件。使用 `--desktop` 打开阅读器，使用 `--cli` 执行本地命令。保留同目录的 `plugins/`。 |
| macOS 宿主 | 打开 `LazyTuiHub.app` 进入阅读器。运行 `LazyTuiHub.app/Contents/MacOS/LazyTuiHub --cli` 执行本地命令。发布命令在本地组装应用包，Developer ID 签名和公证属于后续分发步骤。 |
| 远程 CLI | 运行 `cli/` 内的可执行文件，传入 `--endpoint https://SERVER`。业务插件由远程宿主提供。 |

维护者更新 SDK 补丁并重新发布，用户随产品更新获得运行时安全修复。详见[交付决策](../../.agents/notes/2026-11-24-dotnet-delivery.zh.md)和 [Windows 7 前置依赖](../../.agents/notes/2026-11-23-mvp-win7-cli.zh.md)。
