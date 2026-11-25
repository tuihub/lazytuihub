---
status: proposed
category: architecture
---
# MVP storage, DAO access, and data retention

English | [中文](2026-11-17-mvp-storage-dao.zh.md)

## Scope and status

This Note covers data access across relational, document and key/value stores, files and objects, search and vector engines, and external services. [Framework](2026-11-17-mvp-framework.en.md) · [Node execution](2026-11-17-mvp-node-execution.en.md) · [Plugin ownership](2026-11-17-mvp-plugins.en.md)

SQLite implements all three access modes, namespaced tables and TTL records. Plugins own their schemas. The MVP specifies neither a schema design standard nor compatibility with earlier prototype schemas.

## Ownership and account scope

Plugin namespaces identify table ownership. Resources inside a table also have account scope. Calls through all three DAO access modes should obtain that scope from a trusted context. Reads, writes, queries, indexes, and data relations in shared physical storage must preserve account isolation. The [account service and access control Note](2026-11-19-mvp-account-identity.en.md) defines initial owner access and the two-account acceptance case.

Plugins may create as many tables as they need in their namespace. The framework records ownership independently of the storage backend and keeps its catalog and diagnostics available when a provider is missing.
## Three DAO access modes by encapsulation

| Mode | Interface supplied to consumers | Requirements and portability boundary |
| - | - | - |
| Consistent encapsulation across underlying implementations | Different implementations honor the same operation and behavior contract, such as records, content access, search, or TTL datasets. | Consumers depend on the common contract; only providers implementing that contract qualify. Each contract defines the extent of uniform semantics. |
| Encapsulation specific to an underlying implementation | A specialized interface preserves a backend's types, query capabilities, and behavior, such as FTS5 or PostGIS services. | Consumers declare the specialized service and necessary conditions; specialized interfaces for different backends may differ. |
| Native access | Consumers obtain a database connection, SDK client, file handle, or access session and perform native operations. | Consumers declare backend, extension, and instance requirements and own concrete operations. The framework manages the agreed binding and access lifecycle. |

The access mode, backend type and data shape can vary independently.

A plugin can mix modes. A provider advertises the modes it implements. Plugins remain fully trusted in all three modes, and the framework alone writes its metadata.

The framework shares provider discovery, configuration, access instances, requirement matching, binding lifecycle, and diagnostics. Transactions, ordering, pagination, TTL, and search semantics belong to their respective contracts rather than assuming every DAO behaves like a database. Workflow ports and cross-device execution communication remain execution-layer responsibilities. Business services may call DAO services directly or adapt them through optional workflow nodes.

Keep basic public contracts separate from SQLite native types; specialized wrappers and native entry points may expose types their backend needs. Replacing SqliteConnection with DbConnection reduces driver-type coupling but still leaves SQL, DDL, indexing, and transaction differences to concrete implementations. Mature drivers or ORMs may help implementation; no common ORM or custom general query language has been selected.

## DAO toolkit and call targets

Plugins own their tables, schemas and indexes. They pass the target to each DAO call. A portable DAO contract defines how an operation behaves across backends.

For example, RSS passes its fetch-observation table as a `DaoTable(namespace, name)`. Another plugin can use the same `IRecordDao` with its own table. The plugin calls `CreateTable` to create a key/JSON record table with nullable `expires_at`, then uses `Put`, `Read`, `Keep` and `DeleteExpired`. RSS uses a native connection for tables that require custom columns or queries.

Apply this pattern across DAO resources: document access receives collections, search receives indexes, and object/file access receives corresponding locations or object targets. The three encapsulation modes remain effective without imposing one table structure on these targets.

Provider matching covers required DAO tools, connection capabilities, and access instances. Concrete tables are operation arguments. Calling a tool alone does not require prior per-table registration, schema inference, or framework ownership of business table lifecycles. Common background retention or cross-plugin data cooperation separately receives explicit targets, policies, and necessary descriptions.

## Declarations, checking, and execution plans

`DaoRequirement` names the requirement within the plugin and declares its `Mode`, `Contract`, `Instance` and optional `Backend`. The framework matches those fields against each provider’s `DaoCapability` and reports missing or ambiguous matches. Consumers get the bound service through `DaoScope.Get`. The prototype configures one shared SQLite instance named `workspace`.

Plugins record declarations during `Configure`. Providers publish capabilities before their consumers activate, so manifest order does not affect binding. SQLite advertises FTS5 only after creating a temporary FTS5 table succeeds. Requirements apply per plugin. RSS needs native access and records. Articles needs native access and FTS5. A missing capability blocks the plugin that requires it. The framework catalog remains available.

A search requirement must identify a contract that covers the tokenization, ranking and query behavior the caller needs. A shared label such as “full-text search” cannot establish that two providers behave alike.

Match provider/connection capabilities separately from the existence and structural suitability of particular tables. The framework checks declared prerequisites; access targets determined at runtime produce access results explained at use time. Passing a table argument does not automatically make that table a plugin startup dependency.

