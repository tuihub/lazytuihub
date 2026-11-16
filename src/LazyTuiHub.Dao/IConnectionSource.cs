using System.Data.Common;

namespace LazyTuiHub.Dao;

public interface IConnectionSource
{
    DbConnection Open();
}
