# 流式管线与 RSS 示例

[English](streaming-mvp.en.md) | 中文

## 目标与边界

MVP 用 RSS 阅读器和 SQLite 探索跨业务复用节点。首轮本地运行器支持具名流式／同步端口和可执行的本地计划。[框架 Note](../../.agents/notes/2026-11-17-mvp-framework.zh.md) 与[节点执行 Note](../../.agents/notes/2026-11-17-mvp-node-execution.zh.md) 区分当前选择和后续设计。MVP 合入主分支前，Note 保持可自由编辑的提案状态；每轮将代码和对应 Note 一起提交。

当前通过 Avalonia 桌面阅读器、CLI、进程内 API 和生成的 HTTP 客户端交互，没有可视化图编辑器或分布式执行。图定义和数据库 Schema 没有原型兼容承诺；不兼容的变更使用新的工作区。本轮文章表和默认抓取图均已变更，请使用新工作区。

[桌面 MVP 指南](desktop-mvp.zh.md) 介绍可运行的 Avalonia 界面、真实 RSS／SQLite 操作、工作流查看和插件可用性。[UI 比较指南](ui-comparison.zh.md) 保留此前的独立实验。

## 框架与业务

| 部分 | 责任 |
| - | - |
| [Framework](../../src/LazyTuiHub.Framework/) | 通用数据关联、节点／端口注册、计划、异步交付、同步输入对齐、扇出、反压与取消。 |
| [节点库](../../src/LazyTuiHub.Nodes/) | 定时器、HTTP fetch、有类型的输出消费者和常见源／转换／消费辅助，通过普通核心注册接入。 |
| [插件装配](../../src/LazyTuiHub.Plugins/) | 可信 DLL 装配、声明及意图保留、工作流管理、任务与 hook 调用。 |
| [DAO 工具包](../../src/LazyTuiHub.Dao/) | 不依赖数据库驱动的契约、访问需求／能力及命名空间表目标。 |
| [SQLite 提供者](../../src/LazyTuiHub.Storage.Sqlite/) | 必要存储插件，提供通用 JSON 记录／TTL、SQLite FTS5、原生连接及委托清理。 |
| [核心插件](../../src/LazyTuiHub.CorePlugin/) | 发行清单要求的问候功能及内置节点注册。 |
| [RSS 插件](../../src/LazyTuiHub.Rss/) | 订阅、定时抓取、抓取明细和 RSS／Atom 解析。 |
| [文章插件](../../src/LazyTuiHub.Articles/) | 各类来源的文章入库、全文索引、已读状态和阅读接口。 |
| [Host](../../src/LazyTuiHub/) | 启动、插件／工作流／DAO CLI、桌面／Web 装配、共享 DI 基础设施及生命周期。 |
| [桌面层](../../src/LazyTuiHub.Desktop/)／[文章阅读界面](../../src/LazyTuiHub.Articles.Desktop/) | Avalonia 外壳与诊断／文章阅读器及可选的 RSS 订阅操作。 |

PipelineDefinition 表示处理关系；DataReference 与 DataRelation 表示持久数据身份及关联。各业务插件通过 BusinessStorage 写自己的表，返回数据身份和关联意图；框架登记元数据后，在同一工作区事务提交业务行、FTS 更新及元数据。BusinessStorage 要求显式传入工作区连接源，其他存储不能隐式加入这个事务。插件禁止修改框架表。这是完全可信进程内的所有权契约，没有数据库权限隔离。这些身份不同于同步输入 key，也不构成分布式对象存储。

rss.ingest 连接 builtin.timer、rss.feed-requests、builtin.http-fetch、rss.parse、articles.store 和 articles.output。其他模块可以在解析后分支，不必修改运行器。阅读及其他直接 RSS 操作目前也经过工作流，让工作流成为可选能力仍待后续实现。本地和 HTTP 调用共享 IRssApi 订阅契约和 IArticleApi 文章契约；字段、路由、分页、去重及错误见[生成的 API 参考](../api/index.md)。

接入新的文章来源时，让节点输出 [ArticleDraft](../../src/LazyTuiHub.Api/Articles/ArticleDraft.cs)，并连接 articles.store。来源资源名与来源内的条目 ID 共同标识文章。例如 notebooks/a 和 notebooks/b 可以各自使用条目 ID 1。同一来源再次提交该条目时会更新内容、保留文章名称和已读状态。若数据包附有已登记的来源引用，入库会同时登记来源与文章的关联。

## 端口与执行计划

