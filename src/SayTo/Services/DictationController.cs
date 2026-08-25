using System.Diagnostics;
using System.Text;

namespace SayTo.Services;

public enum DictationMode { InApp, Global }
public enum DictationState { Idle, Starting, Listening }

public sealed class DictationResult
{
    public bool Cancelled { get; init; }
    public bool Typed { get; init; }
    public string Text { get; init; } = "";
    public IntPtr Target { get; init; }
    public DictationMode Mode { get; init; }
}

/// <summary>Orchestrates model + microphone + recognizer for one dictation
/// session, in-app or against an external target window.</summary>
public sealed class DictationController : IDisposable
{
    private readonly RecognitionEngine _engine = new();
    private readonly AudioCapture _audio = new();
    private SynchronizationContext? _ui;
    private readonly Stopwatch _silence = new();
    private readonly StringBuilder _session = new();
    private bool _hasSpoken;
    private double _autoStopSeconds;

    public AppSettings Settings { get; set; } = new();
    public string Lang { get; set; } = "fa";
    public DictationState State { get; private set; } = DictationState.Idle;
    public DictationMode Mode { get; private set; } = DictationMode.InApp;
    public IntPtr GlobalTarget { get; private set; }

    public event Action<DictationState>? StateChanged;
    public event Action<string>? Partial;
    public event Action<float>? Level;
    public event Action<DictationResult>? Completed;
    public event Action<string>? Failed;   // error code from L10n keys

    public bool IsModelReady(string lang) => _engine.IsModelLoaded(lang);

    public string ActiveModelId() =>
        Lang == "en" ? Settings.ActiveEnModel : Settings.ActiveFaModel;

    public async Task StartAsync(DictationMode mode, IntPtr target)
    {
        if (State != DictationState.Idle) return;
        _ui = SynchronizationContext.Current;
        Mode = mode;
        GlobalTarget = target;
        SetState(DictationState.Starting);
        _session.Clear();
        _hasSpoken = false;
        _autoStopSeconds = Settings.AutoStopOnSilence ? Settings.AutoStopSeconds : 0;

        // prefer the user's active model, fall back to the compact one
        var modelId = ModelCatalog.ResolveId(Lang, ActiveModelId());

        try
        {
            await Task.Run(() =>
            {
                _engine.LoadModel(Lang, modelId);
                _engine.StartSession(Lang, modelId);
            });
        }
        catch (Exception)
        {
            SetState(DictationState.Idle);
            Failed?.Invoke("msg.modelerror");
            return;
        }

        if (!AudioCapture.MicrophoneAvailable)
        {
            SetState(DictationState.Idle);
            Failed?.Invoke("status.nomic");
            return;
        }

        _audio.Data += OnAudio;
        _audio.Error += OnAudioError;
        try
        {
            _audio.Start();
        }
        catch
        {
            DetachAudio();
            SetState(DictationState.Idle);
            Failed?.Invoke("msg.micerror");
            return;
        }
        _silence.Restart();
        SetState(DictationState.Listening);
    }

    public void Stop(bool cancelled = false)
    {
        if (State == DictationState.Idle) return;
        DetachAudio();
        _audio.Stop();
        _engine.EndSession();

        var text = _session.ToString().Trim();
        SetState(DictationState.Idle);

        bool typed = false;
        if (!cancelled && text.Length > 0 &&
            Mode == DictationMode.Global && Settings.InsertAfterGlobalDictation &&
            GlobalTarget != IntPtr.Zero)
        {
            typed = TextInjector.TypeInto(GlobalTarget, text);
        }
        Completed?.Invoke(new DictationResult
        {
            Cancelled = cancelled,
            Typed = typed,
            Text = text,
            Target = GlobalTarget,
            Mode = Mode,
        });
    }

    private void OnAudio(byte[] buffer, int count, float level)
    {
        _engine.Feed(buffer, count);

        var ui = _ui;
        ui?.Post(_ => Level?.Invoke(level), null);

        if (level > 0.08)
        {
            _hasSpoken = true;
            _silence.Restart();
        }

        if (_autoStopSeconds > 0 && _hasSpoken && State == DictationState.Listening
            && _silence.Elapsed.TotalSeconds >= _autoStopSeconds)
        {
            _autoStopSeconds = 0; // fire once
            ui?.Post(_ => Stop(), null);
        }
    }

    private void OnAudioError(string message)
    {
        DetachAudio();
        _ui?.Post(_ =>
        {
            if (State == DictationState.Idle) return;
            SetState(DictationState.Idle);
            Failed?.Invoke("msg.micerror");
        }, null);
    }

    private void DetachAudio()
    {
        _audio.Data -= OnAudio;
        _audio.Error -= OnAudioError;
    }

    /// <summary>Wire engine text events (called once at startup).</summary>
    public void AttachTextHandlers(Action<string> partial, Action<string> final)
    {
        _engine.Partial += t => _ui?.Post(_ => partial(t), null);
        _engine.Final += t =>
        {
            _session.Append(t).Append(' ');
            _ui?.Post(_ => final(t), null);
        };
    }

    private void SetState(DictationState s)
    {
        State = s;
        _ui?.Post(_ => StateChanged?.Invoke(s), null);
    }

    public void Dispose()
    {
        _audio.Dispose();
        _engine.Dispose();
    }
}
