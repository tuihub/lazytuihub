using Microsoft.Data.Sqlite;

namespace LazyTuiHub.Framework;

public sealed class BusinessStorage(SqliteDatabase database)
{
    public SqliteConnection Open() => database.Open();

    public T Write<T>(Func<SqliteConnection, SqliteTransaction, BusinessChange<T>> write)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var change = write(connection, transaction);
        DataCatalog.Apply(connection, transaction, change);
        transaction.Commit();
        return change.Value;
    }
}
