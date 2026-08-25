using System.IO;
using System.Text.Json;
using Vosk;

namespace SayTo.Services;

/// <summary>Streaming wrapper around the Vosk recognizer for one active session.</summary>
public sealed class RecognitionEngine : IDisposable
{
    private Model? _model;
    private string? _loadedLang;
    private VoskRecognizer? _rec;

    /// <summary>Raised on the thread feeding audio.</summary>
    public event Action<string>? Partial;
    public event Action<string>? Final;

    public bool IsSessionActive => _rec != null;

    public bool IsModelLoaded(string lang) => _loadedLang == lang && _model != null;

    /// <summary>Loads (or switches) the acoustic model. Blocking — call from a worker thread.</summary>
    public void LoadModel(string lang)
    {
        if (IsModelLoaded(lang)) return;
        _rec?.Dispose(); _rec = null;
        _model?.Dispose();
        var path = ModelCatalog.ModelPath(lang);
        if (!ModelCatalog.IsInstalled(lang))
            throw new DirectoryNotFoundException($"Model not installed: {path}");
        _model = new Model(path);
        _loadedLang = lang;
    }

    public void StartSession(string lang)
    {
        LoadModel(lang);
        _rec?.Dispose();
        _rec = new VoskRecognizer(_model, 16000f);
        _rec.SetWords(true);
    }

    /// <summary>Feeds 16kHz/16-bit/mono PCM. Emits Final when an utterance completes.</summary>
    public bool Feed(byte[] buffer, int count)
    {
        if (_rec == null) return false;
        if (_rec.AcceptWaveform(buffer, count))
        {
            var text = ExtractText(_rec.FinalResult());
            if (!string.IsNullOrWhiteSpace(text)) Final?.Invoke(text);
            return true;
        }
        var partial = ExtractText(_rec.PartialResult());
        if (!string.IsNullOrWhiteSpace(partial)) Partial?.Invoke(partial);
        return false;
    }

    /// <summary>Ends the session and emits the remaining text.</summary>
    public void EndSession()
    {
        if (_rec == null) return;
        try
        {
            var text = ExtractText(_rec.FinalResult());
            if (!string.IsNullOrWhiteSpace(text)) Final?.Invoke(text);
        }
        finally
        {
            _rec.Dispose();
            _rec = null;
        }
    }

    private static string ExtractText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                return t.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("partial", out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString() ?? "";
        }
        catch { /* malformed json should never kill the session */ }
        return "";
    }

    public void Dispose()
    {
        _rec?.Dispose();
        _model?.Dispose();
        _rec = null;
        _model = null;
    }
}
