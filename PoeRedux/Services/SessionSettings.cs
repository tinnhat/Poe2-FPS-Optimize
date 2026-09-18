using System.IO;
using System.Text.Json;

namespace PoeRedux.Services;

public sealed record ColorModSetting(bool Enabled, string Group);

public sealed record SessionSettings(string GamePath, double CameraZoom, string[] SelectedPatches,
    Dictionary<string, string>? GroupColors = null,
    Dictionary<string, ColorModSetting>? ModColors = null,
    Dictionary<string, Dictionary<string, bool>>? PatchOptions = null)
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PoeRedux", "session.json");

    public static SessionSettings? Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<SessionSettings>(File.ReadAllText(SettingsPath))
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are optional; an unwritable profile must not prevent closing the app.
        }
    }
}
