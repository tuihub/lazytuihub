---
status: proposed
category: architecture
---
# MVP storage, DAO access, and data retention

English | [中文](2026-11-17-mvp-storage-dao.zh.md)

## Scope and status

This Note consolidates storage discussions and official-source research through 2026-11-17, alongside the [framework Note](2026-11-17-mvp-framework.en.md), [node execution Note](2026-11-17-mvp-node-execution.en.md), and [plugin Note](2026-11-17-mvp-plugins.en.md). DAO broadly denotes business data access, including relational databases, document/KV stores, files/object stores, search/vector engines, and external data services.

Under the [MVP maintenance agreement](../../AGENTS.md), this Note remains proposed and freely editable until merge into main. It distinguishes established direction, this local implementation, and open details. Three-mode requirement matching, the SQLite toolkit provider, namespaces, and TTL details now have a runnable implementation described below. Prototype interfaces, graphs, and SQLite schemas retain no compatibility commitment, and this discussion introduces no database schema authoring standard.

## Established design direction

Plugin namespaces identify table ownership. Resources inside a table also have account scope. Calls through all three DAO access modes should obtain that scope from a trusted context. Reads, writes, queries, indexes, and data relations in shared physical storage must preserve account isolation. The [account service and access control Note](2026-11-19-mvp-account-identity.en.md) defines initial owner access and the two-account acceptance case.

- Classify all DAO access into three modes: consistent wrappers across underlying implementations, wrappers specific to an underlying implementation, and direct native access.
- Supply reusable DAO logic as a loosely coupled toolkit. Plugins pass tables or other access targets when calling it and own business table creation, structure, and use. Bind access capabilities separately from concrete operation targets.
- Plugins declare actual requirements, providers declare contracts and capabilities they actually supply, and the framework checks satisfaction to produce bindings or missing-requirement explanations.
- Plugins may create tables as needed within their logical namespace, without an additional framework table-count limit; record ownership separately from the concrete backend.
- Common data shapes may use managed templates. Retention of intermediate details and preservation of long-lived data must be explicit.
- Built-in storage plugins may implement templates and cleanup. The exact infrastructure/plugin split remains open; minimal framework catalog access and diagnostics must remain independent of optional implementations.

## Three DAO access modes by encapsulation

| Mode | Interface supplied to consumers | Requirements and portability boundary |
| - | - | - |
| Consistent encapsulation across underlying implementations | Different implementations honor the same operation and behavior contract, such as records, content access, search, or TTL datasets. | Consumers depend on the common contract; only providers implementing that contract qualify. Each contract defines the extent of uniform semantics. |
| Encapsulation specific to an underlying implementation | A specialized interface preserves a backend's types, query capabilities, and behavior, such as FTS5 or PostGIS services. | Consumers declare the specialized service and necessary conditions; specialized interfaces for different backends may differ. |
| Native access | Consumers obtain a database connection, SDK client, file handle, or access session and perform native operations. | Consumers declare backend, extension, and instance requirements and own concrete operations. The framework manages the agreed binding and access lifecycle. |

The second mode supplies an interface tailored to its backend. Database type, data shape, and encapsulation mode are separate dimensions. Native access spans the underlying entry points of all DAO resource types.

Select modes per access requirement; a plugin may mix them. Providers publish modes according to actual capabilities rather than implementing every wrapper merely to complete the classification. These modes do not represent trust levels or permission sandboxes; full trust and exclusive framework-metadata mutation remain effective.

The framework shares provider discovery, configuration, access instances, requirement matching, binding lifecycle, and diagnostics. Transactions, ordering, pagination, TTL, and search semantics belong to their respective contracts rather than assuming every DAO behaves like a database. Workflow ports and cross-device execution communication remain execution-layer responsibilities. Business services may call DAO services directly or adapt them through optional workflow nodes.

Keep basic public contracts separate from SQLite native types; specialized wrappers and native entry points may expose types their backend needs. Replacing SqliteConnection with DbConnection reduces driver-type coupling but still leaves SQL, DDL, indexing, and transaction differences to concrete implementations. Mature drivers or ORMs may help implementation; no common ORM or custom general query language has been selected.

## DAO toolkit and call targets

The framework supplies reusable DAO operations, with plugins passing target tables at use time. Plugins create tables, maintain their structure and indexes, and choose which table to access; tools execute operations under the selected contract. The first mode standardizes operation and behavior semantics while plugins still supply concrete tables at the call site.

