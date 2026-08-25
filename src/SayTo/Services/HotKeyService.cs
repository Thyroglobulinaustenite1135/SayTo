using System.Runtime.InteropServices;

namespace SayTo.Services;

/// <summary>Registers a system-wide hotkey on a window handle.</summary>
public static class HotKeyService
{
    public const int WmHotKey = 0x0312;
    public const int HotKeyId = 0x5A17;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NONE = 0x0000;

    public static readonly string[] Presets = { "Ctrl+Alt+S", "Ctrl+Shift+D", "F9" };

    public static (uint Mods, uint Vk)? Parse(string preset)
    {
        uint mods = MOD_NONE;
        uint vk;
        var parts = (preset ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return null;
        var key = parts[^1].ToLowerInvariant();
        foreach (var p in parts.AsSpan(0, parts.Length - 1))
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= MOD_CONTROL; break;
                case "alt": mods |= MOD_ALT; break;
                case "shift": mods |= MOD_SHIFT; break;
                default: return null;
            }
        }
        vk = key switch
        {
            "s" => 0x53,
            "d" => 0x44,
            "f9" => 0x78,
            "space" or "spacebar" => 0x20,
            _ => 0,
        };
        if (vk == 0) return null;
        return (mods, vk);
    }

    public static bool Register(IntPtr hwnd, int id, string preset)
    {
        var parsed = Parse(preset);
        if (parsed == null) return false;
        UnregisterHotKey(hwnd, id);
        return RegisterHotKey(hwnd, id, parsed.Value.Mods, parsed.Value.Vk);
    }

    public static void Unregister(IntPtr hwnd, int id) => UnregisterHotKey(hwnd, id);
}
