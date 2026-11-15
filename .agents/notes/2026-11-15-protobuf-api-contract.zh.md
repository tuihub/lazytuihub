---
status: implemented
category: architecture
---
# Proto 契约与 HTTP 通信

[English](2026-11-15-protobuf-api-contract.en.md) | 中文

## 状态与范围

本 Note 记录已落实的协议与生成基线及首个 Hello World 示例：用 Proto 声明接口，直接使用 Google API Linter 检查契约，通过 ASP.NET Core JSON transcoding 提供 HTTP/JSON 接口，并让进程内调用进入同一套 C# 应用服务。implemented 表示该基线已落地；下文保留的业务可靠性要求按适用场景实施，未来的 WebSocket 通信保留独立的适配边界。

项目是同时包含客户端和服务端的单体应用。远程调用发生在公网，需要按传统 C/S 应用处理网络故障。服务端可能包含复杂业务，纯 CRUD 较少，代码生成负责契约与通信样板，业务规则和流程由 C# 实现。

当前[产品入口](../../src/LazyTuiHub/Program.cs)已经提供 Hello World 的 HTTP 服务及本地演示，[项目](../../src/LazyTuiHub/LazyTuiHub.csproj)以 net11.0 为目标。Proto、官方检查器、OpenAPI、HTTP 客户端及 Markdown 文档生成、精简标准和官方原文查阅已经贯通。带副作用业务的可靠性、身份认证、长任务与特殊类型编码属于后续接入时的实现及验收工作，不作为当前无状态基线的完成前提，也不记录为已实现能力。产品与部署目标继续参照[仓库初始化 Note](2026-11-15-repository-initialization.zh.md)。

## 契约与设计约定

Proto 是唯一手写的传输契约来源，声明请求、响应、服务方法及 `google.api.http` 映射。C# 消息类型和 OpenAPI 从该契约派生，不再独立手写维护第二份协议定义。生成器不能覆盖手写业务代码，生成产物不手工修改。

