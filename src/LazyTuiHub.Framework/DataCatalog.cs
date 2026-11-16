using Microsoft.Data.Sqlite;

namespace LazyTuiHub.Framework;

public sealed class DataCatalog(SqliteDatabase database)
{
    public static void Register(SqliteConnection connection, SqliteTransaction transaction, DataReference reference)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO data_objects(type, key) VALUES ($type, $key) ON CONFLICT DO NOTHING";
        command.Parameters.AddWithValue("$type", reference.Type);
        command.Parameters.AddWithValue("$key", reference.Key);
        command.ExecuteNonQuery();
    }

    public static void Relate(SqliteConnection connection, SqliteTransaction transaction, DataRelation relation)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO data_relations(source_type, source_key, target_type, target_key, kind)
            VALUES ($sourceType, $sourceKey, $targetType, $targetKey, $kind) ON CONFLICT DO NOTHING
            """;
        command.Parameters.AddWithValue("$sourceType", relation.Source.Type);
        command.Parameters.AddWithValue("$sourceKey", relation.Source.Key);
        command.Parameters.AddWithValue("$targetType", relation.Target.Type);
        command.Parameters.AddWithValue("$targetKey", relation.Target.Key);
        command.Parameters.AddWithValue("$kind", relation.Kind);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<DataRelation> ReadRelations(DataReference source)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT target_type, target_key, kind FROM data_relations
            WHERE source_type = $type AND source_key = $key ORDER BY target_type, target_key, kind
            """;
        command.Parameters.AddWithValue("$type", source.Type);
        command.Parameters.AddWithValue("$key", source.Key);
        using var reader = command.ExecuteReader();
        var relations = new List<DataRelation>();
        while (reader.Read())
            relations.Add(
                new(
                    source,
                    new(
                        reader.GetString(reader.GetOrdinal("target_type")),
                        reader.GetString(reader.GetOrdinal("target_key"))
                    ),
                    reader.GetString(reader.GetOrdinal("kind"))
                )
            );
        return relations;
    }
}
