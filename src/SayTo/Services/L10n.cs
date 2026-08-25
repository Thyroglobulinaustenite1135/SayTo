using System.ComponentModel;

namespace SayTo.Services;

/// <summary>Bilingual UI strings (fa/en). The layout itself always stays LTR.</summary>
public static class L10n
{
    public static event Action? LanguageChanged;

    private static string _lang = "fa";
    public static string Lang => _lang;
    public static bool IsFa => _lang == "fa";

    public static void SetLanguage(string lang)
    {
        if (_lang == lang) return;
        _lang = lang == "en" ? "en" : "fa";
        LanguageChanged?.Invoke();
    }

    private static readonly Dictionary<string, (string Fa, string En)> Strings = new()
    {
        ["app.title"] = ("SayTo — دیکته‌گر صوتی", "SayTo — Voice Typing"),
        ["app.subtitle"] = ("دیکته‌ی آفلاین فارسی و انگلیسی", "Offline speech-to-text · Persian & English"),

        ["status.idle"] = ("آماده — برای شروع دکمه‌ی میکروفون را بزنید", "Ready — press the microphone to start"),
        ["status.loading"] = ("در حال بارگذاری مدل…", "Loading model…"),
        ["status.listening"] = ("در حال شنیدن…", "Listening…"),
        ["status.downloading"] = ("در حال دانلود مدل…", "Downloading model…"),
        ["status.modelmissing"] = ("مدل این زبان دانلود نشده است", "Model for this language is not downloaded"),
        ["status.nomic"] = ("میکروفونی پیدا نشد", "No microphone found"),
        ["status.error"] = ("خطا", "Error"),

        ["btn.copy"] = ("کپی", "Copy"),
        ["btn.clear"] = ("پاک کردن", "Clear"),
        ["btn.insert"] = ("درج در برنامه", "Insert"),
        ["btn.copy.hint"] = ("کپی متن در کلیپ‌بورد", "Copy text to clipboard"),
        ["btn.clear.hint"] = ("پاک کردن متن", "Clear the text"),
        ["btn.insert.hint"] = ("تایپ متن در پنجره‌ی قبلی", "Type the text into the previous window"),
        ["btn.stop.hint"] = ("توقف (همان میانبر)", "Stop (same shortcut)"),

        ["placeholder"] = ("اینجا متن دیکته‌ی شما ظاهر می‌شود…", "Your dictation will appear here…"),

        ["seg.fa"] = ("فارسی", "فارسی"),
        ["seg.en"] = ("English", "English"),
        ["title.speech"] = ("زبان گفتار:", "Speech:"),
        ["title.speech.hint"] = ("مدل تشخیص گفتار برای دیکته", "Recognition model for dictation"),

        ["setup.title"] = ("دانلود مدل تشخیص گفتار", "Download speech models"),
        ["setup.desc"] = ("برای دیکته‌ی آفلاین، مدل هر زبان یک‌بار دانلود می‌شود. پس از آن اینترنت لازم نیست.",
                          "Each language model is downloaded once for offline use. No internet needed afterwards."),
        ["setup.download"] = ("دانلود", "Download"),
        ["setup.downloadall"] = ("دانلود هر دو مدل", "Download both models"),
        ["setup.later"] = ("بعداً", "Later"),
        ["setup.ready"] = ("آماده", "Ready"),
        ["setup.downloading"] = ("در حال دانلود…", "Downloading…"),
        ["setup.extracting"] = ("در حال نصب…", "Installing…"),
        ["setup.done"] = ("نصب شد", "Installed"),
        ["setup.failed"] = ("دانلود ناموفق بود", "Download failed"),

        ["overlay.listening"] = ("در حال شنیدن…", "Listening…"),
        ["overlay.hint"] = ("برای پایان دوباره میانبر را بزنید یا Esc = لغو", "Press the shortcut again to finish · Esc = cancel"),

        ["tray.hint"] = ("SayTo در سینی سیستم فعال ماند — دوباره اجرا کنید یا آیکون را دوبار کلیک کنید",
                         "SayTo keeps running in the tray — double-click the icon to reopen"),
        ["tray.open"] = ("باز کردن SayTo", "Open SayTo"),
        ["tray.exit"] = ("خروج", "Exit"),

        ["settings.title"] = ("تنظیمات", "Settings"),
        ["settings.hotkey"] = ("میانبر دیکته‌ی سراسری", "Global dictation shortcut"),
        ["settings.autostop"] = ("توقف خودکار در سکوت", "Auto-stop on silence"),
        ["settings.autostop.sec"] = ("ثانیه سکوت", "s of silence"),
        ["settings.insertafter"] = ("پس از دیکته‌ی سراسری، متن تایپ شود", "Type text after global dictation"),
        ["settings.models"] = ("پوشه‌ی مدل‌ها", "Models folder"),
        ["settings.models.section"] = ("مدل‌های گفتار", "Speech models"),
        ["model.use"] = ("فعال‌سازی این مدل", "Activate this model"),
        ["model.active"] = ("فعال", "Active"),
        ["model.installed"] = ("نصب‌شده", "Installed"),
        ["model.delete"] = ("حذف مدل", "Delete model"),
        ["model.delete.confirm"] = ("این مدل از سیستم حذف شود؟", "Delete this model from the system?"),
        ["msg.modelactive"] = ("مدل گفتار فعال شد", "Speech model activated"),
        ["msg.modeldeleted"] = ("مدل حذف شد", "Model deleted"),
        ["settings.about"] = ("دیکته‌ی آفلاین با Vosk — بدون ارسال هیچ صدایی به اینترنت",
                              "Offline dictation powered by Vosk — no audio ever leaves your device"),

        ["msg.copied"] = ("کپی شد", "Copied"),
        ["msg.inserted"] = ("متن تایپ شد", "Text typed"),
        ["msg.insertfailed"] = ("درج در برنامه ممکن نشد", "Could not insert into the target window"),
        ["msg.micerror"] = ("دسترسی به میکروفون ممکن نشد", "Could not access the microphone"),
        ["msg.modelerror"] = ("بارگذاری مدل ناموفق بود", "Failed to load the model"),
        ["msg.downloaderror"] = ("دانلود ناموفق بود — اتصال اینترنت را بررسی کنید",
                                 "Download failed — check your internet connection"),
        ["msg.firstinsert"] = ("پنجره‌ی برنامه‌ی مقصد را فعال کنید؛ متن با میانبر یا دکمه‌ی «درج» تایپ می‌شود",
                               "Focus any app, then dictate with the shortcut — text is typed at the caret"),
    };

    public static string Tr(string key)
    {
        if (!Strings.TryGetValue(key, out var pair)) return key;
        return IsFa ? pair.Fa : pair.En;
    }
}
