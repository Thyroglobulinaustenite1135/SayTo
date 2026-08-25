using System.IO;
using System.Text.Json;

namespace SayTo.Services;

public sealed class AppSettings
{
    public string UiLanguage { get; set; } = "fa";          // "fa" | "en"
    public string Theme { get; set; } = "dark";             // "dark" | "light"
    public string RecognizeLanguage { get; set; } = "fa";   // last used recognition language
    public string HotKey { get; set; } = "Ctrl+Alt+S";      // preset name
    public bool AutoStopOnSilence { get; set; } = true;
    public double AutoStopSeconds { get; set; } = 2.0;
    public bool InsertAfterGlobalDictation { get; set; } = true;
    public bool CloseToTrayHintShown { get; set; } = false;
}

public static class SettingsStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SayTo");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (s != null) return s;
            }
        }
        catch { /* corrupted settings fall back to defaults */ }
        return new AppSettings();
    }

    public static void Save(AppSettings s)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(s, Options));
        }
        catch { /* non-fatal */ }
    }
}
