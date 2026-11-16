using LazyTuiHub;
using LazyTuiHub.Commands;
using LazyTuiHub.Examples;

if (args.Contains("--workflow-demo", StringComparer.Ordinal))
{
    await WorkflowDemo.RunAsync();
    return;
}

await using var app = ApplicationHost.Create(args);
if (app.Configuration.GetValue<bool>("demo"))
{
    await GreetingDemo.RunAsync(app);
}
else if (app.Configuration["rss"] is { } command)
{
    await RssCommand.RunAsync(app, command);
}
else
{
    await app.RunAsync();
}
