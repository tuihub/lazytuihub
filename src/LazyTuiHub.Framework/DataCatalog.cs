using System.Data.Common;
using LazyTuiHub.Dao;

namespace LazyTuiHub.Framework;

public sealed class DataCatalog(SqliteDatabase database)
{
    private static void Register(DbConnection connection, DbTransaction transaction, DataReference reference)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO data_objects(type, key) VALUES ($type, $key) ON CONFLICT DO NOTHING";
        command.AddValue("$type", reference.Type);
        command.AddValue("$key", reference.Key);
        command.ExecuteNonQuery();
    }

    private static void Relate(DbConnection connection, DbTransaction transaction, DataRelation relation)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO data_relations(source_type, source_key, target_type, target_key, kind)
            VALUES ($sourceType, $sourceKey, $targetType, $targetKey, $kind) ON CONFLICT DO NOTHING
            """;
        command.AddValue("$sourceType", relation.Source.Type);
        command.AddValue("$sourceKey", relation.Source.Key);
        command.AddValue("$targetType", relation.Target.Type);
        command.AddValue("$targetKey", relation.Target.Key);
        command.AddValue("$kind", relation.Kind);
        command.ExecuteNonQuery();
    }

    internal static void Apply<T>(DbConnection connection, DbTransaction transaction, BusinessChange<T> change)
    {
        foreach (var reference in change.Objects)
            Register(connection, transaction, reference);
        foreach (var relation in change.Relations)
            Relate(connection, transaction, relation);
    }

    public IReadOnlyList<DataRelation> ReadRelations(DataReference source)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT target_type, target_key, kind FROM data_relations
            WHERE source_type = $type AND source_key = $key ORDER BY target_type, target_key, kind
            """;
        command.AddValue("$type", source.Type);
        command.AddValue("$key", source.Key);
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
