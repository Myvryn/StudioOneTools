namespace StudioOneTools.App.Settings;

public sealed class AppUserSettings
{
    public string DefaultSongFolder    { get; set; } = string.Empty;

    public string DefaultArchiveFolder { get; set; } = string.Empty;

    public bool DebugMode { get; set; } = false;
}
