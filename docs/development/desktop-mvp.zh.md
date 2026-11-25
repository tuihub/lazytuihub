# Avalonia 桌面 MVP

[English](desktop-mvp.en.md) | 中文

## 运行与使用

在仓库根目录运行产品桌面界面：

```sh
mise run run:desktop
mise run run:desktop -- --database /path/to/workspace.db
mise run run:desktop -- --endpoint https://SERVER
```

CLI、服务端和本地桌面共用 .NET 本地应用数据目录下的 `LazyTuiHub/workspace.db`。单独指定数据库路径即可隔离实验。相邻 `keys/` 目录保存用于保护分页令牌和游戏数据提供方凭据的密钥。宿主从发行目录读取 `plugins/plugins.json`，可用 `--plugin-directory` 指定其他插件部署目录。

在 Reader 中点击 Library 旁的 +，输入 HTTP／HTTPS 订阅地址和可选名称，点击 Subscribe。选择订阅后使用刷新按钮，通过 RSS 管线抓取文章。选择文章可阅读已存储的正文，点击 Mark read 标记已读，也可按已读／未读筛选。Load more 请求下一页，界面不限制文章总数。选择 All sources 时，刷新按钮重新读取已保存的文章。刷新按钮的右键菜单也提供 Reload saved articles，用来读取包括后台刷新结果在内的工作区内容。正文以可选择的文本显示，源内容中的标记也会显示；尚未实现 HTML 渲染和打开外部链接。

点击外观设置旁的 **Find an operation**，也可以在 macOS 按 Command+K，其他系统按 Ctrl+K。输入操作名称或说明中的关键词，再填写参数。例如，**games · log-session** 可记录游玩时长，**workflow · run** 可运行工作流。CLI 通过 `mise run run:cli -- games --help` 展示同一工作区的游戏能力。运行 `mise run run:desktop -- --endpoint https://SERVER` 可使用远程阅读器及会话、订阅和文章操作。

Workflows 展示已保存的定义、当前执行计划和不可执行原因。在操作面板中可以保存定义、单次运行工作流，或持续运行并查看输出，直到取消。

Plugins 分别展示已保存的启用设置、本次启动的激活状态和文件可用性。启用／关闭在重启后生效，必要插件不能关闭。桌面阅读器依赖文章库插件。关闭 RSS 后，抓取停止，订阅控件隐藏，已保存的文章仍可阅读。插件缺失或关闭时保留页面声明和既有数据；常规保留策略继续遵循存储约定。切换页面会保留页面实例及内存中的选择，重启不会保留选择和筛选条件，外观偏好则单独保存在本机。

## 游戏库

打开 **Games** 浏览封面墙。可以切换到 **List view**，按标题或别名搜索，或选择 **Installed**、**Online**、**Needs match**。点击 **More filters**，在书架上方的浮层中筛选分类和资料来源。单击游戏或用方向键聚焦后按 Enter 打开详情。窄屏使用返回按钮或 Esc 回到原来的浏览位置。在 **Appearance → Reduce motion** 中勾选减少动态效果，面板会立即显示。

打开 **Sources**，选择平台并连接账户，随后点击 **Sync** 导入收藏。本地安装可在 **Local folders** 中选择扫描器和目录。打开游戏详情后，**Auto-fill** 根据来源 ID 补全资料，**Identify** 可手动搜索和确认匹配，**Edit info** 可修改资料或恢复来源值。安装或收藏记录归错游戏时，在该记录旁点击 **Reassign**。[游戏库 Note](../../.agents/notes/2026-11-23-mvp-games.zh.md)

## 外观

打开导航底部的 Appearance，在 Design system 中选择 Automatic、Fluent、macOS 或 Material。Automatic 按平台选择：macOS 使用 macOS 主题，Windows／Linux 使用 Fluent，未来 Android 的映射为 Material。Color mode 单独选择 System、Light 或 Dark。设置保存在本地应用数据目录下的 `LazyTuiHub/desktop.json`，切换工作区或重启后仍然保留。独立运行时，除了指定数据库路径，还可通过 `--desktop-settings /path/to/desktop.json` 指定独立的外观配置。

切换设计系统会重建窗口，保留当前页面、阅读选择／筛选、已加载文章、未提交的订阅输入和正在进行的操作。只切换明暗则直接更新原控件。重建时滚动位置、焦点和文本选择可能重置。后台任务继续运行，插件开关仍需冷启动才能生效。

