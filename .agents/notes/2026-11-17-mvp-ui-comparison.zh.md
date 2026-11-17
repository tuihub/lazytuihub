---
status: proposed
category: architecture
---
# MVP UI 框架选型与桌面接入

[English](2026-11-17-mvp-ui-comparison.en.md) | 中文

## 范围与决策

2026-11-18，用户在源码／生态调查和原生原型比较后，确定使用 Avalonia 作为产品 UI 框架。后续产品 UI 按 Avalonia 推进，Uno 仅保留在比较原型中。该 Note 根据 [MVP 约定](../../AGENTS.md) 保持 proposed，以便所有 MVP Note 在合入主分支前自由编辑；这一生命周期状态不表示框架选择尚未确定。

比较已验证下一轮需要的阅读器、流式工作流画布交互和 DLL 视图机制。Avalonia 在 macOS 上观察到的输入、可访问性树保留和原生菜单表现支持本次选择。Uno 的未决现象仍限定于当前原型与工具；采用 Avalonia 无需先证明这些现象属于框架缺陷。

[比较指南](../../docs/development/ui-comparison.zh.md) 提供命令、源码边界、检查范围和相同的交互操作。代码位于 `.agents/prototypes/ui-comparison/`，不加入产品解决方案或默认 CI。可选的 `check:ui` 将分析器警告视为错误构建，并运行官方无窗口主机。已有产品门槛保持不变；原型仅针对布局尺寸字面量豁免魔法数字规则。

## 比较原型实现

两边共用包含 600 篇中英文文章的 RSS 三栏阅读示例、阅读状态、图位置和强类型页面插件契约。数量是比较工作量，不是应用限制。各自的控件实现工具栏换行、页面淡入、明暗主题、可拖动缩放的五节点画布和独立托管 DLL 页面。图展示连接，不执行产品工作流。

宿主通过 `ReferenceOutputAssembly="false"` 构建插件，将入口 DLL 复制为 `plugins/SamplePlugin.dll`，通过默认程序集上下文冷启动加载，共享框架与契约类型。导航保留已加载视图的状态。实现缺失或禁用时，阅读器与诊断仍可用。不实现热卸载或私有依赖版本求解。

实验性 UI 契约不修改[产品插件契约](2026-11-17-mvp-plugins.zh.md)。示例数据仅保留在当前进程，不读取或修改产品工作区。SQLite、产品路由、持久化和完整图形工作流编辑器不属于本轮比较。版本记录在原型构建配置中，不替换仓库 SDK 基线。

## 已观察证据

2026-11-17，Avalonia 12.1.6／Labs.Panels 12.1.0 和 Uno 7.0.65／Toolkit 11.0.1 的 Release 构建与自动检查均在 macOS ARM64、.NET 11.0 上通过。Uno 另运行了 Cupertino 9.0.1。检查覆盖初始阅读布局、搜索、按视口创建列表容器、共同的订阅源与已读状态、DLL 创建的视图、禁用插件后的启动、缺失 DLL 诊断、Flex 换行与伸缩、图和缩放状态。

固定无窗口视口中，600 条数据对应 Avalonia 13 个、Uno Fluent 24 个已创建列表容器，说明该场景下两边都没有一次性创建全部容器。缓冲策略不同，不能据此做性能排名。Uno 需要 XAML 资源初始化和异步搜索事件处理；Avalonia 提供布局变换包装控件，Uno 原型则围绕渲染变换调整滚动范围。

Avalonia 还编写了原生 Reader／Workflow 菜单代码，Uno 目前只实现窗内导航。这是本次实现的差异，不证明 Uno 缺少原生菜单接入能力。编译错误、API 使用错误和下载失败不计作框架缺陷。

## 原生比较与剩余边界

随后已在 macOS 原生窗口中按相同逻辑尺寸操作两个渲染器。Uno 的 AppWindow 物理像素尺寸现通过 XamlRoot.RasterizationScale 换算，修正原型中的 Retina 尺寸差异。两边均实际完成筛选／搜索、已读状态、列表滚动、DLL 视图与计数导航保留、115% 缩放前后拖动节点、重置、内容主题切换，以及约 500 个逻辑单位宽时的工具栏换行。Avalonia 的原生 View 菜单也实际切换到了 Workflow。

