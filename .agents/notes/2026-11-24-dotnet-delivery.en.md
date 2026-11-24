---
status: implemented
category: repository
---
# .NET toolchain and product delivery

English | [中文](2026-11-24-dotnet-delivery.zh.md)

Development and CI use the .NET 10 LTS SDK managed by mise. [global.json](../../global.json) pins its exact servicing version in the final `10.0.4xx` feature band. The initial pin is SDK 10.0.404 with runtime 10.0.15. Microsoft services .NET 10 through November 14, 2028. This baseline carries forward the SDK and target framework decisions that superseded the corresponding selections in the [repository initialization](2026-11-15-repository-initialization.en.md) and [Proto API contract](2026-11-15-protobuf-api-contract.en.md) Notes. The former and current runtime families share that support end date. Their other package-management, contract and generation agreements remain effective. [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) · [SDK feature band lifecycle](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs#lifecycle)

[Directory.Build.props](../../Directory.Build.props) sets `net10.0` across the solution, plugins, product tests and Avalonia specimens. The compiler defaults to C# 14. The Uno comparison targets `net10.0-desktop`. The API generator and Docker worker tests use the same runtime family.

Every product release ships a self-contained JIT host or remote CLI built for the target OS and architecture. Each package includes its own runtime. The host loads managed business plugins into its runtime at cold startup. The remote CLI connects to a host over HTTPS. Development uses the shared mise runtime. Updating a release's runtime requires updating the SDK servicing pin and republishing the packages. The [release guide](../../docs/development/release.en.md) gives the commands and output layout.

Windows 7 uses the ordinary Windows x64 packages. Its verified scope and prerequisites belong to the [Win7 evaluation](2026-11-23-mvp-win7-cli.en.md) and are outside Microsoft's OS support matrix.
