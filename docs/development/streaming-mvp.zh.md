# 流式管线与 RSS 示例

[English](streaming-mvp.en.md) | 中文

## 目标与边界

RSS 阅读器使用 SQLite，演示具名流式与同步端口、本机执行和向其他设备派发操作。本文介绍可运行的原型。[框架设计](../../.agents/notes/2026-11-17-mvp-framework.zh.md) · [节点执行](../../.agents/notes/2026-11-17-mvp-node-execution.zh.md)

用户通过 Avalonia 桌面阅读器、CLI、进程内 API 或生成的 HTTP 客户端执行订阅刷新等业务操作。插件定义所需工作流，开发和调试插件时可以用 JSON 查看或编辑图。若旧原型的表结构或图定义不兼容，请使用新工作区。文章插件拆分前的工作区属于这种情况。

[桌面 MVP 指南](desktop-mvp.zh.md) 介绍可运行的 Avalonia 界面、真实 RSS／SQLite 操作、工作流查看和插件可用性。[UI 比较指南](ui-comparison.zh.md) 保留此前的独立实验。

## 框架与业务

| 部分 | 责任 |
| - | - |
| [Framework](../../src/LazyTuiHub.Framework/) | 通用数据关联、节点／端口注册、计划、异步交付、同步输入对齐、扇出、反压与取消。 |
| [节点库](../../src/LazyTuiHub.Nodes/) | 公共端口类型及源节点、转换节点和消费节点的编写工具。 |
| [插件 SDK](../../src/LazyTuiHub.Sdk/) | 注册、工作流调用、可选插件状态查询及生命周期 hook。 |
| [LazyTuiHub.Sdk.Avalonia](../../src/LazyTuiHub.Sdk.Avalonia/) | 注册页面、刷新工作区、使用自适应面板与动效，并将界面操作交给宿主跟踪。 |
| [插件装配](../../src/LazyTuiHub.Plugins/) | 可信 DLL 装配、声明及意图保留、工作流管理、任务与 hook 调用。 |
| [DAO 工具包](../../src/LazyTuiHub.Dao/) | 不依赖数据库驱动的契约、访问需求／能力及命名空间表目标。 |
| [SQLite 提供者](../../plugins/LazyTuiHub.Plugin.TuiHub.Storage.Sqlite/) | 必要存储插件，提供通用 JSON 记录／TTL、SQLite FTS5、原生连接及委托清理。 |
| [核心插件](../../plugins/LazyTuiHub.Plugin.TuiHub.Core/Backend/) | 发行清单要求的问候功能及内置节点注册。 |
| [RSS 插件](../../plugins/LazyTuiHub.Plugin.TuiHub.Rss/Backend/) | 订阅、定时抓取、抓取明细和 RSS／Atom 解析。 |
| [文章插件](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/Backend/) | 各类来源的文章入库、全文索引、已读状态和阅读接口。 |
| [游戏库与资料来源](../../.agents/notes/2026-11-23-mvp-games.zh.md) | 管理个人游戏、分类和游玩时段，通过 Steam、Bangumi、VNDB 工作流检索和导入资料。 |
| [Host](../../src/LazyTuiHub/) | 启动、插件／工作流／DAO CLI、桌面／Web 装配、共享 DI 基础设施及生命周期。 |
| [CLI](../../src/LazyTuiHub.Cli/) | `mise run run:cli` 列出本地插件操作。传入 `--endpoint https://SERVER` 可远程登录、管理订阅和阅读文章。独立 CLI 连接远程服务。[CLI 与 Windows 7](../../.agents/notes/2026-11-23-mvp-win7-cli.zh.md) |
| [桌面层](../../src/LazyTuiHub.Desktop/)／[文章阅读界面](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/Gui/) | Avalonia 外壳与诊断／文章阅读器及可选的 RSS 订阅操作。 |

`PipelineDefinition` 描述处理流程，`DataReference` 标识业务对象，`DataRelation` 关联对象。业务插件需要登记数据关联时，将工作区连接源传给 `BusinessStorage`，在同一事务内提交业务写入、索引更新和框架关联。插件维护业务表，框架写入目录。进程内受信任的代码共同遵守这项约定。同步输入的关联键另有用途。