RSS passes its fetch-detail table to the same TTL tool another business can use with its own detail table. This increment uses DaoTable(namespace, name) targets and IRecordDao operations CreateTable, Put, Read, Keep, and DeleteExpired. Its template has key, JSON value, and nullable expires_at; businesses invoke the creation helper themselves. Arbitrary column mappings and general queries remain unimplemented, while complex RSS tables continue through native relational access.

Apply this pattern across DAO resources: document access receives collections, search receives indexes, and object/file access receives corresponding locations or object targets. The three encapsulation modes remain effective without imposing one table structure on these targets.

Provider matching covers required DAO tools, connection capabilities, and access instances. Concrete tables are operation arguments. Calling a tool alone does not require prior per-table registration, schema inference, or framework ownership of business table lifecycles. Common background retention or cross-plugin data cooperation separately receives explicit targets, policies, and necessary descriptions.

## Declarations, checking, and execution plans

DaoRequirement now declares a plugin-local name, Mode, Contract, Instance, and optional Backend; DaoCapability publishes the supplied mode, contract, instance, and backend. The default instance name is workspace, with only the shared SQLite workspace currently composed. Matching uses these fields, explains missing or ambiguous matches, and supplies services through DaoScope.Get.

Declarations persist independently during Configure. Composition lets available providers publish first, then activates consumers, avoiding manifest-order dependence. SQLite actually attempts a temporary FTS5 table and publishes search only when that succeeds. Requirements are currently mandatory at plugin scope: RSS requires native access and record retention. Articles requires native access and FTS5; missing capabilities block their respective consumers. Multiple-instance configuration, node-level optional capabilities, and richer probing results remain open.

For example, RSS may request a common TTL contract for fetch details, an FTS5 wrapper for retrieval, and a native connection for another operation. Whether declaring “full-text search” suffices depends on whether required tokenization, ranking, and query behavior are covered by an explicit contract; the framework does not infer semantic equivalence from names.

Match provider/connection capabilities separately from the existence and structural suitability of particular tables. The framework checks declared prerequisites; access targets determined at runtime produce access results explained at use time. Passing a table argument does not automatically make that table a plugin startup dependency.

Keep plugin hard service dependencies, individual DAO access requirements, and cross-business workflow edges separate. This increment checks DAO requirements during composition, with workflows planning against active nodes and retaining missing-node provenance. Per-node, per-run, and remote-instance DAO rebinding remain unimplemented; later work must preserve the distinction between creation-time feasibility and execution-time conditions.

A successful match describes declared prerequisites at checking time. Connection loss, SQL errors, and unknown external commit outcomes remain runtime results. Retries and business side effects follow existing protocol and node contracts. Retained requirements and ownership use ordinary JSON descriptions, readable without constructing CLR objects from missing plugins. The SQLite probe currently establishes FTS5 availability; richer provider health and failure details remain open.

## Namespaces and ownership

Use stable plugin identity for logical namespaces; plugins define tables, fields, indexes, constraints, and queries as needed within them. The framework maintains namespace ownership and necessary name mappings, without requiring per-table registration before private business access. Indexes, views, and related objects must also avoid collisions. File locations, object keys, and service resources express corresponding bindings according to their backend.