插件页面使用标准 Avalonia 控件。主操作用 `accent`，次要操作用 `secondary`，文本按钮用 `quiet`，图标按钮用 `icon` 并设置无障碍名称。在 ListBox 上使用 `navigation` 或 `collection` 即可获得对应的选中与键盘焦点反馈。文字角色包括 `title`、`headline`、`section`、`item-title`、`reading`、`caption` 和 `metadata`。自定义布局通过动态资源获取 `SurfaceBrush`、`DividerBrush` 或 `SecondaryText`。其他标准控件继承当前基础主题。`AddDesktopPage` 注册瞬态视图工厂；需要跨设计切换保留的状态放入插件自己的服务／模型。数据按模型初始化一次，避免每次加载视图都重新读取。外壳在当前窗口内缓存视图，切换设计不重新加载插件程序集。

在图标按钮中使用 `<PathIcon Classes="action" Data="{DynamicResource ActionIconRefresh}" />`。和文字并排时把类设为 `action inline`。图标随当前设计和前景色变化。工具提示和无障碍名称设置在按钮上。新增操作时，在三套主题的[图标字典](../../src/LazyTuiHub.Desktop/Appearance/Icons/)中定义同一个资源键。

Fluent 使用 Avalonia 官方主题；macOS 使用 Devolutions 主题，尚未覆盖的控件回退到其内置 Fluent 样式；Material 使用 CCSWE Material，并接入项目维护的 M3 色板、Roboto 字体和按钮模板。原生标题栏继续遵循操作系统。主题选择不代表已完成全部原生交互、完整 Material 3 规范或 Android 支持。

## 响应式内容组织

Fluent 在宽窗口显示带标签的侧栏，宽度为 641 至 1007 个逻辑单位时收成图标栏，640 及以下通过菜单导航。点击图标可直接进入页面，点击菜单按钮可在正文上方展开标签。Material 在左侧导航轨显示当前四个入口。窗口宽度小于 600 或高度小于 480 时，这些入口移到底部，外观设置移到页头。已注册入口的数量超出相应样式的适用范围时，侧栏或抽屉会列出全部入口。Mac 外观沿用侧栏，在 992 及以下切换为覆盖式菜单。

每个 `AdaptivePane` 独立按页面可用宽度判断：不超过 768 个逻辑单位时，列表和详情改为单栏切换。点击列表项或按 Enter 进入详情；Back 返回列表并恢复键盘焦点。Workflows 和 Plugins 共用这个布局。

调整尺寸保留原有视图、选择、草稿和已加载数据，不重新加载服务或切换主题。当前处于列表还是详情也会跨设计切换保留。文章卡片依次呈现来源、标题、摘要、未读状态和发布日期。预览限制显示行数，已存储的标题／正文及完整阅读内容不被截断。正文限制行宽，来源网址放到文末。订阅输入放在独立面板；普通加载数量放在 Library 旁，失败信息继续显示。源内容仍按文本展示，不渲染 HTML。

## 装配与生命周期

| 部分 | 职责 |
| - | - |
| [应用宿主](../../src/LazyTuiHub/) | 共用服务／插件装配；桌面模式使用通用宿主，HTTP／CLI 模式使用 Web 宿主。 |
| [LazyTuiHub.Sdk.Avalonia](../../src/LazyTuiHub.Sdk.Avalonia/) | 注册页面、刷新工作区、使用自适应面板与动效，并将界面操作交给宿主跟踪。 |
| [桌面层](../../src/LazyTuiHub.Desktop/) | Avalonia 应用／窗口、页面装配、工作流／插件检查器、界面操作跟踪及退出。 |
| [文章阅读插件](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/Gui/) | 维护阅读器及其状态，通过类型化操作读取文章。RSS 提供可选的订阅源适配器。 |
| [插件框架](../../src/LazyTuiHub.Plugins/) | 普通页面声明、所属插件、元数据保留、依赖激活和冷启动开关。 |

单账户模式下，本地桌面启动已启用的插件后台任务。本地和远程桌面模式都无需启动 HTTP 监听。业务和 DAO 代码不引用 Avalonia。宿主从业务插件 DLL 加载 ClientGui 入口，页面实现在插件内部维护。`AddDesktopPage` 同时提交普通页面声明和带键的 Avalonia 控件工厂；导航只接入已激活插件的页面，首次打开页面时才在 UI 线程创建控件。默认共享程序集上下文保持契约和 Avalonia 类型身份一致。