`rss.ingest` 依次连接 `builtin.timer`、`rss.feed-requests`、`builtin.http-fetch`、`rss.parse`、`articles.store` 和 `articles.output`。其他插件可以接收解析输出。RSS 在解析前记录抓取明细，Articles 逐篇提交文章。刷新随后中断，已提交的数据仍然可读。两者也用工作流实现阅读操作。本地和 HTTP 调用分别通过相同的 `IRssApi`、`IArticleApi` 接口访问对应服务。[API 参考](../api/index.md)

接入新的文章来源时，让节点输出 [ArticleDraft](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/Api/ArticleDraft.cs)，并连接 articles.store。来源资源名与来源内的条目 ID 共同标识文章。例如 notebooks/a 和 notebooks/b 可以各自使用条目 ID 1。同一来源再次提交该条目时会更新内容、保留文章名称和已读状态。若数据包附有已登记的来源引用，入库会同时登记来源与文章的关联。

在独立仓库中使用 NuGet 开发插件时，直接引用 `LazyTuiHub.Sdk`。该包提供注册契约，并自动导入从项目声明生成嵌入清单的构建目标。需要在 ClientGui 入口注册页面时，再引用 `LazyTuiHub.Sdk.Avalonia`。将 Avalonia 类型放在 GUI 入口和视图中，同一 DLL 的其他入口即可在 CLI 和服务端使用。

每个插件根目录只有一个项目，默认按包含发布者的完整插件 ID 取点分 PascalCase 项目名。.NET SDK 默认将项目名用于 AssemblyName 和 RootNamespace。插件自有类型使用该根命名空间，例如游戏插件公开 LazyTuiHub.Plugin.TuiHub.Games.GameEntry。项目通过 `$(RootNamespace)` 引用入口类型。插件作者在[项目文件](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/LazyTuiHub.Plugin.TuiHub.Articles.csproj)中声明 PluginId、Title、可选的 PluginVersion 及 PluginEntryPoint。Server 注册服务端业务，Client 注册客户端共用操作，ClientGui 和 ClientCli 各自注册界面交互。宿主通过 BundledPlugin 选择发行成员，构建生成 DLL 内的清单和发行索引。代码通过注册上下文的 Id 或生成的 PluginProject.Id 获取身份。NuGet 包名可通过 PackageId 单独修改。[插件 Note](../../.agents/notes/2026-11-17-mvp-plugins.zh.md)

报表插件可以引用文章库，并在游戏库可用时显示统计。文章项目改名或迁出后，可调整引用路径；修改其 PluginId 后重新构建，报表会取得新 ID。改用 NuGet 包时，把 PluginRelation 放在 PackageReference 上，构建从包中读取目标身份。只在运行时使用对方时，可用 PluginReference 直接引用其稳定 ID。

```xml
<ItemGroup>
  <ProjectReference Include="../LazyTuiHub.Plugin.TuiHub.Articles/LazyTuiHub.Plugin.TuiHub.Articles.csproj" PluginRelation="Requires" />
  <PluginReference Include="lazytuihub.plugin.tuihub.games"
                   PluginRelation="CompatibleWith" PluginVersionRange="[1.0.0,2.0.0)" />
</ItemGroup>
```

宿主先调用文章插件的入口，再调用报表插件的入口。即使游戏插件也声明了对报表插件的兼容范围，两者仍可按任意顺序启动。报表插件在用户打开游戏统计时再获取游戏能力。游戏插件缺席或停用时，报表仍可启动。若启用的游戏插件版本为 2.0.0，宿主会阻止报表插件启动，并报告所需范围及实际版本。

## 端口与执行计划

NodeRegistration.Register 接收具名、有类型的输入／输出声明和一个异步执行函数，每类端口可以为任意数量，包括零。连线记录 source、sourcePort、target、targetPort，数据类型与交付方式必须严格一致，object 不作为接收任意类型的端口。输出可以未连接，每个已声明输入需要生产者。

计划器合并插件节点必需的资源和工作流节点在 `requires` 数组里追加的需求，再与各实例独立登记的资源匹配。例如，`articles.store` 要求 `workspace.primary`，摘要工作流可以要求 `compute.batch`。图中只需填写逻辑节点、设置、需求和连线。计划列出每一步的需求、候选实例和无法读取清单的对端。运行已保存的定义时重新生成计划。保存时会保留缺少能力的定义并返回诊断。

## 多节点原型

