---
status: proposed
category: architecture
---
# MVP 存储、DAO 访问与数据保留

[English](2026-11-17-mvp-storage-dao.en.md) | 中文

## 范围与状态

本文讨论关系库、文档库、键值库、文件和对象存储、搜索与向量引擎，以及外部数据服务的访问方式。[框架](2026-11-17-mvp-framework.zh.md) · [节点执行](2026-11-17-mvp-node-execution.zh.md) · [插件归属](2026-11-17-mvp-plugins.zh.md)

SQLite 已实现三档访问、表命名空间和 TTL 记录。插件维护自己的 Schema。MVP 未规定 Schema 设计标准，也未承诺兼容旧原型。

## 数据归属与账户隔离

插件命名空间确定表的归属。表内资源还需遵循账户边界，三档 DAO 的调用都应从可信上下文取得账户范围。共享物理库的读写、查询、索引和数据关联同样按账户隔离。首版所有者访问及双账户验收见[账户服务与权限 Note](2026-11-19-mvp-account-identity.zh.md)。

插件可按需在自己的命名空间内建表，框架不限制表数。框架单独记录数据归属，即使缺少存储提供者，仍可读取目录、诊断故障。
## 按封装程度划分的 DAO 三档访问

| 档位 | 对使用方提供什么 | 需求与可移植边界 |
| - | - | - |
| 跨底层实现一致封装 | 不同底层实现遵守相同的操作和行为契约，例如记录、内容读写、搜索或 TTL 数据集。 | 使用方依赖公共契约；只匹配实际实现了该契约的提供者。统一语义的具体范围由每个契约定义。 |
| 针对具体底层的封装 | 为某种底层实现提供专项接口，保留特有类型、查询能力和行为，例如 FTS5 或 PostGIS 专项服务。 | 使用方声明相应专项服务及必要条件；不同底层的专项接口可以不同。 |
| 原生访问 | 直接取得数据库连接、SDK 客户端、文件句柄或访问会话，使用原生操作。 | 使用方声明所需后端、扩展和实例条件，自行负责具体操作。框架承担约定的绑定与访问生命周期。 |

访问档位、后端类型和数据形态可以分别选择。

同一插件可以混用三档，提供者按实际能力声明支持的档位。三档均遵循插件完全信任模型，框架元数据统一由框架写入。

框架共同处理提供者发现、配置、访问实例、需求匹配、绑定生命周期和诊断。事务、排序、分页、TTL、搜索语义等按具体契约声明，不假定所有 DAO 都具有数据库语义。工作流端口和跨设备执行通信仍由执行层维护；DAO 服务可由业务直接调用，也可通过节点参与可选工作流。

公共基础契约应与 SQLite 原生类型分离，专项封装和原生入口可以暴露各自后端需要的类型。将 SqliteConnection 替换为 DbConnection 只能减少驱动类型依赖，SQL、DDL、索引和事务差异仍需具体实现处理。成熟驱动或 ORM 可作为实现手段，当前未选定统一 ORM，也未设计自有通用查询语言。

## DAO 工具包与调用目标

插件维护自己的表、Schema 和索引，调用 DAO 时传入目标。跨后端的公共契约规定同一操作在各实现中的行为。

例如，RSS 通过 `DaoTable(namespace, name)` 传入抓取明细表。其他插件可将自己的表传给同一套 `IRecordDao`。插件调用 `CreateTable` 创建包含键、JSON 值和可空 `expires_at` 的记录表，再用 `Put`、`Read`、`Keep` 和 `DeleteExpired` 操作记录。需要自定义列或查询的 RSS 表通过原生连接访问。

这种调用方式延伸到所有 DAO：文档访问传入集合，搜索传入索引，对象／文件访问传入相应位置或对象目标。三档封装方式继续有效，不强迫这些目标具有同一种表结构。

参与提供者匹配的是所需 DAO 工具、连接能力与访问实例。具体表作为操作参数传入，调用工具本身不要求框架预先逐表登记、推断 Schema 或接管业务表生命周期。需要统一后台保留或跨插件数据协作时，另外显式提供相应目标、策略及必要描述。