两个新的 Uno Fluent 进程均复现了笔记中 Enter 不换行、Command+F 不聚焦搜索。Workflow → + 后，工具读取的可访问性树也均退化为 unknown 节点，但图像和指针操作仍可用；Avalonia 则保留了控件树。这些是当前原型、工具和运行时的观察，归因框架前仍需独立输入与可访问性验证，不代表 Uno 整体缺少多行控件、快捷键或无障碍能力。

Cupertino 版本现显式导出隐式样式；先前只加载资源，未实际应用。原生截图已确认基础控件和字体变化，列表仍混用另一套样式，不构成完整 macOS 桌面界面。截图边缘黑区及 Clip 点击后未观察到计数更新，仍属于待区分的布局、输入或截图现象，不能算完整原生交互通过。指南记录了具体范围和复现步骤。

两边中文显示和粘贴可用，但实际输入法组合输入与候选框位置、GPU 帧表现尚未确定。未运行 Android、Windows、旧 Windows 或 Linux。工具超时和早期坐标错误不计入框架判断；无窗口 Loaded 耗时、工作集快照和容器数仍只用于诊断，不代表原生首帧、峰值内存、帧率或安装体积。

## 产品桌面接入

首轮产品实现已将 Avalonia 12.1.6 纳入主解决方案。[桌面指南](../../docs/development/desktop-mvp.zh.md) 提供命令与范围。`mise run run:desktop` 在单进程内启动 .NET 通用宿主和 Avalonia 窗口，既有 CLI／Web 入口继续可用。共用装配提供相同的插件运行时、服务、SQLite 工作区、数据保护密钥和后台任务。桌面激活不映射 HTTP 端点，也不启动监听；服务工作移出 UI 线程，阅读器直接调用 IRssApi。

LazyTuiHub.Desktop 负责应用外壳、按需装配页面、主题切换和工作流／插件检查器。LazyTuiHub.Rss.Desktop 是独立动态加载的 `rss.desktop` 插件，声明对 `rss` 的必要依赖；它负责阅读控件与状态，引用共用 API 契约，不引用 RSS 实现类型。业务服务、工作流执行、DAO 访问和插件元数据继续独立于 Avalonia。宿主将 UI DLL 作为发行产物构建，不在运行时引用其实现程序集。

PluginRegistration 现接受普通 DeclaredPage 身份／标题记录。框架将页面及所属插件写入元数据，包括激活前的声明，使关闭、缺失和依赖阻塞的插件仍可诊断。AddDesktopPage 同时添加声明和带键的控件工厂；外壳只在 UI 线程调用已激活插件的工厂。视图首次导航时创建，在当前窗口内缓存。工厂创建新的控件实例，插件自己的模型在窗口替换时保留状态。共享程序集基线维持 Avalonia 与契约类型身份。这是暂定的页面扩展边界，尚未形成通用导航或插件依赖平台。

阅读器支持创建订阅、抓取所选订阅、通过 API 分页列出文章、按阅读状态筛选和标记已读。它使用真实 RSS 工作流与 SQLite，不将示例数据复制到产品。Load more 按下一页令牌推进，不限制界面的文章总数；订阅列表读取 API 的全部页。正文按可选择的文本展示，包含源标记；界面尚未实现 HTML 渲染、外部链接导航、全文搜索和改回未读。业务 API 行为保持不变。

Workflows 展示已保存的定义、当前执行计划和节点不可用的原因，不编辑图或提交任意执行。Plugins 分别展示已保存意图与当前激活状态，可以更改下次启动的启用设置。只关闭 UI 不影响 RSS 服务／任务；关闭或缺失 RSS 会阻塞依赖它的 UI。UI 代码缺失时保留页面声明和数据，框架诊断仍可使用。既有托管保留策略继续按自身约定执行。

同一阅读页面内串行执行操作，展示忙碌状态和错误。关闭窗口时先取消并等待受跟踪的界面操作，再停止后台任务，最后释放宿主。取消保留已经提交的写入；这实现协作退出，不代表持久化重放或无损暂停。选择和筛选条件仍只保留在当前进程，外观偏好保存在本机。

## 多设计系统