准备 Docker 引擎和 Compose，运行 `mise -E development run test:multinode`。任务发布应用，在隔离网络中运行场景，将观测结果写入 `.local/multinode/result.json`，随后清理容器、网络及测试卷。[Compose 配置](../../tests/multinode/compose.yaml) 给出了完整示例。ARM64 引擎若缓存了 AMD64 的 .NET 镜像，可设置 `TEST_APP_PLATFORM=linux/amd64`。这个选项同时选择应用节点的发布目标和容器架构。原型按配置的对端地址推送任务。从节点轮询和跨设备插件清单校验是目标设计。[协调 Note](../../.agents/notes/2026-11-19-mvp-coordination.zh.md)

| 配置 | 用途 |
| --- | --- |
| `Cluster:Id`、`Cluster:Resources` | 当前实例的身份及可提供的具名资源。由持有权威文章库的实例登记 `workspace.primary`。单机模式默认使用本地工作区。 |
| `Cluster:Peers` | 可连接的工作节点地址及凭据，每项仅含 `Url` 和 `Token`。生成计划时向各工作节点查询资源与操作。 |
| `--worker true`、`Cluster:Token`、`Cluster:Export` | 启动工作节点端点，配置本机的 Bearer 凭据和允许导出的操作。只有已加载的包操作可以导出。 |
| `Cluster:FetchOrigins` | 此工作节点允许抓取的 HTTP 来源。工作节点不会跟随重定向。实例间通信使用 HTTPS，隔离的 Docker 测试网络显式启用 `Cluster:AllowInsecureHttp`。 |

协调端可直接调用本地实现，也可通过生成的 HTTP 客户端发送带引用的类型化数据包。当前只有注册为单流入、单流出的包操作才能跨实例。可重复转换按包先尝试本地，再按实例标识依次尝试符合条件的远端。其余流、同步组和队列由协调端维护。每份计划使用一次清单快照，新加入或恢复的实例在下次生成计划时可见。计划运行期间如有候选失效，可继续使用其他可达候选。

`articles.store` 等绑定所有者的操作要求恰好一个符合条件的所有者。写入确认丢失时返回结果未知，重新提交前应先核对所有者的数据。RSS 订阅和抓取明细保存在提交方工作区。文章及来源关联保存在计划选中的所有者上。[工作节点协议](../api/operations/WorkerService_ExecutePacket.md) 描述传输契约。

## 流式与同步交付

StreamInput 包含端口和 DataPacket。流数据在反压条件下尽快可读，框架不承诺消息或不同流之间的业务处理顺序。本地扇出将同一只读载荷引用分别交给各分支，转换时产生新结果；GC 回收已释放对象。这不构成跨进程共享内存。

节点通过 `SynchronizedInput.MessageKey` 取得消息键，通过只读的 `Values` 字典取得完整输入。发送方显式传入消息键。框架按键和端口分别排队，每个端口各取一个值，凑齐一组才交给节点。同一键可形成多组，不会自动去重。不同键按齐备顺序推进。多个生产者的业务轮次由插件自行关联。

同步保证成组可见与声明依赖，不保证物理同时收包或相对于流数据的时序。节点负责配置生效、状态和副作用。异步事件循环可以在读到完整配置组后替换局部配置，为随后处理的流数据取得快照，参见[混合输入示例](../../src/LazyTuiHub/Examples/WorkflowDemo.cs)。

每个节点有界入口的 bufferCapacity 默认为 16，满时等待；顺序扇出让慢分支反压上游。未齐同步值移到独立内存表，目前没有暂存数量／字节预算或落盘，因此有界入口不代表整体内存有界。HTTP fetch 缓存完整响应，XML 解析和 SQLite 也有各自成本。

每个端口在最后一个生产者结束后关闭。如果待齐组缺少已经关闭的端口，立即以节点、key 和缺失端口解释失败，即使另一条流仍开放。取消中断协作等待；节点函数返回后停止接收，后续写入失败而不挂起。节点失败取消其他节点并报告身份及原因；已提交数据保留，不回滚全图，也不自动重试节点。

## 运行示例

```sh
mise -E development run demo:workflow
mise -E development run demo:rss
mise run test
```

demo:workflow 不使用 HTTP 或 SQLite。节点处理流数据，同时接收两个同步配置输入；示例通过显式确认协调输入，验证配置未齐时流的结果仍为 2，完整配置更新后分别得到 7、13，最后取消全部任务。这些确认不为框架增加跨类别的时序保证。

