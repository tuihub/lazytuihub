using System.Text;

namespace LazyTuiHub.Dao;

public static class DaoNames
{
    public static string Table(DaoTable table) => "p_" + Encode(table.Namespace) + "__" + Encode(table.Name);

    public static string Index(DaoTable table, string name) =>
        "i_" + Encode(table.Namespace) + "__" + Encode(table.Name) + "__" + Encode(name);

    private static string Encode(string value) => Convert.ToHexStringLower(Encoding.UTF8.GetBytes(value));
}
