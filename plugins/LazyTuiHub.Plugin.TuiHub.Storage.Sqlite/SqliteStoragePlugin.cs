using System.Globalization;
using LazyTuiHub.Dao;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Storage.Sqlite;

public sealed class SqliteStoragePlugin : IApplicationPlugin
{
    private static readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromMinutes(1);

    public void Configure(PluginRegistration registration)
    {
        var source = registration.Workspace;
        registration.DaoServices.Add(
            new(new(DaoMode.Unified, DaoContracts.Records, "workspace", "sqlite"), new SqliteRecordDao(source))
        );
        registration.DaoServices.Add(new(new(DaoMode.Native, DaoContracts.Relational, "workspace", "sqlite"), source));
        if (SqliteSearchDao.Available(source))
            registration.DaoServices.Add(
                new(
                    new(DaoMode.BackendSpecific, DaoContracts.SqliteSearch, "workspace", "sqlite"),
                    new SqliteSearchDao(source)
                )
            );
        var interval = registration.Settings.TryGetProperty("cleanupInterval", out var setting)
            ? TimeSpan.Parse(setting.GetString()!, CultureInfo.InvariantCulture)
            : DefaultCleanupInterval;
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(registration), "Cleanup interval must be positive.");
        registration.Tasks.Add(new("storage.retention", [], async (services, token) =>
            {
                var retention = services.GetRequiredService<IRetentionCleanup>();
                using var timer = new PeriodicTimer(interval);
                do
                {
                    retention.Clean(DateTimeOffset.UtcNow, token);
                } while (await timer.WaitForNextTickAsync(token));
            }));
    }
}