本轮接入 Avalonia.Themes.Fluent、Devolutions.AvaloniaTheme.MacOS 和 Material.Avalonia，具体包版本只在桌面项目中声明。标准 Avalonia 控件共用当前主题，不要求引入 FluentAvalonia 专有控件，也不为每个控件添加统一包装层。导航或对话框的交互确有差异时，再引入专门适配。业务插件不替换应用级主题。

设计系统（Automatic、Fluent、macOS、Material）与明暗模式（System、Light、Dark）分别设置。Automatic 在 macOS 上选择 macOS，在 Android 上选择 Material，其余平台选择 Fluent；该映射不代表已实现 Android 宿主。桌面允许显式选择设计系统。明暗模式使用 RequestedThemeVariant。桌面宿主管理可替换的主题位置，以及页面背景、次要文字、页面边距和标题大小等语义资源，插件局部样式单独保留。

有效设计系统改变时重建窗口，按需重新创建页面视图，保留窗口位置、大小和当前页面。ReaderModel 跨窗口保留订阅、已加载文章、选中订阅／文章、筛选、草稿输入、订阅面板状态、分页、错误及正在进行的操作。工作流／插件选择和工作流详情标签也会保留。视图重建不序列化滚动位置、文本选择和键盘焦点。只切换明暗时保留原控件。替换窗口不停止 DesktopSession、宿主或插件任务，正常退出仍沿用既有停止流程。任务登记、完成移除和退出快照使用同一同步边界，避免取消时集合变化与复制并发。

偏好属于本机 UI 配置，保存在本地应用数据目录的 `LazyTuiHub/desktop.json`，独立于工作区数据库。独立运行可通过 `--desktop-settings` 指定其他文件。写入通过相邻临时文件和重命名完成。读取失败时使用默认外观并显示错误，不覆盖失败文件；保存或应用失败也在界面中报告。探索性 MVP 不引入偏好格式版本或历史迁移。

