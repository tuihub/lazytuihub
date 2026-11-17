# UI 框架比较原型

[English](ui-comparison.en.md) | 中文

2026-11-18，确定使用 Avalonia 作为产品 UI 框架。[选型 Note](../../.agents/notes/2026-11-17-mvp-ui-comparison.zh.md) 记录决策与产品接入。[桌面 MVP 指南](desktop-mvp.zh.md) 介绍使用真实 RSS／SQLite 服务的应用。本指南保留两个原型，供复现比较；后续产品 UI 按 Avalonia 推进。各平台的设计体系、Android 与旧 Windows 的支持范围仍单独决定。

## 运行

在仓库根目录使用常规 mise 环境运行：

```sh
mise run demo:ui:avalonia
mise run demo:ui:uno
mise run demo:ui:uno -- --cupertino
mise run demo:ui:avalonia -- --without-plugin
mise run demo:ui:uno -- --without-plugin
```

每个命令以 Release 模式构建选定原型，并将分析器警告视为错误。关闭窗口即可停止运行。两边默认使用 Fluent 主题；Uno 的可选 Cupertino 运行使用其已发布的主题包。

在 macOS 上，`mise run bundle:ui` 会在 `.agents/prototypes/ui-comparison/artifacts/` 下创建本地应用包装。打开 `LazyTuiHub-Avalonia.app` 和 `LazyTuiHub-Uno.app` 即可比较。`mise run bundle:ui:cupertino` 另生成 `LazyTuiHub-Uno-Cupertino.app`。包装使用本机 mise 管理的 .NET 运行时，只用于开发启动，不是可分发的安装包。Windows 和 Linux 使用上述运行命令；本轮尚未验证它们的原生表现。

## 共同场景

两个原型共用内存会话和 600 篇固定的中英文示例文章。该数量只用于比较，不是应用限制。两边实现：

- 三栏阅读器，包括订阅源选择、搜索、文章选择、已读切换和可编辑阅读笔记。
- 使用框架已发布 Flex 面板自动换行的工具栏、明暗切换及短暂的页面淡入动画。
- 工作流画布，包括五个节点、四条贝塞尔连线、捕获指针的节点拖动、滚动条和缩放／重置按钮。图展示 RSS 管线，不执行或编辑产品中持久化的工作流定义。
- 插件页面，包含自己的文本框、按钮和计数器，由冷启动时加载的独立托管 DLL 创建。

示例不抓取订阅、不接入 SQLite，也不修改产品插件契约。已读状态、笔记、节点位置和插件计数只在当前进程中保留。产品已单独接入 Avalonia，见[桌面 MVP 指南](desktop-mvp.zh.md)。

## 源码边界

源码位于[原型目录](../../.agents/prototypes/ui-comparison/)：

| 目录 | 职责 |
| - | - |
| `Shared/` | 两边共用的示例数据、阅读状态、强类型插件契约与加载器、图模型和诊断测量。 |
| `Avalonia/`、`Uno/` | 框架各自的控件、布局、输入处理、动画和应用启动。 |
| `AvaloniaPlugin/`、`UnoPlugin/` | 编译为独立程序集的框架原生插件视图。 |

宿主通过 `ReferenceOutputAssembly="false"` 构建插件项目，仅将入口 DLL 复制到 `plugins/SamplePlugin.dll`。启动时通过默认 `AssemblyLoadContext` 加载该路径，共享框架与契约的类型身份。切换页面保留已加载视图。禁用插件在下一次进程启动生效；文件缺失会保留可见诊断，不会阻止阅读器启动。原型不实现热卸载、私有依赖版本或持久化插件管理。

除共同的窗内导航外，Avalonia 还通过原生菜单 API 添加 Reader／Workflow 入口。Uno 当前只实现窗内导航；这是本次代码的差异，不代表 Uno 无法接入原生菜单。

## 自动验证

```sh
mise run check:ui
```

该可选检查构建两个原型，并运行各自官方的无窗口主机，检查阅读布局与初始选择、搜索结果、共同的订阅源与已读状态、插件视图创建、禁用插件后的启动、缺失 DLL 诊断、Flex 换行与伸缩、图和缩放状态。Uno 同时检查 Fluent 与 Cupertino 资源。Uno 搜索检查会等待异步送达的 `TextChanged` 事件。

原型不加入产品解决方案或默认 CI，保留仓库格式、文件体积及适用的分析器规则；目录内 `.editorconfig` 仅针对布局尺寸字面量豁免魔法数字规则。不修改产品 lint 门槛。

生成的 JSON 写入原型下被 Git 忽略的 `artifacts/` 目录，记录环境、应用 `Loaded` 回调耗时、工作集、托管分配估计和已创建的列表容器数。无窗口测量描述该测试主机，不能据此判断原生首帧、峰值内存、帧率或发布包体积。

