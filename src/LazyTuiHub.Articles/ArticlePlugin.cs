using LazyTuiHub.Api;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Articles;

public sealed class ArticlePlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration)
    {
        registration.Daos.Requirements.Add(new("database", DaoMode.Native, DaoContracts.Relational, Backend: "sqlite"));
        registration.Daos.Requirements.Add(
            new("search", DaoMode.BackendSpecific, DaoContracts.SqliteSearch, Backend: "sqlite")
        );
        registration.Services.AddSingleton(services => new ArticleStore(
            registration.Daos,
            registration.Storage,
            services.GetRequiredService<ResourceAccess>()
        ));
        registration.Services.AddSingleton<IArticleApi, ArticleApplication>();
        registration.DataTypes.Add("articles.article");
        registration.RegisterNodes = (nodes, services) =>
            ArticleModule.Register(nodes, services.GetRequiredService<ArticleStore>());
        foreach (var workflow in ArticleModule.Pipelines())
            registration.Workflows.Add(workflow);
        registration.Commands.Add(new("articles", ArticleCommand.RunAsync));
        registration.MapEndpoints = endpoints => endpoints.MapGrpcService<ArticleService>();
    }
}
