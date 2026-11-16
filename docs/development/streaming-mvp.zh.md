# 流式管线与 RSS 示例

[English](streaming-mvp.en.md) | 中文

## 目标与边界

MVP 用 RSS 阅读器和 SQLite 探索跨业务复用节点。首轮本地运行器支持具名流式／同步端口和可执行的本地计划。[框架 Note](../../.agents/notes/2026-11-17-mvp-framework.zh.md) 与[节点执行 Note](../../.agents/notes/2026-11-17-mvp-node-execution.zh.md) 区分当前选择和后续设计。MVP 合入主分支前，Note 保持可自由编辑的提案状态；每轮将代码和对应 Note 一起提交。

当前通过 CLI、进程内 API 和生成的 HTTP 客户端交互，没有可视化图编辑器或分布式执行。图定义和数据库 Schema 没有原型兼容承诺；尝试不兼容的图变更时使用新的工作区。

## 框架与业务

| 部分 | 责任 |
| - | - |
| [Framework](../../src/LazyTuiHub.Framework/) | 通用数据关联、节点／端口注册、计划、异步交付、同步输入对齐、扇出、反压与取消。 |
| [节点库](../../src/LazyTuiHub.Nodes/) | 定时器、HTTP fetch、有类型的输出消费者和常见源／转换／消费辅助，通过普通核心注册接入。 |
| [RSS](../../src/LazyTuiHub.Rss/) | 订阅、RSS/Atom 解析、文章存储、查询和已读状态。 |
| [Host](../../src/LazyTuiHub/) | 组合模块、CLI、后台刷新及协议适配。 |

PipelineDefinition 表示处理关系；DataReference 与 DataRelation 表示持久数据身份及关联。RSS 在自己的表中保存业务字段，文章和通用关联在同一 SQLite 事务中提交。这些身份不同于同步输入 key，也不构成分布式对象存储。

rss.ingest 连接 builtin.timer、rss.feed-requests、builtin.http-fetch、rss.parse、rss.store 和有类型的输出消费者。其他模块可以在解析后分支，不必修改运行器。阅读及其他直接 RSS 操作目前也经过工作流，让工作流成为可选能力仍待后续实现。本地和 HTTP 调用共享 IRssApi；字段、路由、分页、去重及错误见[生成的 API 参考](../api/index.md)。

## 端口与执行计划

NodeRegistry.Register 接收具名、有类型的输入／输出声明和一个异步执行函数，每类端口可以为任意数量，包括零。连线记录 source、sourcePort、target、targetPort，数据类型与交付方式必须严格一致，object 不作为接收任意类型的端口。输出可以未连接，每个已声明输入需要生产者。

PipelinePlanner 生成 PipelinePlan，绑定节点实现、端口连接、本地放置和缓冲容量。当前后端要求 DAG，所有节点放在 local。创建／保存图时检查计划，运行定义时生成新计划，也可直接执行已有计划。资源清单、远程放置及远程传输尚未实现。

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
mise run run -- --rss help
mise run run -- --database ./artifacts/rss/workspace.db --rss add --feed-url https://example.com/feed.xml --title Example
mise run run -- --database ./artifacts/rss/workspace.db --rss feeds
mise run run -- --database ./artifacts/rss/workspace.db --rss refresh --name feeds/FEED_ID
mise run run -- --database ./artifacts/rss/workspace.db --rss articles --unread true
mise run run -- --database ./artifacts/rss/workspace.db --rss read --name feeds/FEED_ID/articles/ARTICLE_ID
```

将 feed URI 替换成实际地址。add 返回订阅名称，articles 返回文章名称和后续分页 token，通过 --page-token 继续查询。read 标记已读并输出 Proto JSON，不执行 HTML。

不传 --database 时，工作区位于系统 LocalApplicationData 下的 LazyTuiHub/workspace.db。启动 HTTP 服务同时启动后台刷新，首次创建图时默认周期为 15 分钟，可用 --refresh-interval 00:05:00 修改；已有图定义保留。定时器积压采用 PeriodicTimer 合并，不重放每个错过的时刻。刷新失败记录日志，在下一周期重试。

```sh
mise run run -- --database ./artifacts/rss/workspace.db --urls http://127.0.0.1:5080
```

## 查看和编辑图

```sh
mise run run -- --database ./artifacts/rss/workspace.db --rss nodes
mise run run -- --database ./artifacts/rss/workspace.db --rss pipeline --pipeline-name rss.ingest
mise run run -- --database ./artifacts/rss/workspace.db --rss plan --pipeline-name rss.ingest
```

nodes 描述具名端口、载荷类型和交付方式；pipeline 输出可编辑的图 JSON；plan 展示放置和端口连线。连接的格式如下：

```json
{ "source": "parse", "sourcePort": "output", "target": "store", "targetPort": "input" }
```

将 pipeline 输出保存为 JSON 文件并编辑，再运行或保存。plan 和 run/watch 接受 --pipeline-file 或 --pipeline-name。save-pipeline 检查计划后按名称替换存储定义；当前运行保留自己的计划，后续运行读取新定义。--progress true 将节点和同步进度写入 stderr；Ctrl+C 取消 run/watch。

```sh
mise run run -- --database ./artifacts/rss/workspace.db --rss plan --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --rss run --pipeline-file ./pipeline.json --progress true
mise run run -- --database ./artifacts/rss/workspace.db --rss watch --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --rss save-pipeline --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --rss relations --feed-id FEED_ID
```

## 添加节点与当前边界

用 PortDescriptor.Stream 或 Sync 声明 NodeDescriptor 的输入／输出，并注册异步函数。NodeContext.ReadEventsAsync 使用运行时取消令牌读取单个流输入或完整同步组；WriteAsync 向具名输出发送有类型的值，同步输出还需要 messageKey。继续关联时显式转发 key。StreamNodes.Source、Transform、Sink 是节点库中可选的单流便利封装，不是三套核心执行机制。

运行器通过 .NET Channels 实现端口 API，节点函数不必管理竞争读取任务或具体队列。普通消费者读取到输入结束，或显式安排取消。取消依赖协作，不能强行中断同步工作。Microsoft.Data.Sqlite 的 I/O 是同步的，解析与数据库工作在工作任务中执行。XmlReader 遵守声明编码，保留支持的 RSS/Atom 内容，禁用 DTD／外部实体；尚未覆盖所有扩展或 xml:base 规则。

运行进度不持久保存。保存的图不保存活动队列，也不提供崩溃恢复、检查点、持久重放或恰好执行一次。资源调度、未齐输入预算、复杂 key 关联、反馈图及远程执行留待后续实现。

公网身份与授权尚未接入，原型面向可信操作者；订阅 URI 会触发出站请求。HTTP 错误保留既有 ASP.NET Core google.rpc.Status JSON/ErrorInfo 与 AIP-193 外层 error 和 HTTP code 的差异。分页密钥位于数据库旁的 keys 目录，保留密钥才能让已有 token 在重启后继续使用。本轮不引入数据库 Schema 标准或历史升级层。
