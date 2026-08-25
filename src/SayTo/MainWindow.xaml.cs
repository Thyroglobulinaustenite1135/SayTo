using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SayTo.Services;
using WinForms = System.Windows.Forms;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;
using Shape = System.Windows.Shapes.Shape;

namespace SayTo;

public partial class MainWindow : Window
{
    private readonly DictationController _controller = new();
    private DictationOverlay? _overlay;
    private IntPtr _overlayHwnd;
    private IntPtr _lastExternal;
    private HwndSource? _source;

    private string _committed = "";
    private volatile float _level;
    private readonly DispatcherTimer _levelTimer;
    private readonly List<Rectangle> _bars = new();
    private double _smoothed;
    private readonly DispatcherTimer _flashTimer;
    private Storyboard? _pulse;

    private WinForms.NotifyIcon? _tray;

    public MainWindow()
    {
        InitializeComponent();

        _levelTimer = new DispatcherTimer(DispatcherPriority.Render)
        { Interval = TimeSpan.FromMilliseconds(33) };
        _levelTimer.Tick += (_, _) => AnimateBars();

        _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
        _flashTimer.Tick += (_, _) => { _flashTimer.Stop(); UpdateStatusForState(); };

        _controller.Settings = App.Settings;
        _controller.Lang = App.Settings.RecognizeLanguage;
        _controller.AttachTextHandlers(OnPartial, OnFinalChunk);
        _controller.StateChanged += OnStateChanged;
        _controller.Level += l => _level = l;
        _controller.Completed += OnCompleted;
        _controller.Failed += code => FlashStatus(L10n.Tr(code), true);

        BuildBars();
        ApplyTexts();
        InitSegStates();
        InitSettingsPanel();
        InitTray();

        L10n.LanguageChanged += ApplyTexts;
        Closed += (_, _) => Cleanup();

        // first-run check
        if (!ModelCatalog.IsInstalled(_controller.Lang))
            ShowSetup();

        _ready = true;
    }

