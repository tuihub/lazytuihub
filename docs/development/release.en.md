# Publish LazyTuiHub

English | [中文](release.zh.md)

Use `mise run run:desktop` or `mise run run:cli` for development. The following task builds both distributable packages for the current machine.

```sh
mise -E production run release
```

Pass a .NET runtime identifier to target another OS or architecture, for example `mise -E production run release win-x64`, `mise -E production run release linux-arm64` or `mise -E production run release osx-arm64`. Distribute the complete package from `.local/release/<rid>/`. Each package includes its own JIT runtime. Users can run it without installing mise, the .NET SDK or a shared .NET runtime.

| Package | Entry point |
| --- | --- |
| Windows and Linux host | Run the executable in `host/` with `--desktop` for the desktop or `--cli` for interactive commands. Keep the adjacent `plugins/` directory. |
| macOS host | Open `LazyTuiHub.app` for the desktop. Run `LazyTuiHub.app/Contents/MacOS/LazyTuiHub --cli` for local commands. The release task assembles the bundle locally. Developer ID signing and notarization are separate distribution steps. |
| Remote CLI | Run the executable in `cli/` with `--endpoint https://SERVER`. Keep its adjacent plugins/ directory. The CLI loads the Client and ClientCli entry points. The remote host runs the backend. |

A runtime security update reaches users when the maintainer republishes the packages with the updated SDK. See the [delivery decision](../../.agents/notes/2026-11-24-dotnet-delivery.en.md) and the [Windows 7 prerequisites](../../.agents/notes/2026-11-23-mvp-win7-cli.en.md).