demo:rss 自建本地 RSS fixture、临时 SQLite 和 HTTP 服务，验证本地／HTTP 一致、重复刷新、已读状态及跨业务分支，结束后移除临时数据。产品测试另行覆盖同步对齐、未齐组、严格端口匹配、反压、共享引用、合流结束、失败取消、XML、分页及重启。

## 使用 RSS 工作区

```sh
mise run run -- --command rss --action help
mise run run -- --database .local/rss/workspace.db --command rss --action add --feed-url https://example.com/feed.xml --title Example
mise run run -- --database .local/rss/workspace.db --command rss --action feeds
mise run run -- --database .local/rss/workspace.db --command rss --action refresh --name feeds/FEED_ID
mise run run -- --database .local/rss/workspace.db --command articles --action list --unread true
mise run run -- --database .local/rss/workspace.db --command articles --action read --name articles/ARTICLE_ID
```

将示例订阅地址替换为实际地址。`rss add` 返回订阅名称。`articles list` 返回文章名称和下一页的令牌，将令牌传给 `--page-token` 即可继续查询。`articles read` 将文章标为已读，并以 Protobuf JSON 输出内容，不执行其中的 HTML。

省略 `--database` 时，宿主使用本地应用数据目录下的 `LazyTuiHub/workspace.db`。单账户模式下，HTTP 或本地桌面宿主启动满足条件的插件任务。默认 RSS 定时器每 15 分钟触发一次。新建工作区时可设置 `--refresh-interval 00:05:00`，已有工作区则修改图中的定时器。`PeriodicTimer` 合并错过的触发。运行失败后记录错误，等一个刷新间隔再试。

```sh
mise run run -- --database .local/rss/workspace.db --urls http://127.0.0.1:5080
```

## 启用账户与资源授权

使用新建的 SQLite 工作区。主节点设置 `Access__Enabled=true`，并在首次初始化时通过 `Access__AdminUser` 和 `Access__AdminPassword` 提供管理员凭据。给主节点配置 HTTPS 地址和受客户端信任的证书。使用账户模式时，RSS 从显式刷新运行。

```sh
mise run run -- --command access --action login --endpoint https://primary.example --username admin
mise run run -- --database .local/account/workspace.db --command access --action create-user --username reader
mise run run -- --database .local/account/workspace.db --command access --action set-device-owner --device fetcher --username admin
mise run run -- --database .local/account/workspace.db --command access --action grant --username reader --uri lazytuihub://core/devices/fetcher/jobs --mode write
```

登录时终端隐藏密码。将结果中的会话令牌通过 `Access__Session` 传给主节点上运行的管理命令、本地 CLI 或桌面。远程请求在 `Authorization` 头中携带 Bearer 令牌，默认访问自己的账户。需要访问获分享的资源时，用 `X-LazyTuiHub-Account` 选择目标账户。分享订阅可将 `--uri` 设为 `lazytuihub://lazytuihub.plugin.tuihub.rss/accounts/ACCOUNT_ID/feeds/FEED_ID` 并授予 `read`。撤权将同一命令的 `--action` 改为 `revoke`，省略 `--mode`。远程会话用同一 HTTPS 地址执行 `--action logout` 撤销。

账户模式下，协调器根据资源清单和用户的设备任务入口写权限选择抓取端。`--device` 使用实例配置中的 ID，独立运行的实例使用 `local`。文章写入按工作区绑定路由，并检查文章集合的写权限。参与该工作流的工作节点也要设置 `Access__Enabled=true`。[账户与 URI 授权 Note](../../.agents/notes/2026-11-19-mvp-account-identity.zh.md) · [会话 API](../api/operations/SessionService_AuthenticateUser.md)

## 冷启动插件

宿主从应用输出目录的 plugins/plugins.json 加载显式清单，--plugin-directory DIR 可选其他目录。构建和发布将每个选定插件的入口 DLL 和依赖放入 `plugins/<id>/`。插件共享宿主依赖基线和默认程序集上下文；附加依赖需随插件部署，原型没有私有依赖版本、原生依赖部署系统、热更新或沙箱。[插件 Note](../../.agents/notes/2026-11-17-mvp-plugins.zh.md) 记录边界与调研。

```sh
mise run run -- --database .local/rss/workspace.db --command plugins --action list
mise run run -- --database .local/rss/workspace.db --command plugins --action inspect --plugin lazytuihub.plugin.tuihub.rss
mise run run -- --database .local/rss/workspace.db --command plugins --action disable --plugin lazytuihub.plugin.tuihub.rss
mise run run -- --database .local/rss/workspace.db --command plugins --action enable --plugin lazytuihub.plugin.tuihub.rss
mise run run -- --database .local/rss/workspace.db --command plugins --action configure --plugin lazytuihub.plugin.tuihub.rss --file ./rss-settings.json
```

