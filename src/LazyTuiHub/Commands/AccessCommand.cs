using LazyTuiHub.Access;
using LazyTuiHub.Cli;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Commands;

internal static class AccessCommand
{
    private const string Username = "username";

    public static void Run(WebApplication app, string action)
    {
        var authority = app.Services.GetRequiredService<AccountAuthority>();
        var config = app.Configuration;
        if (string.Equals(action, "login", StringComparison.Ordinal))
        {
            CommandJson.Write(
                authority.Login(CommandJson.Required(config, Username), RemoteConsole.ReadPassword(config))
            );
            return;
        }
        var token = CommandJson.Required(config, "Access:Session");
        var actor = authority.Authenticate(token);
        switch (action)
        {
            case "create-user":
                var (user, account) = authority.CreateUser(
                    actor,
                    CommandJson.Required(config, Username),
                    RemoteConsole.ReadPassword(config)
                );
                CommandJson.Write(new { user, account });
                break;
            case "set-device-owner":
                authority.SetDeviceOwner(
                    actor,
                    CommandJson.Required(config, "device"),
                    CommandJson.Required(config, Username)
                );
                break;
            case "grant":
            case "revoke":
                authority.SetGrant(
                    actor,
                    CommandJson.Required(config, Username),
                    CommandJson.Required(config, "uri"),
                    string.Equals(action, "revoke", StringComparison.Ordinal)
                        ? AccessMode.None
                        : Mode(CommandJson.Required(config, "mode"))
                );
                break;
            case "logout":
                authority.RevokeSession(token);
                break;
            default:
                throw new ArgumentException(
                    "Choose login, create-user, set-device-owner, grant, revoke, or logout.",
                    nameof(action)
                );
        }
    }

    private static AccessMode Mode(string mode) =>
        mode switch
        {
            "read" => AccessMode.Read,
            "write" => AccessMode.Write,
            "read-write" => AccessMode.Read | AccessMode.Write,
            _ => throw new ArgumentException("Choose read, write, or read-write.", nameof(mode)),
        };
}
