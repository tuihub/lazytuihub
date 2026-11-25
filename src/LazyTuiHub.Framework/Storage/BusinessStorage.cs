using System.Data.Common;
using LazyTuiHub.Dao;

namespace LazyTuiHub.Framework;

public sealed class BusinessStorage(IConnectionSource workspace)
{
    public T Write<T>(IConnectionSource source, Func<DbConnection, DbTransaction, BusinessChange<T>> write)
    {
        if (!ReferenceEquals(source, workspace))
            throw new ArgumentException(
                "Atomic metadata registration requires the workspace connection source.",
                nameof(source)
            );
        using var connection = workspace.Open();
        using var transaction = connection.BeginTransaction();
        var change = write(connection, transaction);
        DataCatalog.Apply(connection, transaction, change);
        transaction.Commit();
        return change.Value;
    }
}
