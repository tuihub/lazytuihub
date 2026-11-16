using LazyTuiHub;
using LazyTuiHub.Commands;
using LazyTuiHub.Examples;

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
