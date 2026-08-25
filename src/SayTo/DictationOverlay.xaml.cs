using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SayTo.Services;

namespace SayTo;

/// <summary>Compact always-on-top bar shown during global dictation.
/// Never takes focus away from the target app.</summary>
public partial class DictationOverlay : Window
{
    private readonly MainWindow _owner;
    private IntPtr _target;

    public DictationOverlay(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        SourceInitialized += (_, _) =>
        {
            // keep the overlay from ever becoming the foreground window
            var hwnd = new WindowInteropHelper(this).Handle;
            Interop.SetWindowExNoActivate(hwnd);
        };
    }

    public void Prepare(IntPtr target)
    {
        _target = target;
        PartialText.Text = "";
        var title = TextInjector.GetWindowTitle(target);
        LblTarget.Text = title.Length > 42 ? title[..42] + "…" : title;
        LblListening.Text = L10n.Tr("overlay.listening");
        BtnStop.ToolTip = L10n.Tr("btn.stop.hint");
        PositionNearBottom();
        StartDotAnimation();
    }

    public void SetPartial(string text) => PartialText.Text = text;

    public void HideOverlay()
    {
        StopDotAnimation();
        Hide();
    }

    public void ForceClose() => Close();

    private void BtnStop_Click(object sender, RoutedEventArgs e) => _owner.FinishDictation(false);
    private void BtnCancel_Click(object sender, RoutedEventArgs e) => _owner.FinishDictation(true);

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            _owner.FinishDictation(true);
            e.Handled = true;
        }
    }

    private void PositionNearBottom()
    {
        var wa = System.Windows.Forms.Screen.FromHandle(_target != IntPtr.Zero
            ? _target
            : new WindowInteropHelper(this).Handle).WorkingArea;

        // window size not finalized yet; use fixed estimate then re-center on Loaded
        Left = wa.Left + (wa.Width - 540) / 2.0;
        Top = wa.Bottom - 170;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        var wa = System.Windows.Forms.Screen.FromHandle(_target).WorkingArea;
        Left = wa.Left + (wa.Width - ActualWidth) / 2.0;
        Top = wa.Bottom - ActualHeight - 28;
    }

    private void StartDotAnimation()
    {
        var anim = new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(650))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        LiveDot.BeginAnimation(OpacityProperty, anim);
    }

    private void StopDotAnimation()
    {
        LiveDot.BeginAnimation(OpacityProperty, null);
        LiveDot.Opacity = 1;
    }
}

/// <summary>Small Win32 helpers for focus-friendly overlay windows.</summary>
internal static class Interop
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public static void SetWindowExNoActivate(IntPtr hwnd)
    {
        // NOACTIVATE keeps the dictation target focused while the bar is visible;
        // the stop/cancel buttons still work because WPF routes the mouse clicks.
        var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE);
    }
}