阅读操作将服务工作移出 UI 线程，同一页面内串行执行。错误保留在界面上，不转换为空的成功结果。关闭主窗口时取消并等待受跟踪的界面操作，再停止后台任务，最后释放服务。取消不回滚已经提交的数据库变更，也不提供工作流的持久化暂停。

桌面端使用编译式 XAML 绑定、自适应分栏和列表虚拟化。HTTPS 客户端支持会话、订阅和文章，工作区管理及游戏库在本地运行。HTML 渲染、应用内服务端选择器及 Android 或旧版 Windows 图形界面仍待实现或验证。[渲染框架选型](../../.agents/notes/2026-11-17-mvp-ui-comparison.zh.md) · [比较原型](ui-comparison.zh.md)

## 验证

无需操作电脑即可运行桌面交互场景并生成审阅截图：

```sh
mise -E development run test:desktop
```

[DesktopUiDriver](../../tests/LazyTuiHub.Tests/Desktop/DesktopUiDriver.cs) 向真实控件发送 Avalonia 无窗口鼠标和键盘输入，调整窗口尺寸，并捕获 Skia 绘制的画面。[布局场景](../../tests/LazyTuiHub.Tests/Desktop/DesktopTests.Layout.cs) 创建临时 SQLite 工作区和本地 RSS 服务，通过表单输入订阅、抓取文章、进入详情、标记已读、返回并恢复焦点，再切换检查器页面。场景检查三套设计在 1280、820、390 个逻辑单位下的布局，以及外观弹层和深色模式。任务将 PNG 及同名 JSON 控件快照（边界、字体、前景色、启用／焦点状态）写入已忽略的 `.local/desktop/`；直接调用测试运行器时，可通过 `LAZYTUIHUB_UI_ARTIFACTS` 指定截图目录。常规产品测试也执行这些场景，但不写截图。

自动检查桌面交互时，优先运行无窗口的 `mise -E development run test:desktop`。在 macOS 上验证打包产物时，可用 `open -g -j /path/to/LazyTuiHub.app` 在后台隐藏启动，避免打断正在使用的应用。

[游戏库交互场景](../../tests/LazyTuiHub.Tests/Desktop/DesktopTests.Games.cs) 检查封面比例、标题颜色、筛选项宽度和工具栏位置，并通过实际控件验证别名搜索、窄屏返回焦点、资料编辑、字段恢复、归属纠错与账户表单。

新增页面或修改共享控件时，按[桌面 UI 自查标准](desktop-ui-review.zh.md)登记场景。运行 `mise -E development run review:desktop` 生成阅读器工具栏、游戏书架等页面的统一报告。

外观弹层通过打开状态、独立视觉根节点和颜色选择器的键盘交互验证；截图使用弹层实际所在的视觉根节点，既支持覆盖式弹层，也支持独立窗口。computer use 截图未包含弹层，本身不能证明应用失败。生成图像用于人工审阅，不引入逐像素基准图门禁。[设计系统审查](../../.agents/notes/2026-11-18-mvp-design-systems.zh.md) 记录组件库对照、产品组件的维护归属和平台边界。首次执行 `mise -E development run setup:design-baselines`，用 `mise -E development run render:design-baselines:avalonia` 生成组件库样板，再用 `mise -E development run run:design-baselines` 对照官方交互参考。无窗口渲染不代表原生弹层位置、系统字体一致性、无障碍或 GPU 性能已经验证。


`mise run test` 验证分页、已读状态、输入错误、插件移除与恢复、本地桌面无 HTTP 监听和协作关闭。`mise run check` 包含这些测试。桌面、文章和游戏项目针对构造函数注入的编译式 XAML 视图抑制 AVLN3001。

2026-11-18 的 macOS ARM64 原生检查还实际操作了添加订阅、通过 HTTP 抓取订阅、中文显示、标记已读／未读筛选、工作流查看、重启后的插件关闭／恢复、已读状态保留、明暗切换和窗口退出。辅助功能名称使用文章标题和插件名称，不显示序列化对象。这些观察不代表完整辅助技术覆盖、输入法组合输入、GPU 性能或其他平台的原生表现已经验证。
