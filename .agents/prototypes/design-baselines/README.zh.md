# 官方组件样板

[English](README.en.md) | 中文

这个独立实验为阅读器组装官方设计参考和独立的 Avalonia 实现样板，不修改产品，也不证明当前 Avalonia 主题正确。页面编排属于本地样板；控件、排版预设、交互状态和 token 才来自官方实现。

## 验证状态

用户于 2026-11-18 确认了官方交互样板。Web 截图已通过 Chromium 软件渲染跑通，生成 24 组 PNG/JSON。原生 AppKit 视图缓存截图仍有缺陷，**不能作为图像基线**。独立的 Avalonia 程序从五套库生成 50 组 PNG/JSON，不加载产品样式，验证输入并检查主题生命周期。[设计系统审查](../../notes/2026-11-18-mvp-design-systems.zh.md) 记录结论和维护取舍。这些样板不证明像素一致，也不替代平台验证。

## macOS 原生游戏库窗口

在 Mac 上运行下面的命令，可以打开含两款游戏的 AppKit 窗口。关闭窗口即可退出。样板采用 NSSplitViewController 侧栏、带原生搜索项的 NSToolbar 和 SwiftUI 书架，与产品自查共享两个游戏名称、封面和缺图场景。它用于对照整窗布局，业务操作仅作展示。把产品切到 Games，在相同窗口宽度和明暗模式下比较。

```sh
mise -E development run render:design-baselines:macos-library --show
```