Plugin dependencies, DAO requirements and workflow edges are separate declarations. Composition binds DAOs. The planner uses active nodes and preserves the saved graph and ownership information when an implementation is missing. Binding a DAO per node, run or remote instance remains open.

A binding establishes that the declared requirements matched at the time of the check. At execution, the caller can still lose the connection, receive a SQL error or get no confirmation of an external commit. Retries follow the protocol and node contracts. The framework keeps requirements and ownership as JSON so diagnostics can read them without loading plugin code.

## Namespaces and ownership

Use stable plugin identity for logical namespaces; plugins define tables, fields, indexes, constraints, and queries as needed within them. The framework maintains namespace ownership and necessary name mappings, without requiring per-table registration before private business access. Indexes, views, and related objects must also avoid collisions. File locations, object keys, and service resources express corresponding bindings according to their backend.

`DaoNames` separately encodes namespace and local name as UTF-8 hexadecimal, combining fixed prefixes and separators into table/index names. This preserves ordinary Unicode names and case distinctions, avoids underscore-concatenation ambiguity, and restricts generated dynamic SQL identifiers to fixed prefixes and hexadecimal characters. Values and search queries remain parameterized. This mapping serves the SQLite implementation; other backends must adapt to their constraints. SQLite schema names refer to main, temp, or attached databases. [SQLite name resolution](https://www.sqlite.org/lang_naming.html)

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

Track business identity, immutable snapshots, storage locations and runtime references separately. An article retains its ID when its title or body changes. Replaying the earlier input requires the corresponding snapshot and context. A synchronized-input key only associates messages.

Continue efficient in-process transport through read-only object references without forcing each message through persistent storage. Limited replay requires retained input, configuration, node implementation information, and state-affecting synchronized inputs and consumption order. Queue, incomplete-group, and node-state recovery remain framework/node Note topics; TTL does not establish those guarantees.

## Template tools and retention

`IRecordDao` supports permanent and expiring records. `Read` orders records by key. Plugins that need custom columns, indexes, filters or pagination can use backend-specific or native access.

The required Storage.Sqlite plugin supplies record tools, FTS5 search, a native `IConnectionSource` and retention cleanup. The framework keeps its own SQLite metadata and diagnostics available when the provider DLL is missing. Public DAO contracts have no SQLite driver dependency. In hosts that start plugin tasks, cleanup runs at startup and every `cleanupInterval` (one minute by default). Each record has its own expiry.

A plugin can call the TTL tool itself or delegate background cleanup for a specific records DAO and table through `PluginRegistration.Retentions`. The host stores the delegation by owner and target. `dao pause` and `dao resume` apply to subsequent cleanup passes and survive restarts. A pass already in progress may finish. Removing a delegation permanently and scheduling cleanup across instances remain open.

The framework executes lifecycle operations only under an explicit retention contract. Corresponding implementations declare cleanup responsibility for complex business tables, external content, specialized indexes, and multi-table relationships. Registering ownership or selecting an access mode does not by itself authorize generic deletion. The framework does not guess TTL columns or domain dependencies from table names.

### Record expiry

Intermediate details explicitly declare expiry, with null expiry preserving records indefinitely. RSS detailRetention is unset by default, so details do not automatically expire. A positive configured duration calculates new expiry from fetch completion; configuration changes do not rewrite existing rows. No common data-size, table-count, or retention-duration limit is introduced.

Records store millisecond-precision absolute expiresAt. Put explicitly writes/replaces both value and expiry; reads do not renew it. Read hides expiry less than or equal to the supplied now by default, with explicit diagnostic access to expired records. DeleteExpired removes expired rows only in the supplied table. Keep clears expiry on existing records, including expired but undeleted records; deleted records return not found. Each target currently uses one deletion statement without an exact deletion-time guarantee. Batch/time budgets await actual large-scale use. Flink's separation of renewal, visibility, and cleanup remains a reference. [Flink State TTL](https://nightlies.apache.org/flink/flink-docs-stable/docs/dev/datastream/fault-tolerance/state/#state-time-to-live-ttl)

Each completed RSS fetch records its URI, byte count and completion time. These private observations omit the response body. Expiring them leaves business objects and their relationships intact. Using TTL for execution inputs would require rules for holding queued messages and incomplete groups, propagating dependencies and releasing holds after a crash. Those rules and durable node state remain open.

Retaining compact expiry/provenance records, handling relationships, and maintaining specialized indexes require separate decisions; references do not automatically remain meaningful after payload deletion. SQLite deletion commonly leaves reusable pages, while shrinking the database file is a separate maintenance operation. [SQLite VACUUM](https://www.sqlite.org/lang_vacuum.html)

### Relationship to plugin enablement preservation

Disablement or absence is not a cleanup condition. Data without an explicit retention policy continues under the plugin Note's preservation boundary. Normal TTL is a declared data lifecycle, separate from explicit cleanup/uninstall hooks; automatic retention tasks must not invoke those maintenance hooks as a substitute.

Delegated cleanup continues while its owner is disabled or missing if the storage provider is available. When the provider is missing, the catalog reports `unavailable`. Data, delegations, pause settings and expiry times survive. Reads through that DAO resume when the provider returns, using the saved expiry times.

A plugin’s own TTL tasks stop when the plugin is unavailable. The shared cleaner visits only delegated targets and records the last attempt, deletion count and error for each. Failure at one target leaves the others eligible for cleanup.

## Transactions, backend choice, and public-network devices

Framework metadata, RSS subscriptions and articles share a SQLite workspace. `BusinessStorage` takes an explicit `IConnectionSource`. When it is the framework’s workspace source, business changes and metadata registration commit in the same transaction. Article FTS updates participate in the article transaction. Separate sources require their own coordination. SQLite WAL with `ATTACH` cannot guarantee crash atomicity across databases. [SQLite WAL](https://www.sqlite.org/wal.html)

Multiple DAO providers and bindings per dataset/function are a design direction, not automatic data portability or a shared transaction. Database, file, external-index, or service commits need decisions about authoritative state, metadata registration order, failures, and unknown outcomes. Existing same-database atomicity cannot simply extend to these combinations.

Owners maintain their schemas and implementation changes without introducing a general migration platform or historical compatibility requirement for the MVP. ADO.NET abstractions reduce type coupling and an ORM may assist implementation; even EF Core requires separate migrations for multiple providers. [ADO.NET](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace), [EF Core provider migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers)

Devices can host different data services. The primary tracks resource ownership and chooses an eligible execution site or explains the missing binding. Each instance accesses its local storage. SQLite WAL assumes processes on one host, so remote work uses a service connection. Remote DAO rebinding and data migration remain open. [Multi-node coordination](2026-11-19-mvp-coordination.en.md)

Backup/restore should consider the complete workspace: databases, durable content, configuration, and necessary keys. Consistent backups across stores, file/catalog commits, import/export, and data relocation remain undesigned. Native and specialized DAO implementations need to describe their own recovery requirements.

## Research basis and adoption

| Project | Adoption and boundary |
| - | - |
| [ComfyUI assets](https://github.com/Comfy-Org/ComfyUI/blob/master/app/assets/docs/README.md) | Its optional asset system separates logical records and disk content and retains missing-content records; adopt separate identity, location, and availability. |
| [Factorio Storage](https://lua-api.factorio.com/latest/auxiliary/storage.html) | Each mod has storage persisted by the engine with saves; reference logical ownership and centralized storage without inferring lossless mod removal. |
| [DeepSeek Harness storage](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/storage/storage/README.md) | Separate backend and domain form. Its [domain layer](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/storage/storage-domain/README.md) lacks cross-table transactions and secondary indexes, so its KV model does not replace complex business tables. [Session persistence](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/session/session-persistence/README.md) separately defines append/flush guarantees. |
| [Jellyfin backup/restore](https://jellyfin.org/docs/general/administration/backup-and-restore/) | Databases, configuration, and file metadata contribute to recovery; adopt the workspace perspective. |
| [Ray object fault tolerance](https://docs.ray.io/en/latest/ray-core/fault_tolerance/objects.html) | Object values and owner metadata differ, and owner death can invalidate references; distinguish runtime object mechanisms from persistent business identity. |
| [Flink state backends](https://nightlies.apache.org/flink/flink-docs-stable/docs/ops/state/state_backends/) and [checkpoint prerequisites](https://nightlies.apache.org/flink/flink-docs-stable/docs/dev/datastream/fault-tolerance/checkpointing/) | Separate working state and checkpoint storage, with replayable input required for recovery; define TTL, persistence, and execution recovery separately. |
| [Npgsql full-text search](https://www.npgsql.org/efcore/mapping/full-text-search.html) and [spatial mapping](https://www.npgsql.org/efcore/mapping/nts.html) | Illustrate backend-specific APIs inside a common access ecosystem, informing the second mode. |

## Implementation and verification

[DAO contracts and toolkit entry points](../../src/LazyTuiHub.Dao/) contain no SQLite driver references. The [SQLite provider plugin](../../plugins/LazyTuiHub.Plugin.TuiHub.Storage.Sqlite/) implements common records, specialized FTS5, and the native connection source. [SqliteDatabase](../../src/LazyTuiHub.Framework/Storage/SqliteDatabase.cs) remains minimal workspace infrastructure, while [BusinessStorage](../../src/LazyTuiHub.Framework/Storage/BusinessStorage.cs) coordinates workspace registration with DbConnection/DbTransaction. [PluginRegistration](../../src/LazyTuiHub.Sdk/PluginRegistration.cs) collects Daos, DaoServices, and Retentions, retaining ordinary descriptions for missing-provider diagnostics.

[RssStore](../../plugins/LazyTuiHub.Plugin.TuiHub.Rss/Backend/RssStore.cs) owns subscriptions. [RssDetails](../../plugins/LazyTuiHub.Plugin.TuiHub.Rss/Backend/RssDetails.cs) uses the common record tool for fetch observations. [ArticleStore](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/Backend/ArticleStore.cs) owns articles and their FTS index in the articles namespace. Its write transaction commits article content, search updates and the optional origin relationship together. Articles can be read and searched while RSS is disabled.

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