NodeRegistry.Register 接收具名、有类型的输入／输出声明和一个异步执行函数，每类端口可以为任意数量，包括零。连线记录 source、sourcePort、target、targetPort，数据类型与交付方式必须严格一致，object 不作为接收任意类型的端口。输出可以未连接，每个已声明输入需要生产者。

PipelinePlanner 生成 PipelinePlan，绑定节点实现、端口连接、本地放置和缓冲容量。当前后端要求 DAG，所有节点放在 local。保存时返回可执行性诊断，同时保留不可用节点及原始参数／连线；执行要求有效计划，运行定义时生成新计划，也可直接执行已有计划。资源清单、远程放置及远程传输尚未实现。

## 流式与同步交付

StreamInput 包含端口和 DataPacket。流数据在反压条件下尽快可读，框架不承诺消息或不同流之间的业务处理顺序。本地扇出将同一只读载荷引用分别交给各分支，转换时产生新结果；GC 回收已释放对象。这不构成跨进程共享内存。

SynchronizedInput 包含 messageKey 和完整同步输入组的只读字典。节点全部同步输入组成一组，发送方显式给出 key；核心按相同 key 对齐，节点不能看到半组输入。每个 key 为每个端口维护接收 FIFO，每组各取一个值；重复 key 形成多组，不自动去重。不同 key 按齐备情况推进，不推断跨生产者的业务轮次关联。

同步保证成组可见与声明依赖，不保证物理同时收包或相对于流数据的时序。节点负责配置生效、状态和副作用。异步事件循环可以在读到完整配置组后替换局部配置，为随后处理的流数据取得快照，参见[混合输入示例](../../src/LazyTuiHub/Examples/WorkflowDemo.cs)。

每个节点有界入口的 bufferCapacity 默认为 16，满时等待；顺序扇出让慢分支反压上游。未齐同步值移到独立内存表，目前没有暂存数量／字节预算或落盘，因此有界入口不代表整体内存有界。HTTP fetch 缓存完整响应，XML 解析和 SQLite 也有各自成本。

每个端口在最后一个生产者结束后关闭。如果待齐组缺少已经关闭的端口，立即以节点、key 和缺失端口解释失败，即使另一条流仍开放。取消中断协作等待；节点函数返回后停止接收，后续写入失败而不挂起。节点失败取消其他节点并报告身份及原因；已提交数据保留，不回滚全图，也不自动重试节点。

## 运行示例

```sh
mise run demo:workflow
mise run demo:rss
mise run test
```

demo:workflow 不使用 HTTP 或 SQLite。节点处理流数据，同时接收两个同步配置输入；示例通过显式确认协调输入，验证配置未齐时流的结果仍为 2，完整配置更新后分别得到 7、13，最后取消全部任务。这些确认不为框架增加跨类别的时序保证。

demo:rss 自建本地 RSS fixture、临时 SQLite 和 HTTP 服务，验证本地／HTTP 一致、重复刷新、已读状态及跨业务分支，结束后移除临时数据。产品测试另行覆盖同步对齐、未齐组、严格端口匹配、反压、共享引用、合流结束、失败取消、XML、分页及重启。

## 使用 RSS 工作区

```sh
mise run run -- --command rss --action help
mise run run -- --database ./artifacts/rss/workspace.db --command rss --action add --feed-url https://example.com/feed.xml --title Example
mise run run -- --database ./artifacts/rss/workspace.db --command rss --action feeds
mise run run -- --database ./artifacts/rss/workspace.db --command rss --action refresh --name feeds/FEED_ID
mise run run -- --database ./artifacts/rss/workspace.db --command articles --action list --unread true
mise run run -- --database ./artifacts/rss/workspace.db --command articles --action read --name articles/ARTICLE_ID
```

将 feed URI 替换成实际地址。add 返回订阅名称，articles list 返回文章名称和后续分页 token，通过 --page-token 继续查询。read 标记已读并输出 Proto JSON，不执行 HTML。

不传 --database 时，工作区位于系统 LocalApplicationData 下的 LazyTuiHub/workspace.db。启动 HTTP 服务同时启动可用且所需工作流能生成计划的插件任务，首次创建图时默认周期为 15 分钟，可用 --refresh-interval 00:05:00 修改；已有图定义保留。定时器积压采用 PeriodicTimer 合并，不重放每个错过的时刻。刷新失败记录日志，在下一周期重试。

```sh
mise run run -- --database ./artifacts/rss/workspace.db --urls http://127.0.0.1:5080
```

## 冷启动插件

