using System.Data.Common;

namespace LazyTuiHub.Dao;

public static class DbCommands
{
    public static void AddValue(this DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
