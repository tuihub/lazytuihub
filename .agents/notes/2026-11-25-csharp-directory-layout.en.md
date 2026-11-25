---
status: implemented
category: repository
---
# C# source directory layout

English | [中文](2026-11-25-csharp-directory-layout.zh.md)

A directory may contain up to 16 handwritten C# files directly. At adoption, five product directories exceeded 16. A limit of 24 would have caught two and missed the desktop shell, game runtime, and game desktop, each of which already combined distinct responsibilities. When the count exceeds 16, review the architecture and group files that serve the same responsibility. Keep an Avalonia view with its code-behind.

The [repository file check](../scripts/check-file-sizes.mjs) counts existing tracked and non-ignored new files, including tests. Partial classes and code-behind each count by physical `.cs` file. Each subdirectory has its own count. The repository uses the `Generated/` directory or the suffixes `.g.cs`, `.g.i.cs`, `.generated.cs` and `.designer.cs` to identify generated sources. The [1 MiB size limit](2026-11-15-csharp-lint.en.md) still applies to every file in scope, including generated files.

Run `mise run check:files` to see every directory over the limit and its count. Both local checks and main-branch CI run this rule.
