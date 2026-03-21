using System.IO;
using System.Text.Json;

namespace StudioOneTools.App.Settings;

public static class UserSettingsService
{
    #region Fields

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StudioOneTools",
        "settings.json");

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    #endregion

    #region Public Methods

    public static AppUserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppUserSettings();
            }

            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppUserSettings>(json) ?? new AppUserSettings();
        }
        catch
        {
            return new AppUserSettings();
        }
    }

    public static void Save(AppUserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, WriteOptions));
        }
        catch
        {
            // Settings failures are non-critical; ignore silently.
        }
    }

    #endregion
}