修改在重启后生效。list 分别展示保存的启用意图、文件可用性、激活结果和任务资格；inspect 包含保留的节点／端口／数据类型及 DAO 声明和不透明配置。RSS 配置可写为 {"refreshInterval":"00:05:00","detailRetention":"7.00:00:00"}，未知字段保留。detailRetention 可省略，默认 null（长期保留）；正时长为新抓取观察记录设置到期时间，修改配置不改写已有记录。周期用于初始化新默认图及控制失败重试等待；已有定时器周期需修改图内节点参数，存储的图优先于新默认值。

插件禁用或缺失时不提供业务命令／路由／节点，也不启动任务。图查看与编辑、关联、配置及插件诊断仍然可用，包括 core 或 storage.sqlite 缺失的情况。这两个必要插件不能通过正常配置关闭。DAO 提供者缺失会阻止 RSS 激活，诊断指出不可用的契约、实例、档位或底层，并保留需求和数据供恢复。移走和放回 DLL 不改启用意图，不删除数据、已读状态、图、关联或任务声明。工作流缺少实现时停止执行并说明原因。--command workflow --action list 展示全部存储图及可执行性。归属来自保留声明或图中显式 pluginId，没有这些信息时保持未知。

--command plugins --action cleanup 或 uninstall --plugin ID 显式调用可选 hook。禁用插件可在冷启动作为维护目标提供 hook，不激活业务能力。代码或 hook 缺失时报告错误；RSS 目前均未提供。框架接受 hook 完成，不证明业务清理完整。uninstall 不删除 DLL 或目录记录，禁用及缺失不自动调用任何清理 hook。

插件在 Server 入口注册服务、节点、数据类型、路由和任务。SDK 从项目声明生成清单。宿主按必需依赖排序、匹配 DAO 能力，再检查各任务所需的工作流。`IWorkflowExecutor` 执行已保存的定义，只有缺少定义时才写入默认图。插件处理重试并响应取消。目录在重启后保留声明，消息队列、同步输入和节点状态驻留内存。

## DAO 工具与保留

插件通过 registration.Daos.Requirements 声明所需档位、契约、访问实例及可选底层。当前实例为 workspace。提供者支持三档：通用 records.ttl 契约的 IRecordDao、sqlite.fts5 契约的 ISqliteSearch，以及 relational.connection 契约的原生 IConnectionSource。公开契约使用 BCL 的 DbConnection／DbTransaction，不出现 SQLite 驱动类型。当前仅实现 SQLite；声明不依赖底层的需求不代表已经提供其他后端。

插件通过自身 DaoScope 取得工具，每次调用传入 DaoTable。scope.Table("fetch_details") 组合插件身份和本地名称，DaoNames 将两部分编码为安全 SQL 标识符。插件负责 Schema 创建及私有表，普通工具调用不要求逐表登记。原生连接由调用者释放，专用 SQL 可以保留 SQLite 特性。启动时实际探测 FTS5，文章插件将其作为索引所需的能力。

通用记录工具创建 key／JSON／可空到期时间表。Put 同时替换值和显式到期时间，null 表示长期保留。读取隐藏绝对毫秒时间戳已到期的记录；includeExpired 可查看尚未物理删除的记录。Keep 清除到期时间，也可用于已到期但未删除的记录；它不会将观察记录转为 文章或框架数据对象。

RSS 在解析入口记录成功 HTTP 抓取的 URL、字节数和完成时间，不保存响应正文、输入检查点或重放状态。文章单独长期保存。搜索暂时使用 FTS5 查询语法返回文章 ID；全文搜索通过 articles search 命令提供。

```sh
mise run run -- --database .local/rss/workspace.db --command articles --action search --query example
mise run run -- --database .local/rss/workspace.db --command rss --action details --include-expired true
mise run run -- --database .local/rss/workspace.db --command rss --action keep-detail --key RECORD_KEY
mise run run -- --database .local/rss/workspace.db --command dao --action list
mise run run -- --database .local/rss/workspace.db --command dao --action clean
mise run run -- --database .local/rss/workspace.db --command dao --action pause --owner lazytuihub.plugin.tuihub.rss --table fetch_details
mise run run -- --database .local/rss/workspace.db --command dao --action resume --owner lazytuihub.plugin.tuihub.rss --table fetch_details
```

