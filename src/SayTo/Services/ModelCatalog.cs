using System.IO;

namespace SayTo.Services;

public sealed record SpeechModel(string Lang, string DisplayName, string Id, string Url, long ApproxBytes)
{
    public override string ToString() => $"{DisplayName} ({Id})";
}

public static class ModelCatalog
{
    public static readonly SpeechModel Fa = new(
        "fa", "فارسی", "vosk-model-small-fa-0.42",
        "https://alphacephei.com/vosk/models/vosk-model-small-fa-0.42.zip",
        53L * 1024 * 1024);

    public static readonly SpeechModel En = new(
        "en", "English (US)", "vosk-model-small-en-us-0.15",
        "https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip",
        40L * 1024 * 1024);

    public static SpeechModel Get(string lang) => lang == "en" ? En : Fa;

    /// <summary>Models placed next to the exe win (portable/offline bundles);
    /// otherwise they live under %LOCALAPPDATA%\SayTo\models.</summary>
    public static string BaseDir
    {
        get
        {
            var portable = Path.Combine(AppContext.BaseDirectory, "models");
            if (Directory.Exists(portable)) return portable;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SayTo", "models");
        }
    }

    public static string ModelPath(string lang) => Path.Combine(BaseDir, Get(lang).Id);

    public static bool IsInstalled(string lang)
    {
        var p = ModelPath(lang);
        return Directory.Exists(p) && (File.Exists(Path.Combine(p, "final.mdl")) || Directory.Exists(Path.Combine(p, "am")));
    }
}
