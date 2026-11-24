---
status: archived
category: repository
superseded_by:
    - 2026-11-24-dotnet-delivery.en.md
---
# Repository .NET LTS baseline

English | [中文](2026-11-24-dotnet-lts.zh.md)

The project adopts the final SDK feature band of .NET 10 LTS, `10.0.4xx`, for development, CI and publishing. [global.json](../../global.json) pins the exact servicing release that mise installs. The initial pin is 10.0.404, which includes runtime 10.0.15. Microsoft services this LTS release through November 14, 2028. Keep the patch level current within the band and republish self-contained and Native AOT deployments to deliver runtime fixes. [Microsoft release policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) · [SDK feature band lifecycle](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs#lifecycle)

This decision replaces the .NET 11 SDK and target framework selections in the [repository initialization](2026-11-15-repository-initialization.en.md) and [API protocol](2026-11-15-protobuf-api-contract.en.md) Notes. Both releases have the same support end date. The LTS baseline lets ordinary and Win7 deployments share the same runtime family and preserves cold-start managed plugin loading. The existing package-management, API contract and generation agreements continue to apply.

[Directory.Build.props](../../Directory.Build.props) provides the shared `net10.0` target. The compiler selects C# 14 from that target. The solution, plugins, product tests and Avalonia specimens inherit it. Uno uses the desktop-specific `net10.0-desktop` target for its prototype. The Docker worker image and the API generator use the same release family. The [Win7 evaluation](2026-11-23-mvp-win7-cli.en.md) covers the self-contained JIT host with its managed plugins and the standalone Native AOT CLI. Windows 7 compatibility remains a project-tested capability outside Microsoft's supported operating systems.
