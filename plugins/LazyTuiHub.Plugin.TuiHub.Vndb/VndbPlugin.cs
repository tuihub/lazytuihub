using System.Globalization;
using LazyTuiHub.Plugin.TuiHub.Games;
using LazyTuiHub.Sdk;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Plugin.TuiHub.Vndb;

internal sealed class VndbPlugin() : SqliteGameProvider("vndb")
{
    public override void Configure(PluginRegistration registration)
    {
        registration.Services.AddSingleton<GameAccountProvider, VndbAccount>();
        base.Configure(registration);
    }

    protected override string Query(bool search) =>
        """
            SELECT v.id,t.title,t.latin,v.description,v.image,
                (SELECT json_group_array(title) FROM (SELECT DISTINCT title FROM vn_titles WHERE id=v.id ORDER BY title)) AS titles,
                (SELECT json_group_array(name) FROM (SELECT DISTINCT p.name FROM releases_vn rv JOIN releases r ON r.id=rv.id JOIN releases_producers rp ON rp.id=rv.id JOIN producers p ON p.id=rp.pid WHERE rv.vid=v.id AND r.official AND NOT r.patch AND rp.developer ORDER BY p.name)) AS developers,
                (SELECT json_group_array(name) FROM (SELECT DISTINCT p.name FROM releases_vn rv JOIN releases r ON r.id=rv.id JOIN releases_producers rp ON rp.id=rv.id JOIN producers p ON p.id=rp.pid WHERE rv.vid=v.id AND r.official AND NOT r.patch AND rp.publisher ORDER BY p.name)) AS publishers,
                (SELECT min(r.released) FROM releases_vn rv JOIN releases r ON r.id=rv.id
                 WHERE rv.vid=v.id AND rv.rtype='complete' AND r.official AND NOT r.patch AND r.released<99999999) AS released
            FROM vn v JOIN vn_titles t ON t.id=v.id AND t.lang=coalesce(
                (SELECT a.lang FROM vn_titles a WHERE a.id=v.id AND a.lang=v.olang),
                (SELECT min(b.lang) FROM vn_titles b WHERE b.id=v.id))
            WHERE
            """ + (search ? " v.id IN (" + MatchingTitles + ") ORDER BY t.title,v.id" : " v.id=$key");

    protected override GameMetadata Read(SqliteDataReader row)
    {
        var id = row.GetString(0);
        var cover = Optional(row, "image");

        return new(
            "vndb",
            id,
            (string)row["title"],
            "https://vndb.org/" + id,
            Optional(row, "description"),
            CoverUrl(cover),
            ReleaseDate(row["released"]),
            Optional(row, "latin"),
            Names(row, "developers"),
            Names(row, "publishers"),
            OtherTitles: Names(row, "titles")
        );
    }

    private static string[]? Names(SqliteDataReader row, string field) =>
        System.Text.Json.JsonSerializer.Deserialize<string[]>((string)row[field]);

    private static string? CoverUrl(string? image)
    {
        const int shardCount = 100;
        if (
            image is null
            || !image.StartsWith("cv", StringComparison.Ordinal)
            || !int.TryParse(image.AsSpan("cv".Length), CultureInfo.InvariantCulture, out var number)
        )
            return null;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"https://t.vndb.org/cv/{number % shardCount:D2}/{number}.jpg"
        );
    }

    private static string? ReleaseDate(object raw)
    {
        if (raw is not long date)
            return null;
        const int datePart = 100;
        const int unknown = 99;
        var day = date % datePart;
        var month = date / datePart % datePart;
        var year = date / datePart / datePart;
        if (month == unknown)
            return year.ToString(CultureInfo.InvariantCulture);
        return day == unknown
            ? string.Create(CultureInfo.InvariantCulture, $"{year:D4}-{month:D2}")
            : string.Create(CultureInfo.InvariantCulture, $"{year:D4}-{month:D2}-{day:D2}");
    }
}
