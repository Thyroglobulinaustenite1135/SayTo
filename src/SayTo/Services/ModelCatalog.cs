using System.IO;

namespace SayTo.Services;

public sealed record SpeechModel(string Lang, string DisplayName, string Id, string Url, long ApproxBytes, bool IsCompact)
{
    public string SizeLabel => ApproxBytes >= 1024L * 1024 * 1024
        ? $"{ApproxBytes / 1024.0 / 1024 / 1024:0.#} GB"
        : $"{ApproxBytes / 1024 / 1024} MB";

    public override string ToString() => $"{DisplayName} ({Id})";
}

public static class ModelCatalog
{
    // compact models (downloaded by default, fast on old CPUs)
    public static readonly SpeechModel Fa = new(
        "fa", "فارسی", "vosk-model-small-fa-0.42",
        "https://alphacephei.com/vosk/models/vosk-model-small-fa-0.42.zip",
        53L * 1024 * 1024, true);

    public static readonly SpeechModel En = new(
        "en", "English (US)", "vosk-model-small-en-us-0.15",
        "https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip",
        40L * 1024 * 1024, true);

    // full/accurate models (optional downloads)
    public static readonly SpeechModel FaFull = new(
        "fa", "فارسی — دقیق", "vosk-model-fa-0.5",
        "https://alphacephei.com/vosk/models/vosk-model-fa-0.5.zip",
        1024L * 1024 * 1024, false);

    public static readonly SpeechModel EnFull = new(
        "en", "English (US) — accurate", "vosk-model-en-us-0.22",
        "https://alphacephei.com/vosk/models/vosk-model-en-us-0.22.zip",
        1843L * 1024 * 1024, false);

    public static readonly SpeechModel[] All = { Fa, FaFull, En, EnFull };

    public static IEnumerable<SpeechModel> For(string lang) => All.Where(m => m.Lang == lang);

    /// <summary>The compact default model of a language.</summary>
    public static SpeechModel Get(string lang) => lang == "en" ? En : Fa;

    public static SpeechModel? Find(string id) => All.FirstOrDefault(m => m.Id == id);

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

    public static string PathOf(string id) => Path.Combine(BaseDir, id);

    [Obsolete("use PathOf/IsIdInstalled")] public static string ModelPath(string lang) => PathOf(Get(lang).Id);

    public static bool IsIdInstalled(string id)
    {
        var p = PathOf(id);
        return Directory.Exists(p) && (File.Exists(Path.Combine(p, "final.mdl")) || Directory.Exists(Path.Combine(p, "am")));
    }

    /// <summary>True when at least one model of the language is available.</summary>
    public static bool IsInstalled(string lang) => For(lang).Any(m => IsIdInstalled(m.Id));

    /// <summary>Prefers the user's active model; falls back to the compact one.</summary>
    public static string ResolveId(string lang, string preferredId)
    {
        if (!string.IsNullOrEmpty(preferredId) && IsIdInstalled(preferredId)) return preferredId;
        var compact = Get(lang);
        return IsIdInstalled(compact.Id) ? compact.Id : preferredId ?? compact.Id;
    }
}
