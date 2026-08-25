using NAudio.Wave;

namespace SayTo.Services;

/// <summary>Captures 16 kHz / 16-bit / mono PCM from the default microphone
/// and reports normalized loudness (0..1) alongside each buffer.</summary>
public sealed class AudioCapture : IDisposable
{
    private WaveIn? _wi;
    private bool _disposed;

    /// <summary>Raised on a threadpool thread: (pcm bytes, valid count, level 0..1).</summary>
    public event Action<byte[], int, float>? Data;
    public event Action<string>? Error;

    public static bool MicrophoneAvailable
    {
        get { try { return WaveIn.DeviceCount > 0; } catch { return false; } }
    }

    public void Start()
    {
        Stop();
        _wi = new WaveIn
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 60,
            NumberOfBuffers = 3,
        };
        _wi.DataAvailable += OnData;
        _wi.RecordingStopped += (_, e) => { if (e.Exception != null) Error?.Invoke(e.Exception.Message); };
        _wi.StartRecording();
    }

    public void Stop()
    {
        if (_wi == null) return;
        try { _wi.StopRecording(); } catch { }
        try { _wi.Dispose(); } catch { }
        _wi = null;
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        float level = ComputeLevel(e.Buffer, e.BytesRecorded);
        Data?.Invoke(e.Buffer, e.BytesRecorded, level);
    }

    private static float ComputeLevel(byte[] buffer, int count)
    {
        if (count <= 0) return 0;
        double sum = 0;
        int samples = count / 2;
        for (int i = 0; i < samples; i++)
        {
            short s = BitConverter.ToInt16(buffer, i * 2);
            sum += (double)(s * s);
        }
        var rms = Math.Sqrt(sum / Math.Max(1, samples)) / 32768.0;
        // perceptual-ish scaling: quiet speech still shows visible motion
        var scaled = Math.Pow(rms, 0.45) * 2.2;
        return (float)Math.Clamp(scaled, 0.0, 1.0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