采用 Google API Design Guide 与适用的 AIP 设计要求。AIP 以 Protobuf 契约为中心，也明确规定 HTTP/JSON 映射；采用这套契约不要求公网客户端使用 gRPC。参考 [AIP-127](https://google.aip.dev/127) 的 HTTP 绑定、[AIP-136](https://google.aip.dev/136) 的自定义业务动作，以及请求幂等、长时间运行操作、并发控制和结构化错误规范。

按业务意图组织服务方法。能够准确表达为标准资源操作的接口遵循相应标准方法，发布、同步等复杂动作使用适用的自定义方法，不把业务流程拆成供客户端拼接的数据库 CRUD 接口，也不根据数据模型生成业务或持久化实现。

直接使用 [Google API Linter](https://github.com/googleapis/api-linter) 检查 Proto 及其 HTTP 注解，无须把 Google 规则翻译为 OpenAPI 规则。当前使用默认启用规则和阻断退出状态，没有禁用规则组或给示例设置豁免。Proto 的 Java 包元数据遵循 AIP-191，go_package 供 OpenAPI 生成器解析使用，本轮不生成 Java 或 Go 产品代码。未来规则例外须对应实际接口语义并说明原因。检查器仅覆盖可静态检查的 AIP 条款，不能据此认定全部 AIP 要求或运行时可靠性已经满足。

## 精简标准与官方原文

[项目 API 编写标准](../../docs/development/api-design.zh.md)维护适用要求和项目选择，[API 设计 Skill](../skills/api-design/SKILL.md)负责按任务引导查阅。标准删去无关产品背景和内部流程，但保留要求等级、条件、例外及 AIP 编号。项目有意采用的差异单独说明；没有明确本地差异时，以采用基准的原文纠正摘编错误，不把遗漏视为豁免。

[mise.toml](../../mise.toml) 的 aip_repository 和 aip_revision 是官方仓库及采用提交的唯一配置来源。`mise run setup:aip` 将官方仓库克隆到 `.agents/references/google-aip/` 并检出该提交。已有副本在来源正确且工作树干净时才切换到配置提交；初始化不会覆盖本地修改。重复执行复用副本，不自动追随上游最新提交。

原文保留官方目录和内容，供 Skill 读取相关 `aip/general/NNNN.md` 及必要引用，不把全量资料塞进每次任务上下文。副本分别由 Git 和 remark 忽略，不接入本仓双语及 Note 校验。`mise doctor project` 只读检查来源、提交和工作树；更新时显式修改采用提交、重新准备副本并审查精简标准，无需维护另一套摘录镜像、索引或同步服务。

## 生成链路与职责

| 环节 | 方案与边界 |
| - | - |
| 契约检查 | Google API Linter 读取 Proto，不读取 C# 接口或 OpenAPI 来替代契约检查。 |
| C# 消息与服务基类 | 使用 protoc、Grpc.Tools 生成消息类型和服务基类，由手写的薄服务实现承接调用。 |
| HTTP 服务端入口 | 使用 Microsoft.AspNetCore.Grpc.JsonTranscoding，根据注解自动建立路径、参数绑定和 JSON 编解码。 |
| OpenAPI | 使用 google/gnostic 的 protoc-gen-openapi 从同一份 Proto 派生，不把结果作为新的手写来源。 |
| HTTP 客户端 | 使用 NSwag 从派生的 OpenAPI 生成；当前示例复用 Proto 消息并通过 partial 实现共享应用接口。 |
| API 参考文档 | 使用 Scalar 将派生 OpenAPI 渲染为英文 Markdown 操作页及消息页，保留写在 Proto 注释中的说明和示例。 |
| 应用与领域逻辑 | 手写业务规则、权限判断、事务和流程；不采用根据实体生成完整业务层的方案。 |

[官方 JSON transcoding](https://learn.microsoft.com/en-us/aspnet/core/grpc/json-transcoding?view=aspnetcore-10.0) 支持普通 HTTP/1.1、HTTP/2 和 JSON 请求响应。它在同一个 ASP.NET Core 应用中解析请求并直接调用服务实现，不需要额外的转码网关或服务间网络跳转。这里的自动化是生成服务基类并由运行时建立 HTTP 端点，不要求生成独立的 MVC Controller 源文件。

[Grpc.Tools](https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/grpc/basics.md) 默认生成的客户端使用 gRPC，不能把它当作普通 HTTP/JSON 客户端。本例通过 [protoc-gen-openapi](https://github.com/google/gnostic/tree/main/cmd/protoc-gen-openapi) 与 NSwag 完成另一条链路。[生成辅助脚本](../scripts/api.mjs)使用 NuGet 还原的公共 Proto 定义和 mise 编译工具，NSwag 的运行时选择取自 global.json，不重复声明 SDK 版本。

OpenAPI、HTTP 客户端和 Markdown 参考文档作为派生文件入库。`mise run generate:api` 显式写回，`mise run check:generated` 在临时目录重新生成后比较，不修改被检查文件；`mise run check:proto` 执行官方规则，二者经 check:api 纳入 check。C# 消息与服务基类仍由构建生成到 obj，不入库。

本仓使用 .NET 11，旧教程中的 Microsoft.AspNetCore.Grpc.Swagger 实验包已经弃用，官方说明没有直接替代品；这不代表 JSON transcoding 被弃用。OpenAPI 派生单独评估，不以旧的 Swagger 集成包作为方案前提。参见[官方弃用说明](https://learn.microsoft.com/en-us/aspnet/core/grpc/json-transcoding-openapi?view=aspnetcore-10.0)。

## API 参考文档生成

此前 [tuihub/protos 的生成配置](https://github.com/tuihub/protos/blob/master/buf.gen.yaml)通过[自定义 protoc-gen-doc 模板](https://github.com/tuihub/protos/blob/master/docs/markdown.tmpl)生成单篇 Proto 参考文档，另外生成 OpenAPI。本仓使用 [Scalar 的 OpenAPI-to-Markdown 库](https://github.com/scalar/scalar/tree/main/packages/openapi-to-markdown)渲染既有 HTTP 契约，版本及依赖锁定沿用 Node 工具链，不自行维护完整 schema 渲染器或引入文档网站。

接口说明和示例遵循 [AIP-192](https://google.aip.dev/192)，写在英文 Proto 注释中；路径、类型、必填和只读行为从声明及注解派生。Hello World 方法说明默认值、无状态重复调用和实际请求响应示例。注释变更经过 OpenAPI 进入生成文档，不再手写第二份接口说明。

`docs/api/` 整个目录全部生成英文 Markdown，明确作为双语配对约定的例外。[生成索引](../../docs/api/index.md)链接操作页和消息页，嵌套消息通过页面链接引用；检测到渲染器截断时生成失败，不发布缺少字段的文档。其他 Markdown 检查和仓库 1 MiB 文件上限继续生效。手写的生成、运行和组件说明归入双语 [API 开发指南](../../docs/development/api-design.zh.md)，接口行为和 HTTP 调用示例写在 Proto 注释中并进入生成文档。网页渲染及部署不属于 API 工作范围。

生成任务维护完整参考目录并清理过期页面。派生检查同时核对内容与文件清单，拒绝手工改动、缺失、多余和过期文件，不写回结果。工具测试覆盖递归消息链接、手写示例保留、截断检测、目录漂移及局部语言例外。这些检查证明同步关系，不证明注释或接口行为本身正确。

## 调用与业务边界

```text
进程内客户端 -> 本地适配 ----------------------> C# 应用服务
公网 HTTP   -> 自动 HTTP 端点 -> 薄服务实现 ----> C# 应用服务
未来 WebSocket -> 独立适配 --------------------> C# 应用服务
```

进程内调用直接进入应用服务，不经过回环 HTTP、序列化或模拟的 gRPC 服务上下文。远程入口完成传输适配，再调用同一个应用服务，复杂业务不写进转码组件。

调用方所依赖的应用接口保持传输无关。Grpc.Tools 的服务基类和客户端带有 gRPC 调用类型，不会自动生成一份同时适合进程内直调和 HTTP 客户端的纯 C# 接口。本例手写 [IGreetingApi](../../src/LazyTuiHub.Api/IGreetingApi.cs)，由应用服务和生成客户端的 partial 共同实现。只有存在实际重复时才评估定制接口生成，不预先建设通用 RPC 框架。

[客户端 partial](../../src/LazyTuiHub.Client/GreetingHttpClient.cs)采用 camelCase JSON 字段名，并为 NSwag 生成的相对路径添加 `./`，避免 v1:generateGreeting 被 .NET 当作 URI scheme。实例 partial 回调的签名由 NSwag 声明，因此只在该方法局部豁免 CA1822 并说明理由，不修改生成源码或关闭产品规则。该实现验证了本例字符串消息的复用，不能据此承诺所有 Protobuf 特殊类型都能直接由同一序列化配置处理。

应用服务可以复用生成的消息类型；领域对象在真实业务需要时独立建模，不为形式上的分层机械复制所有 DTO。业务校验、授权语义和幂等执行进入共享路径，本地调用不依赖 HTTP 模型绑定才能满足业务约束。身份如何建立和传递在具体接入时确定。

统一 API 表示统一业务操作、数据和业务结果，不承诺抹去网络差异。远程适配必须区分业务拒绝、通信失败，以及写操作结果未知；调用方不能把超时或断线直接解释为服务端未执行。

## 公网可靠性

以下是已采用的设计约束，需要在相应业务或公网接入阶段落实。需要去重的写操作出现时实现幂等执行；接入自动重试时配置安全范围、错误分类和调用预算；引入长任务或并发资源修改时分别实现操作恢复与并发控制。公网接入前确定并实现调用身份及权限策略。当前无状态样例不预先建设去重存储、任务系统或账号体系，后续实现仍须满足这些约束。

- 请求标识：按 [AIP-155](https://google.aip.dev/155)，为需要去重保护的命令提供 `request_id`。同一次业务意图的重试复用标识，新的业务意图使用新标识。标识属于请求，不属于资源；在 HTTP 中按契约映射到请求体或查询参数。是否必填逐操作约定，不把所有接口一律改成缺失标识即拒绝。
- 幂等执行：在明确的调用身份或租户、操作和请求标识范围内去重，并核对业务参数，避免把不同请求误当重试。重复的成功请求返回先前成功结果或契约允许的等价结果；长时间运行命令的重试应定位同一个操作对象。去重有效范围、保存期和处理中请求的响应需要明确约定，不在本 Note 中预设时长。
- 提交一致性：同一数据库中的去重记录与业务写入需要原子提交，避免业务成功后缺失去重记录。涉及文件、外部服务或其他无法纳入该事务的副作用时，按业务设计幂等和恢复措施。单纯增加 UUID 或内存响应缓存不能代替这些保证，也不构成全局恰好执行一次的承诺。
- 重试与取消：按 [AIP-194](https://google.aip.dev/194) 的操作语义和错误分类决定重试，配置退避、抖动及总调用预算。失败不统一重试，用户取消与已耗尽的调用期限需要被尊重。将取消传播给服务端，但取消等待不承诺回滚已提交的操作。
- 长时间运行操作：按 [AIP-151](https://google.aip.dev/151) 使用 google.longrunning.Operation 及对应操作查询服务，先返回操作对象，再查询进度、结果或错误，避免必须维持一条连接直至任务完成。操作状态和执行生命周期需要支持约定范围内的断线恢复；持久化与重启恢复方式在实现时确定，不能仅靠内存 Task 冒充已满足这一要求。
- 并发与错误：按实际业务采用 [AIP-154](https://google.aip.dev/154) 的 ETag 等前置条件，并遵循 [AIP-193](https://google.aip.dev/193) 的错误模型、HTTP 表示和稳定机器可读信息。幂等解决重复业务意图，并发控制解决基于过期状态进行修改的问题，两者分别设计。

Google API Linter 能检查契约中的部分标识和注解，不能证明提交、去重或恢复行为正确。例如 [AIP-155 的现有格式规则](https://linter.aip.dev/155/request-id-format)检查 request_id 的 UUID4 注解，不检查服务端实际执行过程。这些保证需要通过相关业务实现和针对性的行为验证建立。

## WebSocket 边界

| 能力 | 官方 JSON transcoding 支持情况 |
| - | - |
| 普通请求与响应 | 支持。 |
| 服务端流式响应 | 支持，使用逐行 JSON 的 HTTP 响应流。 |
| 客户端流式请求 | 不支持。 |
| 双向流式调用 | 不支持。 |
| 自动生成 WebSocket 接口与客户端 | 不支持。 |

Proto 的 stream 声明表达消息流，不自动指定 WebSocket 传输。[AIP-127](https://google.aip.dev/127) 也将双向流排除在普通 HTTP 映射要求之外。逐行 JSON 的响应流不能当作已实现的 WebSocket 协议。

未来可以继续用 Proto 声明事件消息并生成消息类型，再单独实现 WebSocket 适配。连接恢复、订阅、确认、补发和顺序语义按实际需求设计，不属于 Google 检查器或本次转码链路已经覆盖的能力。若用于操作进度通知，重连后仍通过操作查询恢复状态；是否引入事件历史和游标由实际恢复需求决定。

本方案不选择 WebSocket 框架，不承诺同一份 Proto 自动生成 HTTP 与 WebSocket 的完整双向调用体系。未来选择适配器或生成器时，继续复用应用服务，不建立第二套业务实现。

## 样例验证与后续工作

首个 [Hello World 接口](../../docs/api/operations/GreetingService_GenerateGreeting.md)使用无状态自定义方法 GenerateGreeting，HTTP 路径为 POST /v1:generateGreeting。`mise run demo:api` 在回环临时端口启动服务，通过同一调用签名比较进程内与真实 HTTP 路径。默认接收者、Ada 与 Unicode 输入均返回相同结果。本例不需要请求去重存储或长任务对象。

1. 已完成同一份 Proto 的默认 Google 规则检查、C# 消息及服务基类构建、OpenAPI 与普通 HTTP 客户端生成。服务实现只承担传输适配，进程内直接进入同一个应用服务。
2. 后续引入业务错误时，验证结构化失败及框架默认错误体是否符合 AIP-193 的 HTTP 表示，需要时在传输适配层统一。继续验证取消传播和相应业务约束，不能仅因使用 Proto 就认定已经兼容。
3. 后续使用字段 presence、枚举、64 位整数、时间或 Any 等类型时，验证派生 OpenAPI、共享消息和客户端与服务端线上的编码一致性；不因本例字符串消息成功而推广为所有类型都已验证。
4. 带副作用命令仍需验证响应丢失后重试、并发重复请求、相同标识携带不同参数，以及长任务的重新查询和约定的重启恢复。Hello World 的成功响应不代替这些可靠性验证。

本地完整 check 通过，构建为零警告、零错误；接口说明中的运行命令及 Ada、空对象请求也已实际验证。通过临时反例确认：不符合 AIP 的自定义 HTTP 动词会失败，派生文件漂移会失败且检查不会改写文件。反例修改随后还原。官方原文准备可重复执行，Doctor 的十项检查与 Skill 格式校验通过。

本轮已执行上述无状态样例，没有接入新的产品测试框架。身份传递、操作存储和去重保存策略仍待业务闭环确定；WebSocket 留待实际引入时验证。样例在 macOS 上运行，尚未在 Windows、Linux 或公网部署环境执行。

## 实施与仓库约定

工具和任务由 mise 管理，Go、protoc 和 Go 编写的协议工具固定在 mise.toml，NSwag 使用现有 dotnet-tools.json 清单，NuGet 项目引用声明运行时及编译依赖。[API 开发指南](../../docs/development/api-design.zh.md)提供采用标准、生成与运行命令及仓库初始化入口；契约与生成检查读取真实声明，生成差异通过修改契约或生成配置解决。

生成代码沿用[既有 lint 边界](2026-11-15-csharp-lint.zh.md)：不要求人工清理生成器的代码风格，仍须通过编译。构建产物遵循现有 Git 忽略规则；入库或未被忽略的生成文件仍受 1 MiB 文件上限约束，不为生成器放宽检查或静默跳过文件。

本 Note 的 implemented 范围是协议声明、规范采用、生成与检查链路，以及 Hello World 的本地和 HTTP 调用基线。后续业务继续按上述适用条件实施并验收可靠性、身份、长任务、特殊类型和 WebSocket 能力；采用规范与通过静态检查不代替实际运行时保证。