## 当前证据

2026-11-17，两边的 Release 构建与无窗口检查均在 macOS ARM64、.NET 11.0 上通过。两边都从独立 DLL 创建了真实的框架原生控件视图，并支持共同的阅读器和画布机制。Uno 额外需要 XAML 资源初始化和异步搜索事件处理；Avalonia 为缩放表面提供了布局变换包装控件。两边都仍需实现节点编辑器的交互逻辑。

随后已实际操作两个 macOS 应用。初始尺寸统一为 1180 × 800 个逻辑单位；Uno 的 AppWindow 使用物理像素，现按其 [macOS 调整尺寸实现](https://github.com/unoplatform/uno/blob/30034eefe42ecf5f3e3fff7d49a05340aaa0152e/src/Uno.UI.Runtime.MacOS/UI/Xaml/Window/MacOSWindowWrapper.cs)，通过 XamlRoot.RasterizationScale 换算。这修正了 2× Retina 屏幕上的原型尺寸差异，不是框架布局缺陷。两个常规窗口的截图均为包含标题栏的 2360 × 1656 像素。

| 原生操作 | Avalonia | Uno Fluent |
| - | - | - |
| 订阅源／搜索、已读状态、独立列表滚动 | 实际操作通过。 | 实际操作通过。 |
| DLL 页面、计数及导航后的状态保留 | 实际操作通过。 | 实际操作通过。 |
| 节点拖动、连线更新、115% 缩放后拖动、重置 | 实际操作通过。 | 实际操作通过。 |
| 约 500 个逻辑单位宽时工具栏换行 | 实际操作通过，随后恢复尺寸。 | 实际操作通过，随后恢复尺寸。 |
| 内容明暗主题 | 实际操作通过。 | 实际操作通过。 |
| 笔记中 Enter 与 Command+F | 多行输入和搜索聚焦正常，另检查了撤销。 | 两个新进程中 Enter 未换行，Command+F 未聚焦搜索，根因待定位。 |
| Workflow → + 后的可访问性树 | 仍能读取控件。 | 两个新进程均退化为 unknown 节点，但图像和指针交互仍正常。 |
| 原生菜单 | 实际通过 View → Workflow 打开画布。 | 原型未实现等价原生菜单，不据此判定框架不支持。 |

键盘和可访问性现象限定于当前原型、运行时与桌面工具；归因到框架前，仍需人工输入、VoiceOver 或独立可访问性检查器交叉验证。Fluent 运行中还出现清除按钮缺字符号，字体资产配置有待核查。

Cupertino 现在显式设置 `WithImplicitStyles = true`，调用其[隐式样式导出逻辑](https://github.com/unoplatform/Uno.Themes/blob/b43eec86d060a991fb24fe5f0c5f24eba2533631/src/library/Uno.Cupertino/CupertinoResources.cs)；先前仅创建资源字典，控件仍沿用 Fluent。检查已补充有效隐式按钮样式查找，不再只验证资源加载。原生截图确认按钮、输入框与字体发生变化，列表仍保留另一套样式。这说明主题覆盖部分控件，不代表完整 macOS 桌面界面。该运行的截图还出现边缘黑区，坐标点击 Clip 后未观察到计数变化；布局、输入注入、命中位置与截图影响尚未区分，完整原生交互检查仍未通过。

两边均检查了中文显示和粘贴；实际输入法组合输入与候选框位置、GPU 帧表现，以及 Android、Windows、旧 Windows 和 Linux 仍未验证。桌面工具超时和早期坐标错误不计作框架失败或应用启动耗时。这些结果支持了采用 Avalonia 的决策。Note 按 MVP 生命周期约定保持 proposed；框架选择已确定，首轮产品接入已实现，更多平台验证仍待完成。

## 交互比较

两个应用使用相同窗口大小，分别执行以下操作：

1. 选择 Design，文章列表应有 200 条；搜索 `workflow`，应有 100 条。选择文章，切换已读状态，离开页面再返回。
2. 分别滚动文章列表和正文。调整窗口大小，检查工具栏换行和三栏布局。
3. 用实际输入法在阅读笔记中输入中文，检查组合输入、候选框位置、选区、复制粘贴与撤销。仅粘贴中文不能验证输入法。
4. 打开 Workflow，拖动节点，放大、缩小、重置并滚动画布。连线应跟随节点移动；缩放后再次检查拖动。
5. 打开 Plugin，编辑片段，点击 Clip a passage，切走再返回，计数应保留。带 `--without-plugin` 重启，阅读器应可用，插件页应解释缺失原因。
6. 切换明暗主题，使用键盘导航，包括 macOS 的 Command+F 或其他系统的 Control+F。分别检查淡入动画和原生菜单。

原生结果与无窗口检查分开记录。未观察到的行为保持待验证，不记作框架失败或通过。
