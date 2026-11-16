using LazyTuiHub;
using LazyTuiHub.Commands;
using LazyTuiHub.Examples;

if (args.Contains("--workflow-demo", StringComparer.Ordinal))
{
    await WorkflowDemo.RunAsync();
    return;
}

if (args.Contains("--rss-demo", StringComparer.Ordinal))
{
    await RssDemo.RunAsync();
    return;
}

await using var app = ApplicationHost.Create(args);
if (app.Configuration.GetValue<bool>("demo"))
{
    await GreetingDemo.RunAsync(app);
}
else if (app.Configuration["command"] is { } command)
{
    await CommandRunner.RunAsync(app, command);
}
else
{
    await app.RunAsync();
}
