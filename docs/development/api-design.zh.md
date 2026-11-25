# API 开发指南

[English](api-design.en.md) | 中文

## 依据与查阅

本指南说明 API 开发流程，并摘编本项目采用的 Google AIP 要求。完整背景、条件、例外及关联要求以采用基准的官方原文为依据。摘要未提及的要求不自动成为豁免；项目需要不同约定时，明确记下适用位置和理由。

[mise.toml](../../mise.toml) 是官方仓库地址和采用提交的唯一配置来源。初始化时运行 `mise run setup:aip`，将原文克隆到被 Git 忽略的 `.agents/references/google-aip/`。通用条目在 `aip/general/NNNN.md`，例如 AIP-155 对应 `aip/general/0155.md`。`mise doctor project` 只读核对该副本的来源、提交和工作树状态。

日常先读本标准，再按任务查阅相关已批准 AIP 的完整内容和必要引用。Google 内部治理流程、特定 Cloud 或 Workspace 产品要求及无关的 SDK 发布背景不自动成为本项目约束。更新基准时显式修改 mise 中的提交并重新准备副本，核对受影响条目后同步调整本标准；普通构建和检查不自动更新规范。

## 契约与业务边界

用 Proto 维护唯一的传输契约。C# 消息、OpenAPI 和 HTTP 客户端从契约生成，不独立修改派生产物。传输无关的 C# 应用接口用于本地与远程适配；它描述业务调用边界，不另行维护 HTTP 路径或 JSON schema。

业务规则、权限、事务和执行流程写在应用服务中。进程内调用直接进入该服务，远程入口只适配传输。按实际需要区分领域对象，不机械复制全部 DTO。

当前实现以 [IGreetingApi](../../src/LazyTuiHub.Api/IGreetingApi.cs) 为共享调用边界。[GreetingApplication](../../plugins/LazyTuiHub.Plugin.TuiHub.Core/Backend/GreetingApplication.cs) 直接实现该接口，[传输服务](../../plugins/LazyTuiHub.Plugin.TuiHub.Core/Backend/GreetingService.cs)将转码请求及取消信号交给它。生成 HTTP 客户端的[手写 partial](../../src/LazyTuiHub.Client/GreetingHttpClient.cs) 实现同一接口，配置 JSON 字段命名及相对路径。其中的 `./` 路径前缀防止自定义方法的冒号被解析为 URI scheme。

订阅使用 [IRssApi](../../plugins/LazyTuiHub.Plugin.TuiHub.Rss/Api/IRssApi.cs)，各类来源的文章使用 [IArticleApi](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/Api/IArticleApi.cs)。应用服务执行共享管线，[客户端 partial](../../plugins/LazyTuiHub.Plugin.TuiHub.Rss/Frontend/RssHttpClient.cs) 将资源名称与查询参数适配到生成方法。Proto 消息使用 Protobuf JSON 序列化，正确处理时间戳和 repeated 字段；生成器按配置的服务分别输出客户端。[MVP 指南](streaming-mvp.zh.md)说明模块组合及当前使用 ASP.NET Core 原生 HTTP 错误表示的明确例外，该例外不改变其他 AIP 要求。

业务 Proto 随插件放在 Api/Protos 目录中。Frontend/http-client.json 指定服务、HTTP 客户端类名，以及与该插件手写 partial 类一致的命名空间。消息和服务桩的 C# 命名空间由 Proto 中的 csharp_namespace 指定。生成任务汇总业务和宿主协议，生成统一的 OpenAPI、API 参考文档，并将各业务客户端写入插件自己的 Frontend/Generated 目录。

## 设计要求