省略 `--show` 时，工具在后台记录以点为单位的尺寸，用 ScreenCaptureKit 单独截取自身窗口的浅色和深色画面，输出到 `.local/design-baselines/`。运行需要 macOS 14.4 或更新版本及 Xcode 命令行工具。系统合成截图包含标题栏和侧栏；采集失败时，工具会写入明确标注的 `*.view-cache.png` 诊断图。此时应打开交互窗口核对材质。原生控件的外观取决于本机系统和链接的 SDK。[Apple 的 AppKit 窗口与侧栏演示](https://developer.apple.com/videos/play/wwdc2025/310/)

## Windows 与 Android 原生导航

在 Windows 上打开 WinUI 3 Gallery 的 NavigationView 页面，选中 Auto 后调整窗口宽度。在中等宽度下展开紧凑侧栏，观察展开前后的正文位置。[微软 NavigationView 示例](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/navigationview)

在 Android 上安装官方 Material Components Catalog APK。打开 Navigation Bar → Label Visibility Demo，添加第四个入口并选择 Labeled。再打开 Navigation Rail → Demo，添加第四个入口。对照图标、标签、选中指示器和入口间距。将截图像素数除以模拟器的密度倍数，即可比较逻辑尺寸。本次使用 Android 16、420 dpi，每 dp 对应 2.625 像素。[Material Catalog 1.14.0](https://github.com/material-components/material-components-android/releases/tag/1.14.0)

## 运行

使用仓库通过 mise 管理的 Node.js 和 pnpm。样板单独维护依赖锁文件，允许 esbuild 执行必要的安装脚本，不重复声明运行时版本。

```sh
mise -E development run setup:design-baselines
mise -E development run render:design-baselines:web
mise exec -- node .agents/prototypes/design-baselines/baseline.mjs render macos
mise -E development run render:design-baselines:avalonia
mise -E development run run:design-baselines
```

`setup` 安装局部依赖，将 Playwright 的 Chromium headless shell 下载到 `.local/design-baselines/browsers/`，将 Google 提供的 Roboto 字体缓存到 `.local/design-baselines/web/fonts/`，并在 `sources.json` 中记录字体来源。之后渲染使用本地资源。`serve` 输出回环地址，可交互查看 Fluent、Material 官方参考和 Avalonia 图片对照，按 Ctrl+C 结束。对照页支持切换浅色／深色及五种状态，并提供指标链接。不带目标的 `render` 运行 Web、macOS 和 Avalonia 截图。原生目标需要 macOS、可用的桌面会话和 Xcode 命令行工具；运行时会短暂显示自己的窗口，然后关闭。

构建原生目标后，也可以交互运行：

```sh
.local/design-baselines/appkit-baseline .local/design-baselines --interactive
```

原生交互窗口保留文本编辑、选择器、开关、弹层及按钮行为。检查完毕后结束进程。这些入口均不依赖 computer use 工具。

## 对照范围

| 样板 | 官方实现 | 可对照内容 | 边界 |
| - | - | - | - |
| Fluent | Microsoft Fluent UI React v9，`webLightTheme` / `webDarkTheme`，官方文字预设和图标 | Fluent 2 **Web** 的控件尺寸、排版、选择、焦点、悬停和弹层 | 不等同于 WinUI 或 Windows 原生渲染。macOS 上默认 Segoe UI 字体栈可能回退到本机字体，JSON 记录实际字体。当前官方 Nav 指南将其标为 Preview。 |
| Material | Google 的 `@material/web`、官方 type scale、Roboto 和 Material color utilities | 已实现的 Material 3 Web 按钮、文本框、选择器、标签页、列表、对话框及状态 | Material Web 处于维护模式，不代表完整的新 Material 3 Expressive，也不是 Material 2。包中缺少正式导航抽屉和卡片组件，样板使用真实标签页和列表，不自行仿造缺失组件。 |
| macOS | Apple AppKit 的 `NSButton`、`NSPopUpButton`、`NSTextField`、`NSTableView`、`NSSegmentedControl`、`NSSwitch`、`NSBox`、`NSPopover` | 记录的系统版本上的原生控件及系统语义字体、颜色 | 当前视图缓存 PNG 丢失合成内容，侧栏选中行发黑，弹层全黑，不能据此判断视觉正确。尚未验证原生悬停、展开的选择器菜单、窗口边框、模糊和阴影。 |

包版本只在局部依赖声明和锁文件中维护。原生实现来自本机系统 SDK，输出记录实际系统版本。包锁不能固定系统字体、浏览器光栅化、系统强调色和辅助功能设置。

## 样板与指标

Web 入口捕获两种主题下的 1120 CSS 像素布局和 430 像素布局，以及默认、悬停、输入焦点、展开弹层和展开选择器状态。默认场景包含已选标签页或导航、勾选控件和禁用按钮。CSS 仅编排样板，不替换控件排版、圆角、内边距或状态样式。Material 页面背景采用生成的语义颜色 token。

每张成功生成的 Web PNG 都配有 JSON，记录控件及 shadow DOM 的尺寸位置、CSS 字体、字号、字重、行高、颜色、边框、间距、焦点、悬停、禁用状态、主题 token、实际平台字体、浏览器版本和依赖版本。自定义元素外层的字体值不一定代表内部文字，需要查看记录的 shadow 节点。AppKit JSON 记录以 point 为单位的位置尺寸、系统字体描述及 traits、语义 sRGB 颜色、原生状态、有效外观，以及窗口是否真正成为 key window。发出设置焦点的调用，不代表系统已激活窗口。

原生入口目前生成四组主视图 PNG/JSON 和两张弹层 PNG。在已测试的 macOS 26.6.2 会话中，`keyWindow` 为 false。这些文件保留调试证据，不能将存在缺陷的图片当成 golden files。即使字重为 normal，不同字体和渲染器仍可能造成偏粗观感，不能仅凭观感诊断字重设置。

Avalonia 程序加载 Avalonia Fluent、Devolutions macOS、Material.Avalonia、FluentAvalonia 和 CCSWE Material 3。每套库在独立进程中运行，分别在浅色和深色下操作按钮、文本、选择器和弹层，捕获默认、悬停、焦点、按下和弹层状态。输出位于 `.local/design-baselines/web/avalonia/`。JSON 记录控件边界、焦点、请求和解析后的主字体，不逐字记录回退字形。样板的标题／正文字号用于页面编排，不证明组件库的完整排版尺度符合设计规范。

每个进程还挂载、移除四个新主题实例，调用可用的释放和库清理入口，执行垃圾回收，并将弱引用可达性写入 `*-retention.json`。它用于发现运行中的无窗口应用仍持有的主题对象，不测量堆增长，也不证明完全没有泄漏；工具不会仅因保留数量不同就判定组件库测试失败。

没有新增默认 CI 任务、快照门禁、容差或基准更新审批。应对照产品图像及指标中相同的角色和状态，不强制所有设计采用相同密度或布局。捕获稳定状态不验证动效和动画时序。

## 官方来源

- [Microsoft Fluent 2 React 组件目录](https://fluent2.microsoft.design/components/web/react)、[文字预设](https://fluent2.microsoft.design/components/web/react/core/text/usage)、[Nav 指南](https://fluent2.microsoft.design/components/web/react/core/nav/usage)及[官方 React Storybook](https://react.fluentui.dev/)。
- [Microsoft Fluent UI 源码](https://github.com/microsoft/fluentui)。
- [Google Material Web 源码与维护状态](https://github.com/material-components/material-web)、[组件目录](https://material-web.dev/)、[排版配置](https://github.com/material-components/material-web/blob/main/docs/theming/typography.md)及 [Material color utilities](https://github.com/material-foundation/material-color-utilities)。
- [Google Material Icons 源码及许可证](https://github.com/google/material-design-icons)，用于官方 `md-icon` 控件中的两个 SVG 图标；[Roboto 字体仓库](https://github.com/googlefonts/roboto-3-classic)。
- [Apple macOS 设计指南](https://developer.apple.com/design/human-interface-guidelines/designing-for-macos)、[排版指南](https://developer.apple.com/design/human-interface-guidelines/typography)及 [AppKit 文档](https://developer.apple.com/documentation/appkit)。

上述来源提供上游实现和指南。第三方 Avalonia 主题仍是需要在对应范围内接受检查的实现。
