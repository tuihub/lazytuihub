using System.Diagnostics;
using System.Text.Json;

namespace UiComparison;

public static class Measurements
{
    private static readonly Stopwatch Startup = Stopwatch.StartNew();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Start() => Startup.Restart();

    public static void Save(string framework, string stage, object details)
    {
        using var process = Process.GetCurrentProcess();
        var directory = Environment.GetEnvironmentVariable("UI_COMPARISON_ARTIFACTS")
            ?? Path.Combine(AppContext.BaseDirectory, "artifacts");
        Directory.CreateDirectory(directory);
        var result = new
        {
            Framework = framework,
            Stage = stage,
            ElapsedMilliseconds = Startup.Elapsed.TotalMilliseconds,
            WorkingSetBytes = process.WorkingSet64,
            ManagedBytes = GC.GetTotalMemory(false),
            Runtime = Environment.Version.ToString(),
            OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            Details = details,
        };
        var json = JsonSerializer.Serialize(result, JsonOptions);
        File.WriteAllText(Path.Combine(directory, $"{framework}-{stage}.json"), json);
        Console.WriteLine(json);
    }

    public static void VerifySession()
    {
        var session = new ReaderSession { Feed = "Design", Search = "workflow" };
        if (
            session.Visible().Length == 0
            || session.Visible().Any(article => !string.Equals(article.Feed, "Design", StringComparison.Ordinal))
        )
            throw new InvalidOperationException("Feed/search intersection failed.");
        session.ToggleRead(1);
        session.ToggleRead(1);
        if (session.Read.Count != 0)
            throw new InvalidOperationException("Read state round-trip failed.");
    }
}