| 主题 | 编写要求与适用条件 | 原文 |
| - | - | - |
| 资源 | 用资源及关系表达可命名的业务对象；API 不照搬数据库表。名称和资源引用遵循统一模式。 | [121](https://google.aip.dev/121)、[122](https://google.aip.dev/122)、[123](https://google.aip.dev/123) |
| 方法 | 标准方法适用于其语义确实吻合的操作；复杂业务采用自定义动作，保持一个完整业务意图。每个方法使用相应的请求和响应消息。 | [130](https://google.aip.dev/130)、[136](https://google.aip.dev/136) |
| HTTP | 在 google.api.http 中声明方法、路径和请求绑定。读取、修改和无状态计算按对应方法要求选择 HTTP 动词。JSON 遵循 Protobuf 的字段名和类型表示，不自行混用普通 DTO 编码规则。 | [127](https://google.aip.dev/127)、[136](https://google.aip.dev/136) |
| 字段 | 使用一致的名称和准确注释，明确 REQUIRED、OPTIONAL、OUTPUT_ONLY 等行为。需要区分缺省与显式值时，采用合适的 presence 表示。 | [140](https://google.aip.dev/140)、[149](https://google.aip.dev/149)、[203](https://google.aip.dev/203) |
| 文档 | 用英文 Proto 注释说明服务、方法、消息、字段、枚举及枚举值。按实际情况解释默认值、副作用、幂等性、错误及示例。使用受支持的 CommonMark，不在注释中使用标题、表格或原始 HTML。 | [192](https://google.aip.dev/192) |
| 幂等与重试 | 为需要去重的命令设计 request_id，重试复用标识。是否必填和去重范围逐操作约定。只对语义安全且错误可重试的请求自动重试，并尊重取消及调用期限。 | [155](https://google.aip.dev/155)、[194](https://google.aip.dev/194) |
| 并发 | 对基于已有资源状态的修改，按业务需要采用 ETag 等前置条件。并发控制与请求去重分别设计。 | [154](https://google.aip.dev/154) |
| 长任务 | 持续执行的操作采用 google.longrunning.Operation 和相应查询能力；C# 方法返回 Task 不等于业务长任务。状态和结果应能在约定范围内恢复查询。 | [151](https://google.aip.dev/151) |
| 错误 | 遵循 google.rpc.Status、标准错误码与 ErrorInfo，并核对其 HTTP 表示。客户端依赖稳定的机器可读信息，不解析错误文案。 | [193](https://google.aip.dev/193) |
| 集合查询 | 集合确实需要分页或过滤时采用相应模式，不因一个方法返回列表就添加没有业务依据的限制。 | [158](https://google.aip.dev/158)、[160](https://google.aip.dev/160) |
| 命名与演进 | 遵循 Proto 文件、包、消息和方法命名。变更字段时保留编号语义，按已承诺的兼容范围审查变更；不因实现或文案变化额外引入 API 版本。 | [180](https://google.aip.dev/180)、[190](https://google.aip.dev/190)、[191](https://google.aip.dev/191) |

要求等级、边界和例外保留原文含义，不能把 SHOULD 或 MAY 摘编成无条件 MUST。表中导航不替代标准方法等关联条目的完整规定。

## API 参考文档

接口说明和示例以 Proto 注释为来源；路径、类型、必填和只读等结构事实以声明及注解为来源。说明应与实际实现一致，不在 Markdown 中另行维护接口定义，也不修改派生的 OpenAPI。

`mise -E development run generate:api` 在生成客户端时一并执行 Proto → OpenAPI → Scalar Markdown。[生成的参考索引](../api/index.md)链接操作页与消息页。请求响应示例和 HTTP 调用示例写在方法注释中，使用带标签的围栏代码块；字段注释解释相关默认值与行为。注释遵循 AIP-192，完整要求和例外仍以采用的原文为准。

`docs/api/` 全部由工具生成英文 Markdown，明确豁免双语命名与配对，其他 Markdown 规则和 1 MiB 文件上限继续生效。生成清理过期页面，差异检查拒绝手工改动、缺失、多余或过期文件，不写回结果。消息通过页面链接引用，避免递归展开；检测到上游截断时生成失败，不发布缺少字段的文档。手写开发说明放在 `docs/development/`，网页渲染及部署不属于 API 工作范围。

Scalar 自动合成的日期示例使用固定占位时间，保证重复生成得到相同结果。处理仅作用于渲染器的输入副本，保留声明中的示例、默认值及原始 OpenAPI 契约。

## 初始化与生成

按[仓库初始化命令](../../AGENTS.md#development-commands)准备工具和依赖。修改 Proto 后生成并检查派生产物：

```sh
mise -E development run generate:api
mise run check:api
```

Grpc.Tools 在 .NET 构建时生成 C# 消息与服务基类。显式生成任务分别使用 protoc-gen-openapi、NSwag 和 Scalar 生成 OpenAPI、HTTP 客户端及 Markdown。每次契约变更后检查生成差异。

`mise run check:proto` 使用 Google API Linter 的默认启用规则，并通过退出状态阻断发现的问题。当前没有关闭规则组或为 Hello World 设置豁免。必要的未来例外须按具体规则和接口记录依据。

`mise run check:generated` 在临时目录重新生成并比较结果，不写回被检查文件。`mise run check:api` 聚合两项检查，已经纳入 `mise run check`。

自动检查不能证明资源建模、授权、去重提交或恢复正确。对涉及这些能力的改动验证其真实行为。入库生成文件继续受仓库 1 MiB 上限约束；外部原文副本是忽略的开发参考依赖，不作为本仓 Markdown 或双语文档维护。

## 运行本地与 HTTP 调用

运行集成示例，比较应用服务直调与生成客户端的 HTTP 调用：

```sh
mise -E development run demo:api
```

演示在回环地址的临时端口启动服务，检查两条路径返回相同结果后停止。需要手动调用时，运行 HTTP 服务：

```sh
mise run run -- --urls http://127.0.0.1:5080
```

接口行为和请求示例查阅[生成的操作参考文档](../api/operations/GreetingService_GenerateGreeting.md)。使用其中的 HTTP 调用示例时，将 `BASE_URL` 设为正在运行的服务地址。

## 集成边界

当前集成已验证字符串消息。引入字段 presence、枚举、64 位整数、时间、Any 等 Protobuf 类型时，验证派生 OpenAPI 与客户端、服务端线上的编码一致性。

引入业务错误时，验证并适配框架错误与 AIP-193 HTTP 表示的一致性。公网身份认证、HTTPS 部署、带副作用命令、长任务持久化及 WebSocket 接入按[协议 Note](../../.agents/notes/2026-11-15-protobuf-api-contract.zh.md)处理，示例本身不建立这些能力。