DaoNames now separately encodes namespace and local name as UTF-8 hexadecimal, combining fixed prefixes and separators into table/index names. This preserves ordinary Unicode names and case distinctions, avoids underscore-concatenation ambiguity, and restricts generated dynamic SQL identifiers to fixed prefixes and hexadecimal characters. Values and search queries remain parameterized. This mapping serves the SQLite implementation; other backends must adapt to their constraints. SQLite schema names refer to main, temp, or attached databases. [SQLite name resolution](https://www.sqlite.org/lang_naming.html)

The data owner, DAO provider, and template/cleanup provider may differ. RSS details still belong to RSS; selecting a common managed template does not transfer business ownership to the storage plugin.

The framework exclusively mutates its metadata. Business and storage plugins submit declarations, business results, and lifecycle-change intent through agreed interfaces for framework registration. Native access still follows the ownership contract. Trusted code contracts maintain ownership in the current fully trusted process; database permission isolation and SQL interception are outside the current proposal.

## Persistence, retention, and data references

Define whether data persists, how long it remains, and where it resides separately. Recent intermediate details may persist, and long-lived business results may reside in SQLite or another store.

| Data role | Storage responsibility and retention direction |
| - | - |
| Framework metadata | The framework maintains plugin/data ownership, relationships, workflows, and necessary catalogs, readable despite missing implementations. |
| Business data | Business plugins own fields, constraints, queries, and domain retention needs, without flattening complex domains into a universal KV document model. |
| Content and snapshots | Files, attachments, and original fetches may use separate content storage; identity, ownership, and available locations must relate. |
| Runtime state and records | Plans, results, input snapshots, queued messages, synchronized groups, and node state have distinct persistence requirements. |
| Reclaimable data | Explicit retention authorizes reclamation; labeling data as intermediate or cached does not establish that it may be discarded. |

Model logical business objects, particular immutable content snapshots, storage locations, and runtime payload references separately. An article ID may stay unchanged after its content changes; replay needs the original input snapshot and necessary context. The articles plugin updates an article's title and body, so retaining its ID alone cannot reproduce historical input. Synchronized-input messagekeys do not implicitly become these identities either.

Continue efficient in-process transport through read-only object references without forcing each message through persistent storage. Limited replay requires retained input, configuration, node implementation information, and state-affecting synchronized inputs and consumption order. Queue, incomplete-group, and node-state recovery remain framework/node Note topics; TTL does not establish those guarantees.

## Template tools and retention

Templates supply table-creation, access, and applicable lifecycle operations as toolkit facilities, with plugins selecting templates and passing target tables. Persistent records and TTL details share IRecordDao: null expiry preserves data indefinitely, while explicit expiry enables retention. Read orders records by key; general filters, pagination, custom columns, and indexes are not provided. Businesses may retain other structures through specialized/native access.

The new storage.sqlite core plugin supplies record templates, FTS5 encapsulation, a native IConnectionSource, and cleanup. Its distribution manifest prevents manual disablement; missing code still allows minimal framework startup. SQLite metadata infrastructure remains in the framework, independently retaining plugin and retention catalogs. Public DAO contracts contain no SQLite driver types. Cleanup runs as a plugin task, once at server startup and then every cleanupInterval, defaulting to one minute. This is a scheduling interval, not data retention. CLI mode does not start background tasks.

Business plugins may call TTL tools directly; background cleanup requires PluginRegistration.Retentions to delegate a records DAO and target table explicitly. Each row's expiresAt determines retention. Delegations persist by owner/target, preserving saved enablement on repeated composition. dao pause/resume controls subsequent cleanup passes; operations already started may finish. This increment retains paused delegations; permanent revocation and cross-instance scheduling remain open. Using a helper/template alone does not register cleanup.

The framework executes lifecycle operations only under an explicit retention contract. Corresponding implementations declare cleanup responsibility for complex business tables, external content, specialized indexes, and multi-table relationships. Registering ownership or selecting an access mode does not by itself authorize generic deletion. The framework does not guess TTL columns or domain dependencies from table names.

### Candidate TTL semantics

Intermediate details explicitly declare expiry, with null expiry preserving records indefinitely. RSS detailRetention is unset by default, so details do not automatically expire. A positive configured duration calculates new expiry from fetch completion; configuration changes do not rewrite existing rows. No common data-size, table-count, or retention-duration limit is introduced.

Records store millisecond-precision absolute expiresAt. Put explicitly writes/replaces both value and expiry; reads do not renew it. Read hides expiry less than or equal to the supplied now by default, with explicit diagnostic access to expired records. DeleteExpired removes expired rows only in the supplied table. Keep clears expiry on existing records, including expired but undeleted records; deleted records return not found. Each target currently uses one deletion statement without an exact deletion-time guarantee. Batch/time budgets await actual large-scale use. Flink's separation of renewal, visibility, and cleanup remains a reference. [Flink State TTL](https://nightlies.apache.org/flink/flink-docs-stable/docs/dev/datastream/fault-tolerance/state/#state-time-to-live-ttl)

TTL currently applies to completed-fetch observations containing URI, byte count, and completion time. They store no original body and serve neither replay input nor checkpoints. Records are private observations rather than framework data objects, so cleanup removes no business objects or relationships. Keep supports long-term retention; execution holds, dependency propagation, and release after crashes remain unimplemented. These tools are not reliable queues or node-state backends. Durable execution inputs require holding rules before reclamation and must not silently lose queued messages or incomplete groups.

Retaining compact expiry/provenance records, handling relationships, and maintaining specialized indexes require separate decisions; references do not automatically remain meaningful after payload deletion. SQLite deletion commonly leaves reusable pages, while shrinking the database file is a separate maintenance operation. [SQLite VACUUM](https://www.sqlite.org/lang_vacuum.html)

### Relationship to plugin enablement preservation

Disablement or absence is not a cleanup condition. Data without an explicit retention policy continues under the plugin Note's preservation boundary. Normal TTL is a declared data lifecycle, separate from explicit cleanup/uninstall hooks; automatic retention tasks must not invoke those maintenance hooks as a substitute.

This increment continues persisted delegations while the owner is disabled or missing and the record/cleanup provider remains available. A missing provider marks retention unavailable while keeping data, delegation, and expiry readable through minimal catalog diagnostics. Restoration reuses expiry, and recomposition preserves paused intent. Expiry visibility follows the record tool contract; missing providers make the DAO unavailable without renewing records or reporting fictitious successful reads.

Plugin-owned tasks calling TTL tools still stop with that plugin, and undelegated targets never enter common cleanup automatically. The retention catalog records the last attempt time, deleted count, and error; one target's failure does not block others. These operations neither invoke maintenance hooks nor verify their business cleanup semantics. Execution holds and cross-instance recovery remain open.

## Transactions, backend choice, and public-network devices

This increment retains one SQLite workspace for framework, RSS, and article tables. BusinessStorage accepts an explicit IConnectionSource and coordinates atomic business/metadata writes only for the same framework workspace source; other sources require separate coordination. Article FTS indexing participates in the article-write transaction. SQLite WAL with ATTACH does not provide crash atomicity for a set of databases. [SQLite WAL](https://www.sqlite.org/wal.html)

Multiple DAO providers and bindings per dataset/function are a design direction, not automatic data portability or a shared transaction. Database, file, external-index, or service commits need decisions about authoritative state, metadata registration order, failures, and unknown outcomes. Existing same-database atomicity cannot simply extend to these combinations.

Owners maintain their schemas and implementation changes without introducing a general migration platform or historical compatibility requirement for the MVP. ADO.NET abstractions reduce type coupling and an ORM may assist implementation; even EF Core requires separate migrations for multiple providers. [ADO.NET](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace), [EF Core provider migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers)

Future devices cooperate over the public internet, requiring separate data identity, available locations, and transfer paths. The current preference is local instance storage with DAO resource bindings and necessary transfer in execution plans; SQLite WAL cannot provide cross-device cooperation by sharing a database file. General resource descriptions, instance selection, reconnect behavior, credentials, and data movement remain resource-model and concrete-operation topics.

Backup/restore should consider the complete workspace: databases, durable content, configuration, and necessary keys. Consistent backups across stores, file/catalog commits, import/export, and data relocation remain undesigned. Native and specialized DAO implementations need to describe their own recovery requirements.

## Research basis and adoption

These previously consulted official documents/source provide references, not automatic dependencies or project behavior promises. The three DAO modes follow the current discussion.

| Project | Adoption and boundary |
| - | - |
| [ComfyUI assets](https://github.com/Comfy-Org/ComfyUI/blob/master/app/assets/docs/README.md) | Its optional asset system separates logical records and disk content and retains missing-content records; adopt separate identity, location, and availability. |
| [Factorio Storage](https://lua-api.factorio.com/latest/auxiliary/storage.html) | Each mod has storage persisted by the engine with saves; reference logical ownership and centralized storage without inferring lossless mod removal. |
| [DeepSeek Harness storage](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/storage/storage/README.md) | Separate backend and domain form. Its [domain layer](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/storage/storage-domain/README.md) lacks cross-table transactions and secondary indexes, so its KV model does not replace complex business tables. [Session persistence](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/session/session-persistence/README.md) separately defines append/flush guarantees. |
| [Jellyfin backup/restore](https://jellyfin.org/docs/general/administration/backup-and-restore/) | Databases, configuration, and file metadata contribute to recovery; adopt the workspace perspective. |
| [Ray object fault tolerance](https://docs.ray.io/en/latest/ray-core/fault_tolerance/objects.html) | Object values and owner metadata differ, and owner death can invalidate references; distinguish runtime object mechanisms from persistent business identity. |
| [Flink state backends](https://nightlies.apache.org/flink/flink-docs-stable/docs/ops/state/state_backends/) and [checkpoint prerequisites](https://nightlies.apache.org/flink/flink-docs-stable/docs/dev/datastream/fault-tolerance/checkpointing/) | Separate working state and checkpoint storage, with replayable input required for recovery; define TTL, persistence, and execution recovery separately. |
| [Npgsql full-text search](https://www.npgsql.org/efcore/mapping/full-text-search.html) and [spatial mapping](https://www.npgsql.org/efcore/mapping/nts.html) | Illustrate backend-specific APIs inside a common access ecosystem, informing the second mode. |

## Current implementation and next validation

[DAO contracts and toolkit entry points](../../src/LazyTuiHub.Dao/) contain no SQLite driver references. The [SQLite provider plugin](../../src/LazyTuiHub.Storage.Sqlite/) implements common records, specialized FTS5, and the native connection source. [SqliteDatabase](../../src/LazyTuiHub.Framework/SqliteDatabase.cs) remains minimal workspace infrastructure, while [BusinessStorage](../../src/LazyTuiHub.Framework/BusinessStorage.cs) coordinates workspace registration with DbConnection/DbTransaction. [PluginRegistration](../../src/LazyTuiHub.Plugins/PluginRegistration.cs) collects Daos, DaoServices, and Retentions, retaining ordinary descriptions for missing-provider diagnostics.

[RssStore](../../src/LazyTuiHub.Rss/RssStore.cs) owns subscriptions. [RssDetails](../../src/LazyTuiHub.Rss/RssDetails.cs) uses the common record tool for fetch observations. [ArticleStore](../../src/LazyTuiHub.Articles/ArticleStore.cs) owns articles and their FTS index in the articles namespace. Its write transaction commits article content, search updates and the optional origin relationship together. Articles can be read and searched while RSS is disabled. Use a fresh workspace for this schema and the revised default graph.

Product tests cover reuse across targets, namespace/identifier safety, expiry visibility, long-term retention, pause persistence, delegated cleanup with a disabled owner, mode/instance/backend matching, first-start missing capabilities, provider DLL removal/restoration, and rollback shared by search, business data, and framework registration. Existing local/HTTP, graph, and plugin-preservation scenarios remain. Only one backend is implemented; cross-backend portability, additional specialized services, content snapshots, and durable runtime state still require real use cases.

## Remaining decisions and discussion order

| Priority | Decision | Questions and consequence |
| - | - | - |
| Next refinement | Requirement binding extensions | Current mandatory plugin-level declarations bind one workspace. Refine optional per-node capabilities, multiple instances, remote rebinding, and richer diagnostics through actual use cases. |
| Query demand | DAO toolkit extensions | Current tools offer JSON records and specialized FTS5; field mappings, queries/pagination, and index helpers follow real requirements. Callers dispose native connections, and portability needs other providers. |
| Before durable execution inputs | TTL and execution retention | Completed observations can expire directly. Input holds, release, crash recovery, and ordinary versus retention references remain open, alongside permanent delegation revocation and large-scale cleanup budgets. |
| First external-store use case | Registration across transaction domains | Only the same workspace source provides atomic registration. Multiple databases, files, or external services need explicit authoritative state, ordering, and unknown-outcome recovery. |
| First content use case | Object, snapshot, and location identity | Scope references, separate mutable entities and immutable content, define retention effects of relations, expiry descriptions, and cross-instance resolution. Supports reuse and limited replay. |
| Deployment/operational need | Physical layout and complete recovery | Choose shared/separate databases, content directories, schema initialization, consistent backups, migration, and import/export. Retain the MVP's lack of historical compatibility commitments rather than pre-building migration infrastructure. |
| Runtime recovery/multiple devices | Durable runtime state and resource policy | Set queue/group/node-state durability, concurrency, connection reuse, resource budgets, hold release, remote availability, and data movement. Default TTL or bounded queues establish neither recovery nor overall memory guarantees. |

This increment implements a minimal design through the three access modes used by RSS and articles and a detail write/expiry sequence. Continue refining the remaining boundaries through actual use cases and explicit, replaceable simple behavior rather than requiring every storage question to be finalized first.