    // ================= window chrome / hotkey =================

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(WndProc);
        HotKeyService.Register(_source!.Handle, HotKeyService.HotKeyId, App.Settings.HotKey);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotKeyService.WmHotKey && wParam.ToInt32() == HotKeyService.HotKeyId)
        {
            ToggleGlobalDictation();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void TitleBar_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        if (!App.Settings.CloseToTrayHintShown && _tray != null)
        {
            _tray.ShowBalloonTip(2500, "SayTo", L10n.Tr("tray.hint"), WinForms.ToolTipIcon.Info);
            App.Settings.CloseToTrayHintShown = true;
            SettingsStore.Save(App.Settings);
        }
    }

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        var next = App.Settings.Theme == "dark" ? "light" : "dark";
        App.Settings.Theme = next;
        SettingsStore.Save(App.Settings);
        App.ApplyTheme(next);
        UpdateThemeIcon();
    }

    private void UpdateThemeIcon()
    {
        var dark = App.Settings.Theme != "light";
        IconSun.Visibility = dark ? Visibility.Collapsed : Visibility.Visible;
        IconMoon.Visibility = dark ? Visibility.Visible : Visibility.Collapsed;
    }

    // ================= dictation =================

    private async void MicButton_Click(object sender, RoutedEventArgs e)
    {
        if (_controller.State == DictationState.Idle)
            await StartDictation(DictationMode.InApp, IntPtr.Zero);
        else
            _controller.Stop();
    }

    private void ToggleGlobalDictation()
    {
        if (_controller.State != DictationState.Idle)
        {
            _controller.Stop();
            return;
        }

        var target = TextInjector.CaptureForeground();
        if (TextInjector.IsOwnWindow(target, _source?.Handle ?? IntPtr.Zero, _overlayHwnd))
        {
            _ = StartDictation(DictationMode.InApp, IntPtr.Zero);
            return;
        }

        _overlay ??= new DictationOverlay(this);
        _overlay.Prepare(target);
        _overlay.Show();
        _overlayHwnd = new WindowInteropHelper(_overlay).Handle;
        _ = StartDictation(DictationMode.Global, target);
    }

    private async Task StartDictation(DictationMode mode, IntPtr target)
    {
        if (!ModelCatalog.IsInstalled(_controller.Lang))
        {
            ShowSetup();
            return;
        }
        await _controller.StartAsync(mode, target);
    }

    internal void FinishDictation(bool cancelled) => _controller.Stop(cancelled);

    private void OnPartial(string text)
    {
        if (_controller.Mode == DictationMode.Global)
        {
            _overlay?.SetPartial(text);
        }
        else
        {
            SetTranscript((_committed + " " + text).Trim());
        }
    }

    private void OnFinalChunk(string text)
    {
        _committed = (_committed + " " + text).Trim();
        if (_controller.Mode != DictationMode.Global)
            SetTranscript(_committed);
    }

    private void OnStateChanged(DictationState state) => UpdateStatusForState();

    private void OnCompleted(DictationResult result)
    {
        _overlay?.HideOverlay();

        if (result.Cancelled)
        {
            FlashStatus(L10n.Tr("status.idle"), false);
        }
        else if (result.Mode == DictationMode.Global)
        {
            // keep a copy in the main window too
            if (result.Text.Length > 0)
                SetTranscript((_committed + " " + result.Text).Trim());

            if (result.Typed) FlashStatus(L10n.Tr("msg.inserted"), false);
            else if (result.Text.Length > 0) FlashStatus(L10n.Tr("msg.insertfailed"), true);
        }
        _committed = Transcript.Text.Trim();
    }

    private void UpdateStatusForState()
    {
        ActivateMicUi(_controller.State != DictationState.Idle);

        var (key, color) = _controller.State switch
        {
            DictationState.Listening => ("status.listening", "Brush.Accent"),
            DictationState.Starting => ("status.loading", "Brush.Accent2"),
            _ => ("status.idle", "Brush.Good"),
        };
        StatusText.Text = L10n.Tr(key);
        StatusDot.SetResourceReference(Shape.FillProperty, color);

        HintText.Text = string.Format(
            L10n.IsFa ? "میانبر سراسری: {0} — در هر برنامه‌ای امتحان کنید" : "Global shortcut: {0} — try it in any app",
            App.Settings.HotKey);
        MicTip.Content = _controller.State == DictationState.Idle
            ? L10n.Tr("status.idle")
            : L10n.Tr("btn.stop.hint");
    }

    private void ActivateMicUi(bool listening)
    {
        MicIcon.Visibility = listening ? Visibility.Collapsed : Visibility.Visible;
        StopIcon.Visibility = listening ? Visibility.Visible : Visibility.Collapsed;

        if (listening)
        {
            if (_pulse == null) BuildPulse();
            _pulse!.Begin(this, true);
            _levelTimer.Start();
        }
        else
        {
            _pulse?.Remove(this);
            PulseRing.Opacity = 0;
            _levelTimer.Stop();
            _level = 0;
        }
    }

    private void BuildPulse()
    {
        var sx = new DoubleAnimation(1, 1.34, TimeSpan.FromSeconds(1.15))
        { RepeatBehavior = RepeatBehavior.Forever };
        var sy = new DoubleAnimation(1, 1.34, TimeSpan.FromSeconds(1.15))
        { RepeatBehavior = RepeatBehavior.Forever };
        var op = new DoubleAnimation(0.55, 0, TimeSpan.FromSeconds(1.15))
        { RepeatBehavior = RepeatBehavior.Forever };

        Storyboard.SetTarget(sx, PulseRing);
        Storyboard.SetTargetProperty(sx, new PropertyPath("(0).(1)",
            Ellipse.RenderTransformProperty, ScaleTransform.ScaleXProperty));
        Storyboard.SetTarget(sy, PulseRing);
        Storyboard.SetTargetProperty(sy, new PropertyPath("(0).(1)",
            Ellipse.RenderTransformProperty, ScaleTransform.ScaleYProperty));
        Storyboard.SetTarget(op, PulseRing);
        Storyboard.SetTargetProperty(op, new PropertyPath(Ellipse.OpacityProperty));

        _pulse = new Storyboard();
        _pulse.Children.Add(sx);
        _pulse.Children.Add(sy);
        _pulse.Children.Add(op);
    }

    // ================= waveform bars =================

    private void BuildBars()
    {
        double[] env = { 0.45, 0.75, 1.0, 0.7 };
        foreach (var panel in new[] { BarsL, BarsR })
        {
            var e = env;
            if (panel == BarsR) e = env.Reverse().ToArray();
            foreach (var v in e)
            {
                var r = new Rectangle
                {
                    Width = 4,
                    RadiusX = 2,
                    RadiusY = 2,
                    Opacity = 0.35 + 0.65 * v,
                    Height = 4,
                    Margin = new Thickness(2.5, 0, 2.5, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = v,
                };
                r.SetResourceReference(Shape.FillProperty, "Brush.Accent");
                _bars.Add(r);
                panel.Children.Add(r);
            }
        }
    }

    private void AnimateBars()
    {
        _smoothed += (_level - _smoothed) * 0.35;
        if (_smoothed < 0.001) _smoothed = 0;
        foreach (var bar in _bars)
        {
            var env = (double)bar.Tag;
            var h = 4 + _smoothed * 26 * env;
            bar.BeginAnimation(HeightProperty, null);
            bar.Height = h;
        }
    }

    // ================= transcript & actions =================

    private void SetTranscript(string text)
    {
        Transcript.Text = text;
        Transcript.CaretIndex = Transcript.Text.Length;
        Transcript.ScrollToEnd();
    }

    private void Transcript_TextChanged(object sender, TextChangedEventArgs e) =>
        Placeholder.Visibility = Transcript.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (Transcript.Text.Length == 0) return;
        try { Clipboard.SetText(Transcript.Text); FlashStatus(L10n.Tr("msg.copied"), false); } catch { }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _committed = "";
        SetTranscript("");
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        var fg = TextInjector.CaptureForeground();
        if (!TextInjector.IsOwnWindow(fg, _source?.Handle ?? IntPtr.Zero, _overlayHwnd))
            _lastExternal = fg;
    }

    private void BtnInsert_Click(object sender, RoutedEventArgs e)
    {
        if (Transcript.Text.Length == 0) return;
        if (_lastExternal == IntPtr.Zero)
        {
            FlashStatus(L10n.Tr("msg.firstinsert"), false);
            return;
        }
        Hide();
        var ok = TextInjector.TypeInto(_lastExternal, Transcript.Text);
        FlashStatus(L10n.Tr(ok ? "msg.inserted" : "msg.insertfailed"), !ok);
        Show();
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == System.Windows.Input.Key.Escape && _controller.State != DictationState.Idle)
        {
            _controller.Stop();
            e.Handled = true;
        }
    }

    private void FlashStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusDot.SetResourceReference(Shape.FillProperty, isError ? "Brush.Danger" : "Brush.Good");
        _flashTimer.Stop();
        _flashTimer.Start();
    }

    // ================= recognition language =================

    private void InitSegStates()
    {
        SegFa.IsChecked = _controller.Lang != "en";
        SegEn.IsChecked = _controller.Lang == "en";
        UpdateThemeIcon();
    }

    private void SegLang_Changed(object sender, RoutedEventArgs e)
    {
        if (SegFa.IsChecked != true && SegEn.IsChecked != true) return;
        var lang = SegEn.IsChecked == true ? "en" : "fa";
        if (lang == _controller.Lang) return;

        if (_controller.State != DictationState.Idle) _controller.Stop();
        _controller.Lang = lang;
        App.Settings.RecognizeLanguage = lang;
        SettingsStore.Save(App.Settings);
        UpdateStatusForState();

        if (!ModelCatalog.IsInstalled(lang)) ShowSetup(); else HideSetup();
    }

    // ================= model setup overlay =================

    private void ShowSetup()
    {
        SetupFaName.Text = ModelCatalog.Fa.DisplayName;
        SetupEnName.Text = ModelCatalog.En.DisplayName;
        SetupFaSize.Text = $"{ModelCatalog.Fa.ApproxBytes / 1024 / 1024} MB · Vosk";
        SetupEnSize.Text = $"{ModelCatalog.En.ApproxBytes / 1024 / 1024} MB · Vosk";
        RefreshSetupRow("fa");
        RefreshSetupRow("en");
        SetupOverlay.Visibility = Visibility.Visible;
    }

    private void HideSetup() => SetupOverlay.Visibility = Visibility.Collapsed;

    private void RefreshSetupRow(string lang)
    {
        var installed = ModelCatalog.IsInstalled(lang);
        var prog = lang == "fa" ? ProgFa : ProgEn;
        var state = lang == "fa" ? StateFa : StateEn;
        var btn = lang == "fa" ? BtnDlFa : BtnDlEn;

        prog.Visibility = Visibility.Collapsed;
        btn.IsEnabled = !installed;
        if (installed)
        {
            state.Text = L10n.Tr("setup.done");
            state.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Good");
        }
        else
        {
            state.Text = "";
        }
    }

    private void BtnLater_Click(object sender, RoutedEventArgs e) => HideSetup();

    private void BtnDlFa_Click(object sender, RoutedEventArgs e) => DownloadWithUi("fa");
    private void BtnDlEn_Click(object sender, RoutedEventArgs e) => DownloadWithUi("en");

    private async void BtnDlAll_Click(object sender, RoutedEventArgs e)
    {
        if (!ModelCatalog.IsInstalled("fa")) await DownloadModel("fa");
        if (!ModelCatalog.IsInstalled("en")) await DownloadModel("en");
    }

    private void DownloadWithUi(string lang) => _ = DownloadModel(lang);

    private async Task DownloadModel(string lang)
    {
        var prog = lang == "fa" ? ProgFa : ProgEn;
        var state = lang == "fa" ? StateFa : StateEn;
        var btn = lang == "fa" ? BtnDlFa : BtnDlEn;

        btn.IsEnabled = false;
        prog.Value = 0;
        prog.Visibility = Visibility.Visible;
        state.Text = L10n.Tr("setup.downloading");
        state.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextDim");

        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                if (p.Phase == "extract")
                {
                    prog.IsIndeterminate = true;
                    state.Text = L10n.Tr("setup.extracting");
                }
                else if (p.Phase == "done")
                {
                    prog.IsIndeterminate = false;
                    prog.Value = 100;
                }
                else if (p.Percent >= 0)
                {
                    prog.IsIndeterminate = false;
                    prog.Value = Math.Min(100, p.Percent);
                }
            });
            await ModelManager.Instance.EnsureDownloadedAsync(ModelCatalog.Get(lang), progress, CancellationToken.None);

            state.Text = L10n.Tr("setup.done");
            state.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Good");
            UpdateStatusForState();
        }
        catch (Exception)
        {
            state.Text = L10n.Tr("setup.failed");
            state.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Danger");
        }
        finally
        {
            prog.Visibility = Visibility.Collapsed;
            prog.IsIndeterminate = false;
            btn.IsEnabled = !ModelCatalog.IsInstalled(lang);
        }

        if (ModelCatalog.IsInstalled("fa") && ModelCatalog.IsInstalled("en"))
            HideSetup();
    }

    // ================= settings popup =================

    private void InitSettingsPanel()
    {
        for (int i = 0; i < HotKeyService.Presets.Length; i++)
        {
            var rb = i switch { 0 => Hk0, 1 => Hk1, _ => Hk2 };
            rb.Content = HotKeyService.Presets[i];
            rb.IsChecked = HotKeyService.Presets[i] == App.Settings.HotKey;
        }
        ChkAutoStop.IsChecked = App.Settings.AutoStopOnSilence;
        SlAutoStop.Value = App.Settings.AutoStopSeconds;
        UpdateAutoStopRow();
        ChkInsertAfter.IsChecked = App.Settings.InsertAfterGlobalDictation;

        Ux1.Content = "فارسی";
        Ux2.Content = "English";
        (App.Settings.UiLanguage == "fa" ? Ux1 : Ux2).IsChecked = true;
    }

    private void UpdateAutoStopRow() =>
        AutoStopRow.Visibility = App.Settings.AutoStopOnSilence ? Visibility.Visible : Visibility.Collapsed;

    private void BtnSettings_Click(object sender, RoutedEventArgs e) => SettingsPopup.IsOpen = true;

    private void HotKey_Changed(object sender, RoutedEventArgs e)
    {
        var checkedPreset = new[] { Hk0, Hk1, Hk2 }.FirstOrDefault(r => r.IsChecked == true)?.Content as string;
        if (checkedPreset == null) return;
        App.Settings.HotKey = checkedPreset;
        SettingsStore.Save(App.Settings);
        if (_source != null)
            HotKeyService.Register(_source.Handle, HotKeyService.HotKeyId, checkedPreset);
        UpdateStatusForState();
    }

    private void AutoStop_Changed(object sender, RoutedEventArgs e)
    {
        App.Settings.AutoStopOnSilence = ChkAutoStop.IsChecked == true;
        SettingsStore.Save(App.Settings);
        UpdateAutoStopRow();
    }

    private void AutoStopSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // fires during XAML parse (min-clamp) before settings are loaded — ignore
        if (!_ready || SlAutoStop == null) return;
        App.Settings.AutoStopSeconds = SlAutoStop.Value;
        if (LblAutoStopSec != null)
            LblAutoStopSec.Text = $"{SlAutoStop.Value:0.#} " + L10n.Tr("settings.autostop.sec");
        SettingsStore.Save(App.Settings);
    }

    private void InsertAfter_Changed(object sender, RoutedEventArgs e)
    {
        App.Settings.InsertAfterGlobalDictation = ChkInsertAfter.IsChecked == true;
        SettingsStore.Save(App.Settings);
    }

    private void UiLang_Changed(object sender, RoutedEventArgs e)
    {
        var lang = Ux2.IsChecked == true ? "en" : "fa";
        if (lang == App.Settings.UiLanguage) return;
        App.Settings.UiLanguage = lang;
        SettingsStore.Save(App.Settings);
        L10n.SetLanguage(lang);
    }

    private void BtnModels_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ModelCatalog.BaseDir);
            System.Diagnostics.Process.Start("explorer.exe", ModelCatalog.BaseDir);
        }
        catch { }
    }

    // ================= localization =================

    private void ApplyTexts()
    {
        Title = L10n.Tr("app.title");
        Placeholder.Text = L10n.Tr("placeholder");
        TxtCopy.Text = L10n.Tr("btn.copy");
        TxtClear.Text = L10n.Tr("btn.clear");
        TxtInsert.Text = L10n.Tr("btn.insert");
        ToolTips();

        SetupTitle.Text = L10n.Tr("setup.title");
        SetupDesc.Text = L10n.Tr("setup.desc");
        BtnLater.Content = L10n.Tr("setup.later");
        BtnDlAll.Content = L10n.Tr("setup.downloadall");

        SettingsTitle.Text = L10n.Tr("settings.title");
        LblHotkey.Text = L10n.Tr("settings.hotkey");
        ChkAutoStop.Content = L10n.Tr("settings.autostop");
        LblAutoStopSec.Text = $"{SlAutoStop?.Value:0.#} " + L10n.Tr("settings.autostop.sec");
        ChkInsertAfter.Content = L10n.Tr("settings.insertafter");
        LblUiLang.Text = UiLangLabel();
        TxtModels.Text = L10n.Tr("settings.models");
        TxtAbout.Text = L10n.Tr("settings.about");

        RefreshSetupRow("fa");
        RefreshSetupRow("en");
        UpdateStatusForState();
    }

    private static string UiLangLabel() =>
        L10n.IsFa ? "زبان رابط کاربری" : "Interface language";

    private void ToolTips()
    {
        BtnCopy.ToolTip = L10n.Tr("btn.copy.hint");
        BtnClear.ToolTip = L10n.Tr("btn.clear.hint");
        BtnInsert.ToolTip = L10n.Tr("btn.insert.hint");
    }

    // ================= tray =================

    private void InitTray()
    {
        try
        {
            var iconStream = Application.GetResourceStream(
                new Uri("pack://application:,,,/SayTo;component/Assets/sayto.ico"))!.Stream;
            _tray = new WinForms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(iconStream),
                Text = "SayTo",
                Visible = true,
            };
            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add(L10n.Tr("tray.open"), null, (_, _) => RestoreFromTray());
            menu.Items.Add("-");
            menu.Items.Add(L10n.Tr("tray.exit"), null, (_, _) => ExitApp());
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (_, _) => RestoreFromTray();
        }
        catch { /* tray is optional */ }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _closingForExit = true;
        Close();
    }

    private bool _closingForExit;
    private bool _ready;

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_closingForExit && _tray != null)
        {
            e.Cancel = true;
            BtnClose_Click(this, new RoutedEventArgs());
            return;
        }
        base.OnClosing(e);
    }

    private void Cleanup()
    {
        HotKeyService.Unregister(_source?.Handle ?? IntPtr.Zero, HotKeyService.HotKeyId);
        _controller.Stop(true);
        _controller.Dispose();
        _overlay?.ForceClose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        L10n.LanguageChanged -= ApplyTexts;
    }
}
