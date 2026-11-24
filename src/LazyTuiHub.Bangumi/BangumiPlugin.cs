using LazyTuiHub.Games;
using LazyTuiHub.Plugins;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Bangumi;

public sealed class BangumiPlugin : SqliteGameProvider
{
    public override void Configure(PluginRegistration registration)
    {
        registration.Services.AddSingleton<GameAccountProvider, BangumiAccount>();
        base.Configure(registration);
    }

    protected override string Query(bool search) =>
        """
            SELECT CAST(id AS TEXT) AS id, name, nullif(name_cn,'') AS alt, nullif(summary,'') AS summary, nullif(date,'') AS date FROM subject
            WHERE type=4 AND
            """ + (search ? " id IN (" + MatchingTitles + ") ORDER BY name,id" : " id=$key");

    protected override GameMetadata Read(SqliteDataReader row) =>
        new(
            "bangumi",
            row.GetString(0),
            row.GetString(1),
            "https://bgm.tv/subject/" + row.GetString(0),
            Optional(row, "summary"),
            ReleaseDate: Optional(row, "date"),
            AlternativeTitle: Optional(row, "alt")
        );
}
