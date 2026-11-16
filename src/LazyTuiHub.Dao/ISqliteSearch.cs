using System.Data.Common;

namespace LazyTuiHub.Dao;

public interface ISqliteSearch
{
    void CreateTable(DaoTable table);
    void Index(DaoTable table, string key, string text, DbTransaction transaction);
    IReadOnlyList<string> Search(DaoTable table, string query);
}
