using System.Diagnostics;
using System.IO;
using NAudio.Wave;

namespace SayTo.Services;

/// <summary>Headless pipeline test: feeds a WAV file through Vosk and prints
/// the transcript. Usage: SayTo.exe --selftest file.wav [--lang en|fa]</summary>
public static class SelfTest
{
    public static int Run(string[] args)
    {
        AttachConsole();

        string wav = args[1];
        string lang = "en";
        for (int i = 2; i < args.Length - 1; i++)
            if (args[i].Equals("--lang", StringComparison.OrdinalIgnoreCase))
                lang = args[i + 1].ToLowerInvariant() == "fa" ? "fa" : "en";

        Log($"SayTo self-test  lang={lang}  file={wav}");
        if (!File.Exists(wav)) { Log("ERROR: file not found"); return 2; }

        try
        {
            if (!ModelCatalog.IsInstalled(lang))
            {
                Log("model missing — downloading…");
                var swDl = Stopwatch.StartNew();
                // run on the threadpool: the WPF dispatcher is not pumping here,
                // so awaiting with the UI context would deadlock
                Task.Run(() => ModelManager.Instance.EnsureDownloadedAsync(
                    ModelCatalog.Get(lang),
                    new Progress<DownloadProgress>(p =>
                        Log(p.Phase switch
                        {
                            "extract" => "extracting…",
                            "done" => "model installed",
                            _ => p.Percent >= 0 ? $"  {p.Percent:F0}%" : $"  {p.BytesReceived / 1024 / 1024} MB",
                        })),
                    CancellationToken.None)).GetAwaiter().GetResult();
                Log($"download finished in {swDl.Elapsed.TotalSeconds:F1}s");
            }

            var sw = Stopwatch.StartNew();
            using var engine = new RecognitionEngine();
            var model = ModelCatalog.Get(lang);
            engine.LoadModel(lang, model.Id);
            Log($"model loaded in {sw.Elapsed.TotalSeconds:F1}s  ({ModelCatalog.PathOf(model.Id)})");

            string finalText = "";
            string lastPartial = "";
            engine.Partial += t => lastPartial = t;
            engine.Final += t => { finalText = (finalText + " " + t).Trim(); Log($"final: {t}"); };

            engine.StartSession(lang, model.Id);
            sw.Restart();

            var pcm = ReadAs16kMono16(wav);
            var buffer = new byte[8000]; // 250ms chunks
            long fed = 0;
            for (int offset = 0; offset < pcm.Length; offset += buffer.Length)
            {
                int n = Math.Min(buffer.Length, pcm.Length - offset);
                Array.Copy(pcm, offset, buffer, 0, n);
                engine.Feed(buffer, n);
                fed += n;
            }
            engine.EndSession();

            double audioSec = fed / 32000.0; // 16k samples * 2 bytes
            Log($"audio {audioSec:F1}s processed in {sw.Elapsed.TotalSeconds:F1}s (RTF {sw.Elapsed.TotalSeconds / Math.Max(0.1, audioSec):F2})");
            Log($"last partial: {lastPartial}");
            Log($"RESULT: {finalText}");

            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "selftest_result.txt"),
                finalText + Environment.NewLine);

            return string.IsNullOrWhiteSpace(finalText) ? 1 : 0;
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            return 3;
        }
    }

    /// <summary>Decodes any WAV NAudio supports into 16kHz/16-bit/mono PCM
    /// (channel mix-down + linear resample, done manually to stay light).</summary>
    private static byte[] ReadAs16kMono16(string path)
    {
        using var reader = new WaveFileReader(path);
        var fmt = reader.WaveFormat;
        if (fmt.Channels != 1 || fmt.SampleRate != 16000 || fmt.BitsPerSample != 16)
            Log($"note: input is {fmt.SampleRate}Hz/{fmt.BitsPerSample}bit/{fmt.Channels}ch — converting");

        // read whole stream as floats (handles any WAV encoding NAudio supports)
        var sp = reader.ToSampleProvider();
        var samples = new float[reader.Length]; // upper bound (>= sample count)
        int total = 0;
        var buf = new float[reader.WaveFormat.SampleRate * fmt.Channels];
        int read;
        while ((read = sp.Read(buf)) > 0)
        {
            if (total + read > samples.Length) Array.Resize(ref samples, samples.Length * 2);
            Array.Copy(buf, 0, samples, total, read);
            total += read;
        }

        // linear resample to 16k mono (mix-down already done by sample provider)
        int outLen = (int)((long)total * 16000 / fmt.SampleRate);
        var pcm = new byte[Math.Max(1, outLen) * 2];
        for (int i = 0; i < outLen; i++)
        {
            var srcPos = i * (double)fmt.SampleRate / 16000.0;
            int i0 = (int)srcPos;
            float s = i0 + 1 < total
                ? (float)(samples[i0] * (1 - srcPos % 1) + samples[i0 + 1] * (srcPos % 1))
                : samples[i0];
            short v = (short)Math.Clamp(s * short.MaxValue, short.MinValue, short.MaxValue);
            pcm[i * 2] = (byte)(v & 0xFF);
            pcm[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return pcm;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int pid);
    private static void AttachConsole() => AttachConsole(-1);

    private static void Log(string msg) => Console.WriteLine(msg);
}