RSS 通过 `PluginRegistration.Retentions` 委托清理 `fetch_details`。宿主启用插件任务时，存储插件在启动时清理受托表，之后默认每分钟清理一次。`cleanupInterval` 可调整调度间隔。每轮对每张表执行一次 `DELETE`，分别记录结果、删除数量或错误。CLI 命令及账户模式的宿主不启动后台清理任务。

委托在拥有者关闭／缺失时保留，只要提供者仍激活就继续清理，依据的是已有显式授权。提供者缺失时保留数据、到期时间、委托及诊断。dao pause/resume 跨重启保留，对后续轮次生效，已经开始的删除可能完成。暂停物理清理不会改变到期可见性。运行队列及同步输入仍只在内存中，不受这些保留策略处理。跨存储提交、持久输入占用及恢复继续在[存储与 DAO Note](../../.agents/notes/2026-11-17-mvp-storage-dao.zh.md)讨论。

## 插件开发时查看和编辑图

```sh
mise run run -- --database .local/rss/workspace.db --command workflow --action nodes
mise run run -- --database .local/rss/workspace.db --command workflow --action definition --pipeline-name rss.ingest
mise run run -- --database .local/rss/workspace.db --command workflow --action plan --pipeline-name rss.ingest
```

nodes 描述已激活的具名端口、载荷类型和交付方式；definition 输出可编辑的图 JSON；plan 展示放置和端口连线。连接的格式如下：

```json
{ "source": "parse", "sourcePort": "output", "target": "store", "targetPort": "input" }
```

将 definition 输出保存为 JSON 文件并编辑，再运行或保存。plan 和 run/watch 接受 --pipeline-file 或 --pipeline-name。save 按名称替换存储定义并报告可执行性，保留缺失节点和 pluginId 来源；plan 报告缺失实现和归属，执行要求有效计划；当前运行保留自己的计划，后续运行读取新定义。--progress true 将节点和同步进度写入 stderr；Ctrl+C 取消 run/watch。

```sh
mise run run -- --database .local/rss/workspace.db --command workflow --action plan --pipeline-file ./pipeline.json
mise run run -- --database .local/rss/workspace.db --command workflow --action run --pipeline-file ./pipeline.json --progress true
mise run run -- --database .local/rss/workspace.db --command workflow --action watch --pipeline-file ./pipeline.json
mise run run -- --database .local/rss/workspace.db --command workflow --action save --pipeline-file ./pipeline.json
mise run run -- --database .local/rss/workspace.db --command workflow --action relations --data-type rss.feed --data-key FEED_ID
```

## 添加节点与当前边界

用 PortDescriptor.Stream 或 Sync 声明 NodeDescriptor 的输入／输出，并注册异步函数。NodeContext.ReadEventsAsync 使用运行时取消令牌读取单个流输入或完整同步组；WriteAsync 向具名输出发送有类型的值，同步输出还需要 messageKey。继续关联时显式转发 key。StreamNodes.Source、Transform、Sink 是节点库中可选的单流便利封装，不是三套核心执行机制。

运行器通过 .NET Channels 实现端口 API，节点函数不必管理竞争读取任务或具体队列。普通消费者读取到输入结束，或显式安排取消。取消依赖协作，不能强行中断同步工作。Microsoft.Data.Sqlite 的 I/O 是同步的，解析与数据库工作在工作任务中执行。XmlReader 遵守声明编码，保留支持的 RSS/Atom 内容，禁用 DTD／外部实体；尚未覆盖所有扩展或 xml:base 规则。

运行进度不持久保存。保存的图不保存活动队列，也不提供崩溃恢复、检查点、持久重放或恰好执行一次。容量调度、未齐输入预算、复杂 key 关联、反馈图及跨设备同步执行留待后续实现。

单账户模式信任操作者。账户模式要求业务请求通过 HTTPS 携带用户会话。两种模式下，订阅地址都可能触发出站 HTTP 请求。ASP.NET Core 返回含 `ErrorInfo` 的 `google.rpc.Status`，与 AIP-193 的 HTTP 错误外层结构和状态码字段仍有差异。保留数据库旁的 `keys/` 目录可让分页令牌跨重启有效。MVP 未提供废弃原型 Schema 的迁移路径。