宿主从应用输出目录的 plugins/plugins.json 加载显式清单，--plugin-directory DIR 可选其他目录。构建和发布将 core、rss、articles、articles.desktop、storage.sqlite 入口 DLL 及依赖描述放入对应目录。插件共享宿主依赖基线和默认程序集上下文；附加依赖需随插件部署，原型没有私有依赖版本、原生依赖部署系统、热更新或沙箱。[插件 Note](../../.agents/notes/2026-11-17-mvp-plugins.zh.md) 记录边界与调研。

```sh
mise run run -- --database ./artifacts/rss/workspace.db --command plugins --action list
mise run run -- --database ./artifacts/rss/workspace.db --command plugins --action inspect --plugin rss
mise run run -- --database ./artifacts/rss/workspace.db --command plugins --action disable --plugin rss
mise run run -- --database ./artifacts/rss/workspace.db --command plugins --action enable --plugin rss
mise run run -- --database ./artifacts/rss/workspace.db --command plugins --action configure --plugin rss --file ./rss-settings.json
```

修改在重启后生效。list 分别展示保存的启用意图、文件可用性、激活结果和任务资格；inspect 包含保留的节点／端口／数据类型及 DAO 声明和不透明配置。RSS 配置可写为 {"refreshInterval":"00:05:00","detailRetention":"7.00:00:00"}，未知字段保留。detailRetention 可省略，默认 null（长期保留）；正时长为新抓取观察记录设置到期时间，修改配置不改写已有记录。周期用于初始化新默认图及控制失败重试等待；已有定时器周期需修改图内节点参数，存储的图优先于新默认值。

插件禁用或缺失时不提供业务命令／路由／节点，也不启动任务。图查看与编辑、关联、配置及插件诊断仍然可用，包括 core 或 storage.sqlite 缺失的情况。这两个必要插件不能通过正常配置关闭。DAO 提供者缺失会阻止 RSS 激活，诊断指出不可用的契约、实例、档位或底层，并保留需求和数据供恢复。移走和放回 DLL 不改启用意图，不删除数据、已读状态、图、关联或任务声明。工作流缺少实现时停止执行并说明原因。--command workflow --action list 展示全部存储图及可执行性。归属来自保留声明或图中显式 pluginId，没有这些信息时保持未知。

--command plugins --action cleanup 或 uninstall --plugin ID 显式调用可选 hook。禁用插件可在冷启动作为维护目标提供 hook，不激活业务能力。代码或 hook 缺失时报告错误；RSS 目前均未提供。框架接受 hook 完成，不证明业务清理完整。uninstall 不删除 DLL 或目录记录，禁用及缺失不自动调用任何清理 hook。

插件实现 IApplicationPlugin.Configure(PluginRegistration)，贡献服务／节点／数据类型／默认工作流／命令／路由／任务，以及所需的 DAO 需求／提供声明或保留委托，并在发行清单登记身份及入口 DLL／类型。requires 只列必要服务依赖；DAO 需求在使用者激活前匹配活跃提供者能力，不依赖清单顺序。跨业务工作流连线保持独立。框架服务管理元数据，业务服务获得 IWorkflowExecutor。默认图仅在不存在时插入；任务声明所需工作流，自行决定重试并遵守宿主取消。实现缺失时仍保留声明，但活动队列、未齐同步输入及节点状态仍只在内存中。

## DAO 工具与保留

插件通过 registration.Daos.Requirements 声明所需档位、契约、访问实例及可选底层。当前实例为 workspace。提供者支持三档：通用 records.ttl 契约的 IRecordDao、sqlite.fts5 契约的 ISqliteSearch，以及 relational.connection 契约的原生 IConnectionSource。公开契约使用 BCL 的 DbConnection／DbTransaction，不出现 SQLite 驱动类型。当前仅实现 SQLite；声明不依赖底层的需求不代表已经提供其他后端。

插件通过自身 DaoScope 取得工具，每次调用传入 DaoTable。scope.Table("fetch_details") 组合插件身份和本地名称，DaoNames 将两部分编码为安全 SQL 标识符。插件负责 Schema 创建及私有表，普通工具调用不要求逐表登记。原生连接由调用者释放，专用 SQL 可以保留 SQLite 特性。启动时实际探测 FTS5，文章插件将其作为索引所需的能力。

通用记录工具创建 key／JSON／可空到期时间表。Put 同时替换值和显式到期时间，null 表示长期保留。读取隐藏绝对毫秒时间戳已到期的记录；includeExpired 可查看尚未物理删除的记录。Keep 清除到期时间，也可用于已到期但未删除的记录；它不会将观察记录转为 文章或框架数据对象。

