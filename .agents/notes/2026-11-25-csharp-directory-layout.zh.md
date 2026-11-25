---
status: implemented
category: repository
---
# C# 源码目录组织

[English](2026-11-25-csharp-directory-layout.en.md) | 中文

每个目录直接存放的手写 C# 文件最多为 16 个。采用这项约定时，有 5 个产品目录超过 16 个文件。上限若为 24，只会命中其中两个，而桌面壳层、游戏运行时和游戏桌面都已承担多项可区分的职责。超限时，建议审视架构，按职责归拢经常一起修改的文件。Avalonia 视图与对应的后台代码放在一起。

[仓库文件检查](../scripts/check-file-sizes.mjs)统计工作区中仍存在的已跟踪文件和未被忽略的新文件，测试同样适用。分部类和视图后台代码都按实际的 `.cs` 文件计数，子目录独立计算。仓库用 `Generated/` 目录或 `.g.cs`、`.g.i.cs`、`.generated.cs`、`.designer.cs` 后缀标识生成源码。包括生成文件在内的所有受检文件仍然遵守 [1 MiB 大小上限](2026-11-15-csharp-lint.zh.md)。

运行 `mise run check:files` 可查看所有超限目录及其文件数。本地检查和主分支 CI 均执行这条规则。
