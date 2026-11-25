using System.Security.Cryptography;
using System.Text;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Interaction;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace LazyTuiHub.Access;

internal sealed class AccountAuthority
{
    private const string NameParameter = "$name";
    private const string UserParameter = "$user";
    public static string UsersUri { get; } = ResourceUri.SystemControl("users");
    public static string DevicesUri { get; } = ResourceUri.SystemControl("devices");
    public static string OperationsUri { get; } = ResourceUri.SystemControl("operations");
    private readonly PasswordHasher<string> passwords = new();
    private readonly SqliteDatabase database;
    private readonly TimeProvider clock;
    private readonly TimeSpan lifetime;

    public AccountAuthority(SqliteDatabase database, IConfiguration configuration, TimeProvider clock)
    {
        this.database = database;
        this.clock = clock;
        lifetime = configuration.GetValue<TimeSpan?>("Access:SessionLifetime") ?? TimeSpan.FromHours(8);
        if (lifetime <= TimeSpan.Zero)
            throw new ArgumentException("Access:SessionLifetime must be positive.", nameof(configuration));
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS access_users (
                id TEXT PRIMARY KEY, account TEXT NOT NULL UNIQUE, name TEXT NOT NULL UNIQUE, password_hash TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS access_devices (
                id TEXT PRIMARY KEY, owner TEXT NOT NULL REFERENCES access_users(id)
            );
            CREATE TABLE IF NOT EXISTS access_sessions (
                digest TEXT PRIMARY KEY, user_id TEXT NOT NULL REFERENCES access_users(id), expires INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS access_grants (
                principal TEXT NOT NULL REFERENCES access_users(id), uri TEXT NOT NULL, mode INTEGER NOT NULL,
                PRIMARY KEY (principal, uri)
            );
            """;
        command.ExecuteNonQuery();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM access_users)";
        if ((long)command.ExecuteScalar()! == 0)
        {
            var name = configuration["Access:AdminUser"]
                ?? throw new InvalidOperationException("Access:AdminUser is required to initialize this authority.");
            var password = configuration["Access:AdminPassword"]
                ?? throw new InvalidOperationException(
                    "Access:AdminPassword is required to initialize this authority."
                );
            var user = InsertUser(connection, transaction, name, password);
            WriteGrant(connection, transaction, user.Id, UsersUri, AccessMode.Write);
            WriteGrant(connection, transaction, user.Id, DevicesUri, AccessMode.Write);
            WriteGrant(connection, transaction, user.Id, OperationsUri, AccessMode.Write);
        }
        transaction.Commit();
    }

    public IssuedSession Login(string name, string password)
    {
        using var connection = database.Open();
        User user;
        using (var account = connection.CreateCommand())
        {
            account.CommandText = "SELECT id, account, password_hash FROM access_users WHERE name = $name";
            account.AddValue(NameParameter, name);
            using var reader = account.ExecuteReader();
            if (!reader.Read())
                throw new AccessRequiredException();
            user = new(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("account")),
                reader.GetString(reader.GetOrdinal("password_hash"))
            );
        }
        if (passwords.VerifyHashedPassword(user.Id, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            throw new AccessRequiredException();

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = clock.GetUtcNow().Add(lifetime);
        using var record = connection.CreateCommand();
        record.CommandText = "INSERT INTO access_sessions VALUES ($digest, $user, $expires)";
        record.AddValue("$digest", Digest(token));
        record.AddValue(UserParameter, user.Id);
        record.AddValue("$expires", expires.ToUnixTimeSeconds());
        record.ExecuteNonQuery();
        return new(token, user.Account, expires);
    }

    public AccessPrincipal Authenticate(string token, string? targetAccount = null)
    {
        using var connection = database.Open();
        var user = SessionUser(connection, null, token);
        if (targetAccount is not null && !ResourceUri.IsAccountId(targetAccount))
            throw new AccessDeniedException();
        var account = targetAccount ?? user.Account;
        return new(
            user.Id,
            account,
            () => ResolveGrants(token, account),
            string.Equals(account, user.Account, StringComparison.Ordinal)
        );
    }

    private List<UriGrant> ResolveGrants(string token, string account)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        var user = SessionUser(connection, transaction, token);
        return ReadGrants(connection, transaction, user, account);
    }

    public (string User, string Account) CreateUser(AccessPrincipal actor, string name, string password)
    {
        if (!actor.Allows(UsersUri, AccessMode.Write))
            throw new AccessDeniedException();
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var user = InsertUser(connection, transaction, name, password);
        transaction.Commit();
        return (user.Id, user.Account);
    }

    public void SetDeviceOwner(AccessPrincipal actor, string device, string username)
    {
        if (!actor.Allows(DevicesUri, AccessMode.Write))
            throw new AccessDeniedException();
        _ = ResourceUri.DeviceJobs(device);
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var owner = GetUserId(connection, transaction, username);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO access_devices(id, owner) VALUES ($device, $user)
            ON CONFLICT(id) DO UPDATE SET owner = excluded.owner
            """;
        command.AddValue("$device", device);
        command.AddValue(UserParameter, owner);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void SetGrant(AccessPrincipal actor, string username, string uri, AccessMode mode)
    {
        var control = ResourceUri.Segments(ResourceUri.Canonical(uri)) switch
        {
            [not "core", "accounts", var account, ..] when ResourceUri.IsAccountId(account) =>
                ResourceUri.AccountGrants(account),
            ["core", "devices", var device, "jobs"] => ResourceUri.DeviceGrants(device),
            _ => throw new AccessDeniedException(),
        };
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        if (!actor.Allows(control, AccessMode.Write))
            throw new AccessDeniedException();
        var principal = GetUserId(connection, transaction, username);
        WriteGrant(connection, transaction, principal, uri, mode);
        transaction.Commit();
    }

    public void RevokeSession(string token)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM access_sessions WHERE digest = $digest";
        command.AddValue("$digest", Digest(token));
        command.ExecuteNonQuery();
    }

    private Session SessionUser(SqliteConnection connection, SqliteTransaction? transaction, string token)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT u.id, u.account FROM access_sessions s JOIN access_users u ON s.user_id = u.id
            WHERE s.digest = $digest AND expires > $now
            """;
        command.AddValue("$digest", Digest(token));
        command.AddValue("$now", clock.GetUtcNow().ToUnixTimeSeconds());
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new AccessRequiredException();
        return new(reader.GetString(0), reader.GetString(1));
    }

    private static List<UriGrant> ReadGrants(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Session user,
        string account
    )
    {
        var grants = new List<UriGrant>
        {
            new(ResourceUri.AccountGrants(user.Account), AccessMode.Write),
            new(ResourceUri.AccountRelations(user.Account), AccessMode.Read),
        };
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT uri, mode FROM access_grants WHERE principal = $user";
        command.AddValue(UserParameter, user.Id);
        using (var result = command.ExecuteReader())
            while (result.Read())
            {
                var uri = result.GetString(0);
                if (
                    ResourceUri.Segments(uri) is not [_, "accounts", var owner, ..]
                    || string.Equals(owner, account, StringComparison.Ordinal)
                )
                    grants.Add(new(uri, (AccessMode)result.GetInt64(1)));
            }
        command.CommandText = "SELECT id FROM access_devices WHERE owner = $user";
        using var devices = command.ExecuteReader();
        while (devices.Read())
        {
            var device = devices.GetString(0);
            grants.Add(new(ResourceUri.DeviceJobs(device), AccessMode.Read | AccessMode.Write));
            grants.Add(new(ResourceUri.DeviceGrants(device), AccessMode.Write));
        }
        return grants;
    }

    private User InsertUser(SqliteConnection connection, SqliteTransaction transaction, string name, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrEmpty(password);
        var id = Guid.NewGuid().ToString("N");
        var user = new User(id, Guid.NewGuid().ToString("N"), passwords.HashPassword(id, password));
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO access_users VALUES ($id, $account, $name, $password)";
        command.AddValue("$id", user.Id);
        command.AddValue("$account", user.Account);
        command.AddValue(NameParameter, name);
        command.AddValue("$password", user.PasswordHash);
        command.ExecuteNonQuery();
        return user;
    }

    private static void WriteGrant(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string user,
        string uri,
        AccessMode mode
    )
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        if (mode == AccessMode.None)
            command.CommandText = "DELETE FROM access_grants WHERE principal = $user AND uri = $uri";
        else
            command.CommandText = """
                INSERT INTO access_grants VALUES ($user, $uri, $mode)
                ON CONFLICT(principal, uri) DO UPDATE SET mode = excluded.mode
                """;
        command.AddValue(UserParameter, user);
        command.AddValue("$uri", uri);
        command.AddValue("$mode", (int)mode);
        command.ExecuteNonQuery();
    }

    private static string GetUserId(SqliteConnection connection, SqliteTransaction transaction, string username)
    {
        using var target = connection.CreateCommand();
        target.Transaction = transaction;
        target.CommandText = "SELECT id FROM access_users WHERE name = $name";
        target.AddValue(NameParameter, username);
        return target.ExecuteScalar() as string ?? throw new KeyNotFoundException("The user does not exist.");
    }

    private static string Digest(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed record User(string Id, string Account, string PasswordHash);

    private sealed record Session(string Id, string Account);
}