RSS 在解析入口记录成功 HTTP 抓取的 URL、字节数和完成时间，不保存响应正文、输入检查点或重放状态。文章单独长期保存。搜索暂时使用 FTS5 查询语法返回文章 ID；全文搜索通过 articles search 命令提供。

```sh
mise run run -- --database ./artifacts/rss/workspace.db --command articles --action search --query example
mise run run -- --database ./artifacts/rss/workspace.db --command rss --action details --include-expired true
mise run run -- --database ./artifacts/rss/workspace.db --command rss --action keep-detail --key RECORD_KEY
mise run run -- --database ./artifacts/rss/workspace.db --command dao --action list
mise run run -- --database ./artifacts/rss/workspace.db --command dao --action clean
mise run run -- --database ./artifacts/rss/workspace.db --command dao --action pause --owner rss --table fetch_details
mise run run -- --database ./artifacts/rss/workspace.db --command dao --action resume --owner rss --table fetch_details
```

统一清理只处理 PluginRegistration.Retentions 显式持久委托的目标；使用模板本身不授权清理。RSS 委托 fetch_details 表。存储插件在 HTTP 服务启动时清理一次，随后默认每分钟执行；其 cleanupInterval 配置影响调度，不决定数据到期时间。CLI 模式不启动后台任务。每个目标通过单条 DELETE 删除到期记录，暂不承诺工作量预算或分批处理；逐目标独立保留最近尝试、删除数量及错误。

委托在拥有者关闭／缺失时保留，只要提供者仍激活就继续清理，依据的是已有显式授权。提供者缺失时保留数据、到期时间、委托及诊断。dao pause/resume 跨重启保留，对后续轮次生效，已经开始的删除可能完成。暂停物理清理不会改变到期可见性。运行队列及同步输入仍只在内存中，不受这些保留策略处理。跨存储提交、持久输入占用及恢复继续在[存储与 DAO Note](../../.agents/notes/2026-11-17-mvp-storage-dao.zh.md)讨论。

## 查看和编辑图

```sh
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action nodes
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action definition --pipeline-name rss.ingest
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action plan --pipeline-name rss.ingest
```

nodes 描述已激活的具名端口、载荷类型和交付方式；definition 输出可编辑的图 JSON；plan 展示放置和端口连线。连接的格式如下：

```json
{ "source": "parse", "sourcePort": "output", "target": "store", "targetPort": "input" }
```

将 definition 输出保存为 JSON 文件并编辑，再运行或保存。plan 和 run/watch 接受 --pipeline-file 或 --pipeline-name。save 按名称替换存储定义并报告可执行性，保留缺失节点和 pluginId 来源；plan 报告缺失实现和归属，执行要求有效计划；当前运行保留自己的计划，后续运行读取新定义。--progress true 将节点和同步进度写入 stderr；Ctrl+C 取消 run/watch。

```sh
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action plan --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action run --pipeline-file ./pipeline.json --progress true
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action watch --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action save --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action relations --data-type rss.feed --data-key FEED_ID
```

## 添加节点与当前边界

用 PortDescriptor.Stream 或 Sync 声明 NodeDescriptor 的输入／输出，并注册异步函数。NodeContext.ReadEventsAsync 使用运行时取消令牌读取单个流输入或完整同步组；WriteAsync 向具名输出发送有类型的值，同步输出还需要 messageKey。继续关联时显式转发 key。StreamNodes.Source、Transform、Sink 是节点库中可选的单流便利封装，不是三套核心执行机制。

运行器通过 .NET Channels 实现端口 API，节点函数不必管理竞争读取任务或具体队列。普通消费者读取到输入结束，或显式安排取消。取消依赖协作，不能强行中断同步工作。Microsoft.Data.Sqlite 的 I/O 是同步的，解析与数据库工作在工作任务中执行。XmlReader 遵守声明编码，保留支持的 RSS/Atom 内容，禁用 DTD／外部实体；尚未覆盖所有扩展或 xml:base 规则。

运行进度不持久保存。保存的图不保存活动队列，也不提供崩溃恢复、检查点、持久重放或恰好执行一次。资源调度、未齐输入预算、复杂 key 关联、反馈图及远程执行留待后续实现。

公网身份与授权尚未接入，原型面向可信操作者；订阅 URI 会触发出站请求。HTTP 错误保留既有 ASP.NET Core google.rpc.Status JSON/ErrorInfo 与 AIP-193 外层 error 和 HTTP code 的差异。分页密钥位于数据库旁的 keys 目录，保留密钥才能让已有 token 在重启后继续使用。本轮不引入数据库 Schema 标准或历史升级层。
