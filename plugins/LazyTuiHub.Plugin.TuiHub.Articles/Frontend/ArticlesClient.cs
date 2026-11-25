using LazyTuiHub.Plugin.TuiHub.Articles.Api;
using LazyTuiHub.Plugin.TuiHub.Articles.Frontend;
using LazyTuiHub.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Plugin.TuiHub.Articles;

internal sealed class ArticlesClient : IPluginFrontend
{
    public void Configure(PluginServices registration)
    {
        if (registration.Remote)
        {
            registration.Services.AddTransient<IArticleApi, ArticleHttpClient>();
            registration.AddCapabilities<ArticleOperations>("articles");
        }
    }
}
