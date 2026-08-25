using System.Windows;
using SayTo.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace SayTo;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();
    public static MainWindow? MainWin { get; private set; }

    private static Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        // self-test / CLI mode: SayTo.exe --selftest file.wav [--lang en|fa]
        if (e.Args.Length >= 2 && e.Args[0].Equals("--selftest", StringComparison.OrdinalIgnoreCase))
        {
            Shutdown(SelfTest.Run(e.Args));
            return;
        }

        _singleInstance = new Mutex(true, @"Local\SayTo.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show(L10n.Tr("tray.hint"), "SayTo", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        Settings = SettingsStore.Load();
        L10n.SetLanguage(Settings.UiLanguage);
        ApplyTheme(Settings.Theme);

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(ex.Exception.Message, "SayTo — " + L10n.Tr("status.error"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        // window is created here (no StartupUri) so CLI mode can exit cleanly
        var win = new MainWindow();
        MainWindow = win;
        win.Show();

        base.OnStartup(e);
        MainWin = win;
    }

    public static void ApplyTheme(string theme)
    {
        var uri = new Uri(theme == "light"
            ? "Themes/Palette.Light.xaml"
            : "Themes/Palette.Dark.xaml", UriKind.Relative);
        Current.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = uri };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SettingsStore.Save(Settings);
        _singleInstance?.ReleaseMutex();
        base.OnExit(e);
    }
}