## 声明、检查与执行计划

插件通过 `DaoRequirement` 命名需求，声明 `Mode`、`Contract`、`Instance` 及可选的 `Backend`。框架用这些字段匹配提供者的 `DaoCapability`，找不到或匹配到多个候选时说明原因。调用方通过 `DaoScope.Get` 取得服务。原型只配置一个名为 `workspace` 的共享 SQLite 实例。

插件在 `Configure` 阶段登记声明。提供者先发布能力，使用方再激活，清单顺序不影响绑定。SQLite 提供者成功创建临时 FTS5 表后才发布搜索能力。RSS 需要原生连接和记录工具，文章插件需要原生连接和 FTS5。缺失能力会阻止需要它的插件启动，框架目录仍可访问。

搜索需求应指定涵盖所需分词、排序和查询行为的契约。两个提供者都声明“全文搜索”，仍可能提供不同的查询行为。

提供者／连接能力的匹配与具体表的存在、结构适配分别处理。框架检查显式声明的前提；运行时才确定的访问目标，其实际访问结果在使用处解释。具体表作为调用参数，并不自动成为插件启动依赖。

插件依赖、DAO 需求和工作流连线分别声明。宿主装配插件时绑定 DAO，计划器根据已激活的节点构建计划。缺少节点实现时保留已存的图及归属信息。按节点、运行或远端实例重新绑定 DAO 仍待设计。

绑定只说明检查时满足了声明的需求。执行时仍可能断开连接、遇到 SQL 错误或收不到外部事务的提交确认。重试遵循协议与节点契约。框架以 JSON 保存需求及归属，无需加载插件即可诊断。

## 命名空间与所有权

以稳定的插件身份划分逻辑命名空间，插件在其中按需求定义表、字段、索引、约束及查询。框架维护命名空间归属及必要名称映射，私有业务表访问不以逐表登记为前提。索引、视图等对象同样需要避免命名冲突；数据库表之外的文件位置、对象键或服务资源，按实际后端表达对应绑定。

