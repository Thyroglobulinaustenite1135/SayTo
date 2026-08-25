using System.Runtime.InteropServices;

namespace SayTo.Services;

/// <summary>Types text into a previously-captured foreground window via
/// unicode keyboard events (layout-independent, works for Persian).</summary>
public static class TextInjector
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const byte VK_MENU = 0x12;   // Alt — pressed/released to allow focus changes
    private const byte VK_RETURN = 0x0D;
    private const int SW_RESTORE = 9;
    private const int SW_MINIMIZE = 6;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public KEYBDINPUT ki;
        public ulong padding; // keep 64-bit alignment size correct
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    public static IntPtr CaptureForeground() => GetForegroundWindow();

    public static bool IsOwnWindow(IntPtr hwnd, IntPtr mainWindow, IntPtr overlayWindow)
        => hwnd == mainWindow || hwnd == overlayWindow || hwnd == IntPtr.Zero;

    public static string GetWindowTitle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "";
        var sb = new System.Text.StringBuilder(256);
        return GetWindowText(hwnd, sb, 256) > 0 ? sb.ToString() : "";
    }

    public static bool IsMinimized(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        GetWindowThreadProcessId(hwnd, out _);
        return IsIconic(hwnd);
    }

    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

    /// <summary>Brings the target window forward and types the text at its caret.</summary>
    public static bool TypeInto(IntPtr hwnd, string text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        try
        {
            if (hwnd != IntPtr.Zero)
            {
                if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

                // Alt tap lets SetForegroundWindow succeed while our window is focused
                keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                SetForegroundWindow(hwnd);
                Thread.Sleep(60);
            }

            var inputs = new List<INPUT>(text.Length * 2);
            foreach (var ch in text)
            {
                if (ch == '\r') continue;
                if (ch == '\n')
                {
                    inputs.Add(Key(VK_RETURN, down: true));
                    inputs.Add(Key(VK_RETURN, down: false));
                    continue;
                }
                if (ch > 0xFFFF) continue; // ignore surrogate pairs (rare in dictation output)
                ushort scan = ch;
                inputs.Add(new INPUT { type = INPUT_KEYBOARD, ki = Kbd(scan, KEYEVENTF_UNICODE) });
                inputs.Add(new INPUT { type = INPUT_KEYBOARD, ki = Kbd(scan, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP) });
            }

            const int chunkSize = 300;
            for (int i = 0; i < inputs.Count; i += chunkSize)
            {
                var chunk = inputs.Skip(i).Take(chunkSize).ToArray();
                var sent = SendInput((uint)chunk.Length, chunk, Marshal.SizeOf<INPUT>());
                if (sent != chunk.Length) return false;
                Thread.Sleep(1);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static KEYBDINPUT Kbd(ushort value, uint flags) => new()
    {
        wVk = 0,
        wScan = value,
        dwFlags = flags,
        time = 0,
        dwExtraInfo = UIntPtr.Zero,
    };

    private static INPUT Key(byte vk, bool down) => new()
    {
        type = INPUT_KEYBOARD,
        ki = new KEYBDINPUT
        {
            wVk = vk,
            wScan = 0,
            dwFlags = down ? 0 : KEYEVENTF_KEYUP,
            time = 0,
            dwExtraInfo = UIntPtr.Zero,
        },
    };
}
