using System.Data.Common;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Games;

public sealed partial class GameLibrary
{
    private Dictionary<string, string> ReadOverrides(DbConnection db, DbTransaction tx, string id)
    {
        using var command = Command(
            db,
            tx,
            $"SELECT field,value FROM {overrides} WHERE account=$account AND game=$id",
            (GameId, id)
        );
        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.Read())
            result.Add(reader.GetString(0), reader.GetString(1));
        return result;
    }

    public void SetField(string id, string field, string? value)
    {
        using var db = database.Open();
        using var tx = db.BeginTransaction();
        _ = Get(db, tx, id, AccessMode.Write);
        SaveField(db, tx, id, field, value);
        tx.Commit();
    }

    public void EditInfo(GameEntry entry, IReadOnlyDictionary<string, string?> changedFields)
    {
        if (string.IsNullOrWhiteSpace(entry.Title))
            throw new ArgumentException("The game needs a title.", nameof(entry));
        using var db = database.Open();
        using var tx = db.BeginTransaction();
        _ = Get(db, tx, entry.Id, AccessMode.Write);
        SaveEntry(db, tx, entry);
        foreach (var pair in changedFields)
            SaveField(db, tx, entry.Id, pair.Key, pair.Value);
        tx.Commit();
    }

    private void SaveField(DbConnection db, DbTransaction tx, string id, string field, string? value)
    {
        if (field is not ("Description" or "Cover" or "Released" or "Other title" or "Developers" or "Publishers"))
            throw new ArgumentException("Unknown metadata field.", nameof(field));
        using var cmd = Command(
            db,
            tx,
            string.IsNullOrWhiteSpace(value)
                ? $"DELETE FROM {overrides} WHERE account=$account AND game=$id AND field=$field"
                : $"""
                INSERT INTO {overrides}(account,game,field,value) VALUES ($account,$id,$field,$value)
                ON CONFLICT(account,game,field) DO UPDATE SET value=excluded.value
                """,
            (GameId, id),
            ("$field", field),
            ("$value", value)
        );
        cmd.ExecuteNonQuery();
    }

    public void Unlink(string id, string provider, string external)
    {
        storage.Write(database, (db, tx) =>
        {
            _ = Get(db, tx, id, AccessMode.Write);
            using var cmd = Command(
                db,
                tx,
                $"DELETE FROM {sources} WHERE account=$account AND game=$id AND provider=$provider AND external_id=$external",
                (GameId, id),
                ("$provider", provider),
                ("$external", external)
            );
            cmd.ExecuteNonQuery();
            return new BusinessChange<bool>(
                true,
                [],
                [],
                [new(new(provider + ".game", external, access.Account), Reference(id), "describes")]
            );
        });
    }
}