`DaoNames` 将命名空间和本地名分别作 UTF-8 十六进制编码，以固定前缀及分隔符组成表／索引名称。它保留正常 Unicode 名称与大小写差异，避免下划线拼接歧义，并使动态 SQL 标识符仅含固定前缀和十六进制字符；记录值与搜索语句仍使用参数。该映射用于当前 SQLite 实现，其他后端需按自身约束适配。SQLite 的 schema 名称对应 main、temp 或附加数据库，不能直接套用独立命名空间的假设。[SQLite 名称解析](https://www.sqlite.org/lang_naming.html)

数据拥有者、DAO 提供者及模板／清理能力提供者可能不同。RSS 的明细数据仍归 RSS 所有，使用通用托管模板不会把业务所有权转交给存储插件。

框架独占其元数据写入；业务和存储插件通过约定接口提交声明、业务结果及生命周期变更意图，由框架登记。原生访问仍须遵守所有权契约。当前同进程、完全信任的条件下，依靠可信代码契约维护归属；数据库权限隔离及 SQL 拦截不属于当前方案。

## 持久化、保留期限与数据引用

是否落盘、保留多久和存放在哪里分别约定。最近一段时间的中间明细可以持久化；长期业务结果可以同样位于 SQLite，也可能在其他存储中。

| 数据角色 | 存储责任与保留方向 |
| - | - |
| 框架元数据 | 框架管理插件／数据归属、关系、工作流及必要目录，缺失实现时仍可读取。 |
| 业务数据 | 业务插件维护字段、约束、查询及业务保留要求，避免把复杂业务全部压成统一 KV 文档。 |
| 内容与快照 | 文件、附件、原始抓取等可使用独立内容存储；身份、归属与可用位置需要关联。 |
| 运行状态与记录 | 执行计划、结果、输入快照、排队消息、同步组和节点状态具有不同的持久化要求。 |
| 可回收数据 | 由明确的保留策略授权回收；不能仅因被称作“中间数据”或“缓存”就推断可丢弃。 |

业务对象、不可变快照、存储位置和运行时引用各自标识。文章更新标题或正文后仍沿用原 ID，重放旧输入需要当时的快照和上下文。同步输入的消息键只用于配对。

进程内继续通过只读对象引用高效传递，不因引入持久存储而强制每条消息先落盘。有限重放需要保留输入、配置、节点实现信息及影响状态的同步输入和消费顺序。活动队列、未齐同步组与节点状态的恢复仍按框架／节点 Note 继续讨论，TTL 不自动解决这些问题。

## 模板工具与保留机制

`IRecordDao` 支持长期记录和有期限的记录。`Read` 按键排序。需要自定义列、索引、筛选或分页的插件可使用后端专用接口或原生连接。

必需的 Storage.Sqlite 插件提供记录工具、FTS5 搜索、原生 `IConnectionSource` 和清理任务。提供者 DLL 缺失时，框架仍可读取自己的 SQLite 元数据并提供诊断。公共 DAO 契约独立于 SQLite 驱动。宿主启用插件任务时，清理在启动时运行，之后按 `cleanupInterval` 调度，默认每分钟一次。每条记录单独设置到期时间。

插件可自行调用 TTL 工具，也可通过 `PluginRegistration.Retentions` 指定记录 DAO 和表，委托后台清理。宿主按拥有者和目标保存委托。`dao pause` 和 `dao resume` 从后续清理轮次起生效，重启后沿用。已开始的清理可执行完毕。永久删除委托和跨实例调度仍待设计。

框架只能依据明确的保留契约执行相应生命周期操作。复杂业务表、外部内容、特殊索引或多表关系的清理责任由对应实现声明；登记归属或采用某个访问档位本身不授予通用删除权限。框架不会仅凭表名猜测 TTL 列或业务依赖。

### 记录到期

中间明细显式声明过期时刻，空时刻表示长期保留。RSS 抓取明细的 detailRetention 默认未设置，因此不会自动过期；配置正时长后，以抓取完成时刻计算新记录期限，配置变化不改写旧记录。没有统一的数据大小、表数或保留时长上限。

记录工具保存毫秒精度的绝对 expiresAt，Put 显式写入或替换记录及期限，读取不续期。Read 默认隐藏 expiresAt 小于等于传入 now 的记录，诊断读取可显式包含它们。DeleteExpired 仅删除目标表中已经到期的记录；Keep 将仍存在记录的期限置空，已到期但尚未删除的记录也可保留，已删除则返回未找到。当前每目标执行一条删除语句，没有承诺精确删除时刻；大规模回收的批次和时间预算留待实际负载细化。Flink 对刷新、可见性和清理方式的分离仍作为参考。[Flink State TTL](https://nightlies.apache.org/flink/flink-docs-stable/docs/dev/datastream/fault-tolerance/state/#state-time-to-live-ttl)

RSS 每次完成抓取后记录 URI、字节数和完成时间，明细不含原始正文。清理这些私有记录后，业务对象及其关系仍然保留。若将 TTL 用于执行输入，还须约定如何保留排队消息和未齐的同步组、沿依赖关系延长保留期，以及崩溃后何时释放。这些规则和节点状态持久化仍待设计。

过期后是否保留精简的状态或来源记录、如何处理关系及特殊索引，需要独立约定；不能假定删除主体后其引用自然有效。SQLite 删除通常留下可复用页面，数据库文件收缩属于另一项维护操作。[SQLite VACUUM](https://www.sqlite.org/lang_vacuum.html)

### 与插件启停保留约定的关系

禁用或缺失本身不构成清理条件；没有明确保留策略的数据继续遵守插件 Note 的保留边界。常规 TTL 是预先声明的数据生命周期，独立于显式 cleanup／uninstall hook，不能借自动保留任务调用这些维护 hook。

数据拥有者关闭或缺失后，只要存储提供者仍可用，已委托的清理就会继续。提供者缺失时，目录显示 `unavailable`，数据、委托、暂停设置和原到期时刻都保留。提供者恢复后，DAO 按原到期时刻处理读取。

插件不可用时，其自行运行的 TTL 任务停止。统一清理器只访问受托目标，分别记录上次尝试时间、删除数量及错误。某个目标清理失败，其他目标仍可继续。

## 事务、后端选择与公网设备

框架元数据、RSS 订阅和文章共用 SQLite 工作区。`BusinessStorage` 接收显式的 `IConnectionSource`。使用框架工作区的同一数据源时，业务写入与元数据登记在一个事务中提交，文章的全文索引也随文章一起提交。其他数据源需要另行协调。SQLite 在 WAL 模式下使用 `ATTACH` 无法保证多个数据库在崩溃后整体提交或回滚。[SQLite WAL](https://www.sqlite.org/wal.html)

支持多 DAO 提供者和按数据集／功能绑定实例是设计方向，不等于所有数据能透明换库或共享一个事务。数据库与文件、外部索引或其他服务分别提交时，需要确定权威状态、元数据登记顺序、失败与未知结果的处理。当前同库原子性不能直接推广到这些组合。

数据拥有者负责相应 Schema 和实现变化，MVP 不因此新增通用迁移平台或历史兼容要求。ADO.NET 公共类型减少类型耦合，ORM 可以辅助实现；即使使用 EF Core，多提供程序的迁移也仍需分别维护。[ADO.NET](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)、[EF Core 多提供程序迁移](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers)

设备可提供不同的数据服务。主节点按资源归属选择具备条件的执行设备，并解释缺失的绑定。各实例访问本机存储。SQLite WAL 要求进程位于同一主机，远程操作通过服务连接执行。DAO 在远端重绑定及数据迁移仍待实现。[多节点协调](2026-11-19-mvp-coordination.zh.md)

备份恢复应按完整工作区考虑：数据库、需要持久保存的内容、配置和必要密钥。多存储的一致备份、文件与目录提交、导入导出及数据搬迁仍未设计；原生或专项 DAO 的恢复要求需要由相应实现提供。

## 调研依据与采用范围

| 项目 | 借鉴及边界 |
| - | - |
| [ComfyUI 资产系统](https://github.com/Comfy-Org/ComfyUI/blob/master/app/assets/docs/README.md) | 可选资产系统分离逻辑记录与磁盘内容，缺失内容保留记录；借鉴身份、位置和可用性分离。 |
| [Factorio Storage](https://lua-api.factorio.com/latest/auxiliary/storage.html) | mod 各有 storage，由引擎随存档持久化；借鉴逻辑归属与集中存储协作，不据此推导插件移除无损。 |
| [DeepSeek Harness 存储](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/storage/storage/README.zh.md) | 分离后端与领域数据形式；[领域层](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/storage/storage-domain/README.zh.md)目前缺少跨表事务和二级索引，其 KV 模型不直接替代复杂业务表；[会话持久化](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/session/session-persistence/README.zh.md)另行定义追加与 flush 保证。 |
| [Jellyfin 备份恢复](https://jellyfin.org/docs/general/administration/backup-and-restore/) | 数据库、配置和文件元数据共同参与恢复；借鉴工作区整体视角。 |
| [Ray 对象容错](https://docs.ray.io/en/latest/ray-core/fault_tolerance/objects.html) | 对象值与拥有者元数据分离，拥有者死亡可使引用失效；运行时对象机制与持久业务身份分别设计。 |
| [Flink 状态后端](https://nightlies.apache.org/flink/flink-docs-stable/docs/ops/state/state_backends/)及[检查点前提](https://nightlies.apache.org/flink/flink-docs-stable/docs/dev/datastream/fault-tolerance/checkpointing/) | 区分运行状态与检查点存储，恢复依赖可回放输入；TTL、落盘与执行恢复分别约定。 |
| [Npgsql 全文检索](https://www.npgsql.org/efcore/mapping/full-text-search.html)及[空间映射](https://www.npgsql.org/efcore/mapping/nts.html) | 展示公共访问体系内保留后端专项接口的方式，支持第二档的设计判断。 |

## 实现与验证

[DAO 契约与工具入口](../../src/LazyTuiHub.Dao/) 不引用 SQLite 驱动；[SQLite 提供者插件](../../plugins/LazyTuiHub.Plugin.TuiHub.Storage.Sqlite/) 实现统一记录工具、FTS5 专项服务及原生连接源。[SqliteDatabase](../../src/LazyTuiHub.Framework/Storage/SqliteDatabase.cs) 仍作为最小工作区基础设施；[BusinessStorage](../../src/LazyTuiHub.Framework/Storage/BusinessStorage.cs) 使用 DbConnection／DbTransaction 协调工作区原子登记。[PluginRegistration](../../src/LazyTuiHub.Sdk/PluginRegistration.cs) 通过 Daos、DaoServices 和 Retentions 收集声明，框架保存普通描述以支持提供者缺失诊断。

[RssStore](../../plugins/LazyTuiHub.Plugin.TuiHub.Rss/Backend/RssStore.cs) 保存订阅，[RssDetails](../../plugins/LazyTuiHub.Plugin.TuiHub.Rss/Backend/RssDetails.cs) 使用通用记录工具保存抓取明细。[ArticleStore](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/Backend/ArticleStore.cs) 在 articles 命名空间管理文章及全文索引，通过同一事务提交内容、索引及可选的来源关联。关闭 RSS 后仍可阅读和搜索文章。

产品测试覆盖工具对多个目标的复用、命名空间与标识符安全、到期可见性、长期保留、暂停状态跨启动保留、拥有者关闭时继续受托清理、模式／实例／后端匹配、首次缺失能力、提供者 DLL 移走／恢复，以及搜索、业务与框架登记共同回滚。既有本地／HTTP、图和插件保留场景继续通过。当前只有一个实际后端，跨后端兼容性、其他专项能力、内容快照及持久运行状态仍需真实用例验证。

## 尚需讨论的决策与顺序

| 优先级 | 决策 | 需要确定的内容及原因 |
| - | - | - |
| 下一轮细化 | 需求绑定扩展 | 当前为插件级必需声明和单工作区实例；节点级可选能力、多实例配置、远程资源重新绑定及更丰富的诊断需按实际用例细化。 |
| 随查询需求 | DAO 工具扩展 | 当前为 JSON 记录模板与 FTS5 专项接口；其他字段映射、查询／分页和索引辅助由实际需求推动。原生连接由调用方释放，语义可移植范围通过其他提供者验证。 |
| 执行输入持久化前 | TTL 与执行保留 | 当前观察明细可直接按期限回收；执行输入的持有、释放、崩溃恢复、普通关联与强保留引用仍需单独确定。后台委托永久撤销及大规模清理预算也待细化。 |
| 首个外部存储用例 | 跨事务域登记 | 当前仅承诺同一工作区源的原子登记；多库、文件或外部服务需要明确权威状态、登记顺序及未知结果的恢复方式。 |
| 随首个内容用例 | 对象、快照与位置标识 | 引用的作用域、可变实体与不可变内容的关系、关联是否保留内容、过期后的描述，以及跨实例解析。决定跨业务复用与有限重放的依据。 |
| 随部署与运维需要 | 物理布局及完整恢复 | 单库／分库、内容目录、Schema 初始化、备份快照一致性、迁移与导入导出。遵循 MVP 无历史兼容承诺，不提前建立通用迁移体系。 |
| 随运行恢复与多设备需要 | 持久现场及资源策略 | 排队消息、未齐同步组、节点状态的持久性；并发、连接复用、资源预算、保留引用释放、远程可用性与数据搬迁。不会从某个默认 TTL 或有界队列推导整体恢复和内存保证。 |
