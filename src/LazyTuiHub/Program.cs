using System.Net;
using LazyTuiHub.Api;
using LazyTuiHub.Application;
using LazyTuiHub.Examples;
using LazyTuiHub.Transport;

var builder = WebApplication.CreateBuilder(args);
var demo = builder.Configuration.GetValue<bool>("demo");
builder.Services.AddGrpc().AddJsonTranscoding();
builder.Services.AddSingleton<IGreetingApi, GreetingApplication>();

if (demo)
{
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
}

await using var app = builder.Build();
app.MapGrpcService<GreetingService>();

if (demo)
{
    await GreetingDemo.RunAsync(app);
}
else
{
    await app.RunAsync();
}
