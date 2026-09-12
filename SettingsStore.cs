using System.Text.Json;

namespace SSOLauncher;

// Non-sensitive app settings (install dir, language, toggles, window size).
// Plain JSON is fine here - unlike ProfileStore, nothing secret is stored.
internal sealed class LauncherSettings
{
    public string InstallDir { get; set; } = "";
    public string Language { get; set; } = "de";            // language the game is started with
    public string LauncherLanguage { get; set; } = "en";    // launcher UI + news feed
    public bool VerifyFiles { get; set; }
    public bool RandomDevice { get; set; }
    public bool MinimizeToTray { get; set; }
    public bool DarkMode { get; set; }
    public bool StreamerMode { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
}

internal static class SettingsStore
{
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static LauncherSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.LauncherSettings);
                if (settings != null) return settings;
            }
        }
        catch { }
        return new LauncherSettings();
    }

    public static void Save(LauncherSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, AppJsonContext.Default.LauncherSettings);
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }
}