适配层使用已发布的 Avalonia 12 包。Devolutions 的 macOS 主题仍处于开发中，未覆盖控件使用内置 Fluent 样式补齐；Material.Avalonia 不代表完整遵循 Material 3。移除 Material 主题时释放它；其适配层先初始化具体明暗变体，再启用继承，避免挂接前的资源查询遇到未初始化的 Inherit 值。macOS 适配层调用库提供的清理接口，移除应用级 Liquid Glass 色调资源。参见 [Devolutions 实现](https://github.com/Devolutions/avalonia-extensions/blob/7ef3ab09b1696938009c7923efda44490a848445/src/Devolutions.AvaloniaTheme.MacOS/DevolutionsMacOsTheme.cs)和 [Material 实现](https://github.com/AvaloniaCommunity/Material.Avalonia/blob/v3.21.0/Material.Styles/Themes/MaterialTheme.cs)。

无窗口集成检查覆盖反复切换设计系统、独立明暗模式、草稿／筛选／选择保留、进行中的操作、检查器导航、偏好恢复和配置错误展示。macOS 原生检查另确认了不同控件绘制、选择器、中文草稿保留、当前导航保留、浅色模式及重新打开后的偏好恢复。其他操作系统和完整辅助技术行为尚未验证。

## 响应式布局与 UI 自动化

本轮参考 waiter 对应用导航、页面内容和次要表单的分离。依据是它的[应用外壳与模块布局](https://github.com/tuihub/waiter/blob/ae1ae6f349eef99a8a300ed9276c319683ef5797/lib/view/pages/frame_page.dart)、[断点](https://github.com/tuihub/waiter/blob/ae1ae6f349eef99a8a300ed9276c319683ef5797/lib/view/layout/bootstrap_breakpoints.dart)、[最近文章页](https://github.com/tuihub/waiter/blob/ae1ae6f349eef99a8a300ed9276c319683ef5797/lib/view/pages/yesod/yesod_recent_page.dart)和[文章预览](https://github.com/tuihub/waiter/blob/ae1ae6f349eef99a8a300ed9276c319683ef5797/lib/view/pages/yesod/yesod_preview_card.dart)。借鉴范围是信息层次和窄屏导航，不引入 Flutter，也不复制完整控件包装层。

应用导航在宽度超过 992 个逻辑单位时常驻，较窄时通过 Menu 打开。共用的 AdaptivePane 根据页面实际分配宽度判断：超过 768 时并排显示 360 宽的列表和详情，否则一次只显示一栏。鼠标选中条目或按 Enter 进入详情，Back 在布局完成后将键盘焦点还给选中的条目。Reader、Workflows 和 Plugins 共用这个行为。这些数字决定呈现方式，不构成最小窗口尺寸或平台支持承诺。缩放复用控件和模型；设计系统切换通过已有页面状态保留当前列表／详情位置。

阅读列表以 Library 标题和数量为起点，放置订阅／阅读状态筛选、随上下文变化的刷新操作，订阅表单独立打开。刷新抓取所选订阅，或在 All feeds 下读取已存文章；右键菜单另提供明确的本地重载。卡片区分来源、标题、摘要、未读状态和日期，预览的行数限制不截断存储内容或完整文章。正文限制行宽，来源网址移到文末；普通操作数量不再占据常驻状态栏，错误继续显示。外观设置保留在导航底部的弹层中。导航、文章卡片和低强调的工具栏按钮使用少量应用专属模板，普通输入控件继续遵循所选设计系统。

程序化 UI 控制成为持续维护的开发入口。`mise run test:desktop` 运行桌面测试，将审阅 PNG 和控件状态 JSON 写入已忽略的 `artifacts/desktop/`。DesktopUiDriver 使用 Avalonia.Headless 官方鼠标／键盘事件、真实控件坐标、窗口尺寸和 Skia 截图。场景通过产品界面访问真实动态装配的 RSS 服务、临时 SQLite 和本地 RSS 样本，不给产品添加自动化端点或测试开关。常规测试门禁执行同一场景但不输出截图。图像用于审阅，不增加依赖平台的逐像素比较门禁。

场景覆盖订阅输入、抓取、阅读、已读状态、窄屏返回／焦点、检查器导航、三种设计的宽／中／窄尺寸以及深色模式。弹层通过打开状态和自己的视觉根节点观察，再通过键盘操作、单独截图。computer use 捕获遗漏属于工具观察，不能单凭它判定界面失败或替换交互。无窗口图像不替代对原生字体、窗口外观、无障碍、弹层位置和图形性能的验证。Android 和旧 Windows 不在本轮范围内。

## 后续视觉校准

用户指出当前三套适配层均存在视觉问题。本轮交付响应式内容组织和程序化 UI 检查，交互／布局测试通过不代表视觉准确。官方设计文档、组件参考的对比及对应的 Avalonia 修正留到下一轮，样板准备与本轮产品变更分开。Fluent Web 不能等同于 WinUI；Material 3 参考不能证明 Material.Avalonia 已符合该规范；macOS 原生样板也需要保留操作系统范围。

## 产品验证与剩余工作

产品检查和 CI 现包含桌面项目与连接真实服务的无窗口测试。测试覆盖多页订阅、已读状态持久化、输入错误、导航、UI 代码关闭／缺失／恢复、RSS 依赖不可用、桌面不启动 HTTP 服务、后台任务退出及正在进行的界面操作取消。2026-11-18 的 macOS ARM64 原生检查还实际操作了订阅／抓取／阅读／筛选、计划查看、重启后的插件关闭／恢复、数据保留、主题切换和应用退出。原生检查促使文章和插件的辅助功能名称改为可读名称，不再暴露对象内容。

UI 项目保留产品分析器规则，仅针对通过构造函数注入依赖并使用编译式 XAML 初始化的视图豁免 AVLN3001：未使用的资源 URI 构造路径无法调用这些构造函数。[官方编译器实现](https://github.com/AvaloniaUI/Avalonia/blob/12.1.6/src/Avalonia.Build.Tasks/XamlCompilerTaskExecutor.cs) 说明了该诊断；不为通过检查添加占位服务或无参构造函数。无窗口测试使用官方会话的异步释放方式。

三套设计体系已接入应用适配层，控件覆盖不代表完整的原生平台交互。远程服务端选择、可视化工作流编辑器、HTML 渲染、安装包以及 Android／Windows 7／XP 支持仍待推进。框架选定不代表已经验证平台支持范围；输入法组合输入、完整辅助技术表现和 GPU 性能仍需验证。Uno 继续保留在可选比较产物中，不引入第二套产品渲染器或跨渲染器抽象。
