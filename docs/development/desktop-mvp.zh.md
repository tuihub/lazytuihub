# Avalonia 桌面 MVP

[English](desktop-mvp.en.md) | 中文

## 运行与使用

在仓库根目录运行产品桌面界面：

```sh
mise run run:desktop
mise run run:desktop -- --database /path/to/workspace.db
```

默认工作区位于 .NET 本地应用数据目录下的 `LazyTuiHub/workspace.db`，与 CLI／服务端模式共用。独立实验请显式指定数据库路径。相邻的 `keys/` 目录保存既有 API 分页密钥。应用加载发行目录中的 `plugins/plugins.json`；`--plugin-directory` 可指定另一套插件部署目录。

在 Reader 中展开 Add subscription，输入 HTTP／HTTPS 订阅地址和可选名称，点击 Subscribe。选择订阅后点击 Fetch，通过 RSS 管线抓取文章。选择文章可阅读已存储的正文，点击 Mark read 标记已读，也可按已读／未读筛选。Load more 请求下一页，界面不限制文章总数。Reload 重新读取当前工作区，包括后台刷新写入的内容。正文以可选择的文本显示，源内容中的标记也会显示；尚未实现 HTML 渲染和打开外部链接。

Workflows 展示已保存的定义、当前本地执行计划和不可执行原因。目前是只读检查器。保存和运行工作流定义使用既有 JSON／CLI 工具；该页面不提交执行或收集所需输入。

Plugins 分别展示已保存的启用设置、本次启动的激活状态和文件可用性。启用／关闭在重启后生效，必要插件不能关闭。RSS Reader UI 插件（`rss.desktop`）可独立于 RSS 业务服务（`rss`）关闭；关闭 RSS 后，依赖它的界面也不可用。插件缺失或关闭时保留页面声明和既有数据；常规保留策略继续遵循存储约定。切换标签页会保留页面实例及内存中的选择，重启不会保留选择和筛选条件，外观偏好则单独保存在本机。

## 外观

在顶部 Design 中选择 Automatic、Fluent、macOS 或 Material。Automatic 按平台选择：macOS 使用 macOS 主题，Windows／Linux 使用 Fluent，未来 Android 的映射为 Material。Appearance 单独选择 System、Light 或 Dark。设置保存在本地应用数据目录下的 `LazyTuiHub/desktop.json`，切换工作区或重启后仍然保留。独立运行时，除了指定数据库路径，还可通过 `--desktop-settings /path/to/desktop.json` 指定独立的外观配置。

切换设计系统会重建窗口，保留当前页面、阅读选择／筛选、已加载文章、未提交的订阅输入和正在进行的操作。只切换明暗则直接更新原控件。重建时滚动位置、焦点和文本选择可能重置。后台任务继续运行，插件开关仍需冷启动才能生效。

宿主同时只加载一套基础主题，并提供 `PageBackground`、`SecondaryText`、`PagePadding` 和 `PageTitleSize` 资源。共用样式类包括 `title`、`caption`、`status` 和 `primary`。插件页面使用标准 Avalonia 控件，通过动态资源引用外观相关值。`AddDesktopPage` 注册瞬态视图工厂；需要跨设计切换保留的状态放入插件自己的服务／模型。数据按模型初始化一次，避免每次加载视图都重新读取。外壳在当前窗口内缓存视图，切换设计不重新加载插件程序集。

Fluent 使用 Avalonia 官方主题；macOS 使用 Devolutions 主题，尚未覆盖的控件回退到其内置 Fluent 样式；Material 使用 Material.Avalonia。原生标题栏继续遵循操作系统。主题选择不代表已完成全部原生交互、完整 Material 3 规范或 Android 支持。

## 装配与生命周期

| 部分 | 职责 |
| - | - |
| [应用宿主](../../src/LazyTuiHub/) | 共用服务／插件装配；桌面模式使用通用宿主，HTTP／CLI 模式使用 Web 宿主。 |
| [桌面层](../../src/LazyTuiHub.Desktop/) | Avalonia 应用／窗口、页面装配、工作流／插件检查器、界面操作跟踪及退出。 |
| [RSS 桌面插件](../../src/LazyTuiHub.Rss.Desktop/) | 阅读控件与状态，直接调用共用的 IRssApi 契约。 |
| [插件框架](../../src/LazyTuiHub.Plugins/) | 普通页面声明、所属插件、元数据保留、依赖激活和冷启动开关。 |

桌面宿主启动相同的可用后台任务，不启动 HTTP 监听。业务和 DAO 代码不引用 Avalonia。桌面插件作为发行产物构建并动态加载，宿主不在运行时引用它的实现程序集。`AddDesktopPage` 同时提交普通页面声明和带键的 Avalonia 控件工厂；导航只接入已激活插件的页面，首次打开页面时才在 UI 线程创建控件。默认共享程序集上下文保持契约和 Avalonia 类型身份一致。

阅读操作将服务工作移出 UI 线程，同一页面内串行执行。错误保留在界面上，不转换为空的成功结果。关闭主窗口时取消并等待受跟踪的界面操作，再停止后台任务，最后释放服务。取消不回滚已经提交的数据库变更，也不提供工作流的持久化暂停。

产品使用 Avalonia 12.1.6、三套设计系统、编译式 XAML 绑定、可换行操作栏和列表虚拟化。更完善的响应式布局、远程服务端选择、可视化工作流编辑器、HTML 渲染以及 Android／旧 Windows 支持仍待后续推进。选型记录在 proposed 状态的 [UI Note](../../.agents/notes/2026-11-17-mvp-ui-comparison.zh.md) 中；[比较原型](ui-comparison.zh.md) 继续作为独立开发产物保留。

## 验证

`mise run check` 包含产品构建／分析器、格式、API 一致性、文档／Note 规则和产品测试。`mise run test` 通过 Avalonia 官方无窗口主机，连接实际动态装配的 RSS 服务和独立 SQLite 工作区，覆盖分页、已读状态持久化、输入错误、导航、界面插件关闭／缺失／恢复、业务依赖不可用、桌面不启动 HTTP 服务以及协作退出。视图通过构造函数注入依赖并使用编译式初始化；两个 UI 项目仅豁免 AVLN3001，它提示缺少无参构造函数导致未使用的 URI 构造方式不可用。

2026-11-18 的 macOS ARM64 原生检查还实际操作了添加订阅、通过 HTTP 抓取订阅、中文显示、标记已读／未读筛选、工作流查看、重启后的插件关闭／恢复、已读状态保留、明暗切换和窗口退出。辅助功能名称使用文章标题和插件名称，不显示序列化对象。这些观察不代表完整辅助技术覆盖、输入法组合输入、GPU 性能或其他平台的原生表现已经验证。
