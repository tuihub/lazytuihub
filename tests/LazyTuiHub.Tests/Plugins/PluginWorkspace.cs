using System.Diagnostics;
using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Xunit;

namespace LazyTuiHub.Tests;

internal sealed class PluginWorkspace : IDisposable
{
    public string DirectoryPath { get; } =
        Path.Combine(Path.GetTempPath(), $"lazytuihub-plugin-test-{Guid.NewGuid():N}");
    public string Plugins => Path.Combine(DirectoryPath, "plugins");
    public string Database => Path.Combine(DirectoryPath, "workspace.db");

    public PluginWorkspace()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "plugins");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(Plugins, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public PluginSpec[] ReadPlugins() => PluginManifest.Read(Plugins, Assert.Fail).ToArray();

    public Task WriteManifestAsync(PluginSpec spec)
    {
        var folder = Path.Combine(Plugins, spec.Id);
        Directory.CreateDirectory(folder);
        var assembly = Path.GetRelativePath(folder, Path.GetFullPath(spec.Assembly, Plugins));
        return File.WriteAllTextAsync(
            Path.Combine(folder, "plugin.json"),
            JsonSerializer.Serialize(spec with { Assembly = assembly }, PipelineStore.JsonOptions),
            TestContext.Current.CancellationToken
        );
    }

    public async Task SelectPluginsAsync(IEnumerable<PluginSpec> specs)
    {
        var selected = new List<PluginSelection>();
        foreach (var spec in specs)
        {
            await WriteManifestAsync(spec);
            selected.Add(new(spec.Id, spec.Required));
        }
        await File.WriteAllTextAsync(
            Path.Combine(Plugins, "plugins.json"),
            JsonSerializer.Serialize(selected, PipelineStore.JsonOptions),
            TestContext.Current.CancellationToken
        );
    }

    public async Task<(int Code, string Output, string Error)> RunAsync(
        string command,
        string action,
        params string[] arguments
    )
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (
            var value in new[]
            {
                typeof(ApplicationHost).Assembly.Location,
                "--database",
                Database,
                "--plugin-directory",
                Plugins,
                "--Logging:LogLevel:Default",
                "None",
                "--command",
                command,
                "--action",
                action,
            }.Concat(arguments)
        )
            start.ArgumentList.Add(value);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        return (process.ExitCode, await output, await error);
    }

    public async Task<string> SuccessAsync(string command, string action, params string[] arguments)
    {
        var result = await RunAsync(command, action, arguments);
        Assert.True(result.Code == 0, result.Output + result.Error);
        return result.Output;
    }

    public async Task<T> JsonAsync<T>(string command, string action, params string[] arguments) =>
        JsonSerializer.Deserialize<T>(await SuccessAsync(command, action, arguments), PipelineStore.JsonOptions)!;

    public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
}
