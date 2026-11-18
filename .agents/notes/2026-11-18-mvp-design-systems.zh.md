---
status: proposed
category: architecture
---
# MVP 设计系统的视觉一致性与维护归属

[English](2026-11-18-mvp-design-systems.en.md) | 中文

## 决策范围

用户于 2026-11-18 人工确认官方组件交互样板正确。这些样板作为本轮参照，不代表 Avalonia 实现已获确认。本 Note 区分应用用法错误、已安装库的限制、可用替代方案，以及平台和渲染差异。[UI 对比 Note](2026-11-17-mvp-ui-comparison.zh.md) 继续记录框架比较和当前桌面实现。

所有 MVP 选型都可以调整。以最终交付为目标，不以保留已写代码、避免替换或已投入的工作作为依据。长期依赖稳定性、未来切库风险和维护归属属于最终架构的取舍；自维护组件库也是正式候选。下文保留原型审查，作为本轮实施依据。

## 产品实现

桌面宿主维护外壳、检查器和 RSS 插件共用的组件角色。Components.axaml 定义导航行、内容列表行、选中与焦点指示，以及文字层级。各设计适配层提供尺寸、配色和字体。插件页面把角色用于标准 Avalonia 控件，其余控件沿用当前基础主题。以后替换基础库时，可以在相同页面上复查同一套操作。

Fluent 以桌面 Fluent 为目标，采用 Avalonia 官方主题及其强调按钮。macOS 采用 Devolutions 的控件行为，由项目维护紧凑的侧栏行，间距和层次参照 AppKit。本机无窗口截图中的 macOS 字体实际解析为 Helvetica Neue。原生合成效果和系统字体的精确渲染仍待实机验证。

Material 通过 CCSWE 提供文本编辑、选择器、菜单和浮层等标准控件。项目维护填充、描边、文本和图标按钮模板、从指针落点扩散的波纹、键盘焦点环、M3 语义配色及阅读排版。Roboto Regular 和 Medium 来自 Uno.Fonts.Roboto 2.10.1，随附 OFL 许可。页面和 CCSWE 控件使用同一套浅深色语义色板。业务页面在三个设计中使用相同的角色。

