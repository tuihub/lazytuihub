using System.Text.Json;
using System.Text.Json.Serialization;

namespace LazyTuiHub.Desktop;

public sealed class AppearanceSettings(string filename)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter<DesignSystem>(allowIntegerValues: false),
            new JsonStringEnumConverter<ColorMode>(allowIntegerValues: false),
        },
    };

    public AppearancePreferences Load()
    {
        try
        {
            return JsonSerializer.Deserialize<AppearancePreferences>(File.ReadAllText(filename), JsonOptions)
                ?? throw new JsonException("Appearance settings must contain an object.");
        }
        catch (FileNotFoundException)
        {
            return new();
        }
        catch (DirectoryNotFoundException)
        {
            return new();
        }
    }

    public void Save(AppearancePreferences preferences)
    {
        var path = Path.GetFullPath(filename);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences, JsonOptions));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