按钮采用 waiter 按操作名称选择图标的做法。页面引用添加、刷新、导航、返回、关闭和外观六种语义资源，宿主按当前设计解析矢量轮廓，保留原始画框和留白，图标随按钮前景色变化。Fluent 使用微软 Regular 20，macOS 使用 Cupertino Icons 1.0.9，Material 使用 Material Symbols Outlined（光学尺寸 24、字重 400、未填充、grade 0）。独立图标分别以 20、18 和 24 个逻辑单位绘制；文字旁的图标分别为 20、16 和 18。Material 工具栏按钮在 48 的点击区域内绘制 40 的状态层。各主题的图标字典保留上游名称，发行时随附许可。[waiter 操作映射](https://github.com/tuihub/waiter/blob/ae1ae6f349eef99a8a300ed9276c319683ef5797/packages/universal_ui/lib/src/icons.dart)、[Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons)、[Cupertino Icons](https://pub.dev/packages/cupertino_icons/versions/1.0.9)、[Material Symbols 源码版本](https://github.com/google/material-design-icons/tree/10ecc14790612ca8fd164e3f4ffe9c16fc50d5b7/variablefont)。

产品无窗口场景在三套设计下操作订阅面板，验证键盘焦点和激活，记录宽窄布局、外观弹层和深色模式。测试还验证 Material 主按钮实际解析 Roboto，并响应指针按下和释放。JSON 快照包含实际解析的主字体。独立的五套组件库样板保留原始库的外观，供将来升级时对照。

## 已验证的参照与方法

[样板工具](../prototypes/design-baselines/README.zh.md) 使用 Microsoft Fluent UI React、Google Material Web 和 Apple 原生 AppKit。前两者是 Web 参照。Fluent Web 不等同于 WinUI；Material 参照采用 Material 3，此前使用的 Material.Avalonia 则沿用较早的设计体系。AppKit 是原生平台参照，不是另一套 Skia 渲染器。

Web 截图改用 Chromium 软件渲染后，已生成 24 组 PNG／JSON，覆盖浅深色下的默认、悬停、输入焦点、弹层、下拉菜单和窄屏。这些图用于观察状态，不构成动画符合性测试。AppKit 视图缓存截图仍会丢失合成内容，不能作为图片基线；用户确认的是交互样板。

独立 Avalonia 程序直接加载五个库，不加载产品模板。每个库使用独立进程，执行浅深色、按钮激活、文本输入、打开 ComboBox 后的键盘选择和弹层打开；捕获默认、悬停、键盘焦点、按下和弹层，共 50 组 PNG／JSON。JSON 记录声明字体与解析后的主字体，不逐字记录回退字形。另外挂载、移除四个主题实例，在垃圾回收后检查弱引用是否仍可达。Devolutions 需要完整的 BeginInit／EndInit 生命周期，与 XAML 初始化一致；样板初次省略此步骤造成的模板缺失属于工具用法错误，不是库不提供该控件。

另一次临时实验将 FluentAvalonia 和 CCSWE 替入真实桌面宿主，现有 5 项桌面测试全部通过。这只证明已覆盖操作可以运行，不代表视觉完全一致或选型已确定。独立样板项目保留两个候选库供比较。

## 对上一轮桌面原型的检查结果

| 部分 | 证据与归类 | 最终实现应当如何处理 |
| - | - | - |
| 按钮层级与交互 | 应用的 primary 类只改变字重。QuietButton 用统一 Border 替换库模板，覆盖了库提供的波纹、层次、焦点、内边距和字体规则。现有 Fluent／macOS 提供 accent；Material.Avalonia 提供 MaterialOutlineButton、MaterialFlatButton、MaterialIconButton。独立样板验证了强调、描边和文本变体的显示及输入；图标变体来自源码检查。 | 属于接入不完整或用法错误，不是生态缺少按钮能力。采用外部库时应使用其真正的变体；自维护时则须明确实现完整的所需状态。 |
| 语义配色 | 三套适配器写死了相似的中性色、悬停色、选中色和深色强调色，普通控件却使用库自己的资源。Material 页面强调色与配置的 Indigo／Teal 控件不一致。 | 每种设计使用连贯的表面、文字、强调、选中和浮层配色，不能靠给少量元素换色来代表设计系统。 |
| 导航与列表 | NavigationItem、ArticleCard 覆盖了原有选中模板。FluentAvalonia 已提供选中指示条；Devolutions 默认 ListBox 采用勾选标记，并非 AppKit 的侧栏行。 | 普通列表、侧栏导航和文章卡片需要各自的语义。应用自行编排没有问题，但其样式应实现相应设计和状态；不能把菜单式列表直接视为原生侧栏。 |
| 排版 | 共用标题强制指定字号和字重，没有采用各设计的文字角色。本机独立样板中，两种 Fluent 的普通正文解析为 Helvetica，Devolutions 解析为 Helvetica Neue，Material.Avalonia 为 Roboto，CCSWE 为 DM Sans 9pt；实际字重均为 Normal。 | 明确文字角色并核对实际字体。仅凭看起来偏粗，不能认定误用了粗体。Mac 主题的字体回退与 AppKit 系统字体存在差异。 |
| Material 代际 | Material.Avalonia 有真实的文本／描边按钮、波纹和排版，但这些能力并不共同构成已确认的 Material 3 参照。CCSWE 已提供 M3 胶囊按钮、色调容器、文字角色和导航主题。 | Avalonia 生态并非没有 M3。若目标是 M3，当前包属于选型不匹配；仅调整圆角不构成迁移。 |
| Fluent 平台变体 | FluentAvalonia 的 WinUI 风格控件与微软 React 样板在尺寸、输入框、开关和排版上存在区别。 | 比较对应的角色和交互状态，不能要求 Web 与桌面逐像素相同；应明确产品选择 Web Fluent 还是桌面 Fluent。 |
| macOS 原生合成 | Devolutions 绘制 Avalonia 模板，提供仿原生控件和 Liquid Glass 资源，未覆盖部分回退至 Fluent；它不会将 Button 变成 NSButton，或将 Flyout 变成 NSPopover。 | 原生窗口、合成效果、字体光栅化、材质和 AppKit 控件行为属于单独的平台工作。没有已验证的包证明完整原生等价；这不等于 Avalonia 无法集成原生视图。 |

本轮没有发现 Avalonia 框架本身无法提供当前所需按钮、输入框、列表、主题切换、焦点状态或响应式布局的证据。不能把某个包缺少功能推广成整个生态不支持；也不能把有一个主题包推广成完整符合设计系统。

## 替代方案与维护证据

| 实现 | 已验证的有效能力 | 维护与视觉边界 |
| - | - | - |
| Avalonia.Themes.Fluent 12.1.6 | Avalonia 官方主题；控件与浅深色场景通过；移除后 0/4 主题实例仍可达。 | 并非微软 Fluent React 的实现。当前应用也覆盖了它原本提供的部分能力。 |
| Material.Avalonia 3.21.0 | 已有控件覆盖、按钮变体和波纹模板；释放后 0/4 实例仍可达。 | 包版本为 3.x 不代表 Material 3，保留该库不能满足 M3 目标。 |
| Devolutions.AvaloniaTheme.MacOS 2026.11.5 | Mac 风格控件和状态；由实际使用该库的公司发布；调用厂商清理后 0/4 实例仍可达。2026-11-17 仍有仓库活动。 | 上游仍标记为开发中。控件覆盖与回退、应用级壁纸资源、字体回退及原生等价性都需处理；厂商需求与本项目不完全相同。 |
| FluentAvaloniaUI 3.1.0 | WinUI 风格控件、选中指示条、透明及强调按钮；独立场景和产品替换实验通过。 | 主题订阅平台配色变化；所审源码没有对应退订，实验中移除后的 4/4 实例仍可达。反复替换需要解决生命周期。README 也未承诺支持 Windows 7／8；本项目有条件的旧系统目标仍未验证。 |
| CCSWE.Avalonia.Material 12.2.1 | 已发布的 Avalonia 12 M3 实现；按钮大体形状和状态层、浅深色及控件交互在样板中有效；0/4 实例仍可达。 | 仓库创建于 2026-06-04，2026-11-16 仍活跃：运行历史较短，不代表已停止维护。采用自有字体和色板；所审按钮模板使用状态层透明度，没有空间扩散波纹；实测键盘焦点为 Avalonia 虚线矩形。默认效果不完全等同于 Google 样板。 |
| jgwy/material3 源码 | 独立 M3 项目，具有动态配色、波纹／焦点基础设施和控件映射。 | README 明确尚无稳定发布，路线图仍列出未完成的核心覆盖和平台验证。所见最后推送为 2026-08-09。本轮仅审查源码，未运行，不将其列为已验证的替代实现。 |

弱引用实验只说明活跃 headless 应用中已移除主题对象的保留情况，不估算泄漏字节，也不证明所有原生生命周期。0/4 不能认证完全没有泄漏。仓库活动和发布历史可以辅助判断维护能力；星数、有包可装或少量场景通过，都不能证明长期稳定。

## 自维护视觉组件

可行的自维护方案继续使用 Avalonia 的控件行为：Button 命令和输入、TextBox 编辑与 IME、选择、焦点路由、布局、虚拟化和自动化节点；项目维护设计 token、排版、ControlTheme、必要模板部件、交互状态、浮层和动效，无需重造 UI 引擎。仍须验证模板是否保留快捷访问键、焦点指示、点击区域、选中色、弹层边界、缩放和禁用状态。复用基础控件不自动证明这些部分正确。

首批交付范围来自真实阅读器和检查页面：按钮和图标、文本输入、选择器、列表／导航、标签页、文字、滚动、分栏、进度、菜单和弹层；随交付功能扩展。额外插件控件的回退方式需要明确，部分自维护主题不能宣称任意控件都保持设计一致。

| 方向 | 长期收益 | 长期责任 |
| - | - | - |
| 第三方主题加明确的适配层 | 可以共享上游缺陷修复、控件扩充和平台工作。 | 跟随多个维护者及其设计目标；适配可能绑定模板部件、全局资源和生命周期。大量覆盖最终可能成为未明说的分叉。 |
| 基于 Avalonia 控件自维护所需视觉层 | 由同一方定义视觉一致性、跨设计切换、公开语义和修复；业务页面无需依赖多个厂商的专用 API。 | 跟进 Avalonia 模板契约、设计系统变化、字体行为和跨平台交互；库较小不代表这些工作消失。 |
| 对有用模板维护经过整理的分叉 | 复用已检查的实现，同时掌握修改权。 | 保留许可证和来源，主动审查上游变更，承担分叉维护。复制大主题不自动带来质量，也不会消除对其契约的依赖。 |

本轮按上述分工实施。依赖库的长期稳定性和三套视觉规则的长期维护量，仍需在实际升级中观察。

## 来源

- [Avalonia Fluent 按钮实现](https://github.com/AvaloniaUI/Avalonia/blob/12.1.6/src/Avalonia.Themes.Fluent/Controls/Button.xaml)。
- [Material.Avalonia 按钮变体和波纹实现](https://github.com/AvaloniaCommunity/Material.Avalonia/blob/v3.21.0/Material.Styles/Resources/Themes/Button.axaml)。
- [Devolutions 覆盖与回退说明](https://github.com/Devolutions/avalonia-extensions/blob/7ef3ab09b1696938009c7923efda44490a848445/src/Devolutions.AvaloniaTheme.MacOS/README.md)、[列表模板](https://github.com/Devolutions/avalonia-extensions/blob/7ef3ab09b1696938009c7923efda44490a848445/src/Devolutions.AvaloniaTheme.MacOS/Controls/ListBoxItem.axaml)及[字体资源](https://github.com/Devolutions/avalonia-extensions/blob/7ef3ab09b1696938009c7923efda44490a848445/src/Devolutions.AvaloniaTheme.MacOS/Accents/ThemeResources.axaml)。
- [FluentAvalonia 发布版本的主题实现](https://github.com/amwx/FluentAvalonia/blob/215ee0481e8b0e6d396cbe2a0f33dc791c5646ab/src/FluentAvalonia/Styling/Core/FluentAvaloniaTheme.axaml.cs)及[包要求](https://www.nuget.org/packages/FluentAvaloniaUI/3.1.0)。
- [CCSWE 发布版本的按钮实现](https://github.com/CCSWE-Avalonia/CCSWE.Avalonia.Material/blob/b75cfa8a65ecb02560888a57ea517cf5bf3bf284/src/CCSWE.Avalonia.Material/Controls/Button.axaml)、[字体](https://github.com/CCSWE-Avalonia/CCSWE.Avalonia.Material/blob/b75cfa8a65ecb02560888a57ea517cf5bf3bf284/src/CCSWE.Avalonia.Material/Fonts.axaml)及[仓库](https://github.com/CCSWE-Avalonia/CCSWE.Avalonia.Material)。
- [jgwy/material3 当前覆盖与发布边界](https://github.com/jgwy/material3/blob/main/README.md)及[路线图](https://github.com/jgwy/material3/blob/main/ROADMAP.md)。
