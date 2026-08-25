using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace SayTo.Services;

public sealed class DownloadProgress
{
    public double Percent { get; init; }        // 0..100, -1 when total size unknown
    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }
    public string Phase { get; init; } = "";    // "" | "extract" | "done"
}

/// <summary>Downloads and extracts Vosk models with progress reporting.</summary>
public sealed class ModelManager
{
    public static readonly ModelManager Instance = new();

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(30),
    };

    static ModelManager()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("SayTo/1.0");
    }

    public async Task EnsureDownloadedAsync(SpeechModel model,
        IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        if (ModelCatalog.IsInstalled(model.Lang)) return;

        var baseDir = ModelCatalog.BaseDir;
        Directory.CreateDirectory(baseDir);

        var zipPath = Path.Combine(baseDir, model.Id + ".zip");
        var partPath = zipPath + ".part";
        var extractTmp = Path.Combine(baseDir, model.Id + ".tmp");

        try
        {
            using (var resp = await Http.GetAsync(model.Url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength;

                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    81920, useAsync: true);

                var buffer = new byte[81920];
                long received = 0;
                int read;
                var lastReport = Stopwatch.GetTimestamp();
                while ((read = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                    received += read;
                    // throttle UI updates to ~20/s
                    if (Stopwatch.GetTimestamp() - lastReport > 50_000) // ~50ms
                    {
                        lastReport = Stopwatch.GetTimestamp();
                        progress.Report(new DownloadProgress
                        {
                            Percent = total is > 0 ? received * 100.0 / total.Value : -1,
                            BytesReceived = received,
                            TotalBytes = total,
                        });
                    }
                }
            }

            progress.Report(new DownloadProgress { Percent = 100, Phase = "extract" });

            if (Directory.Exists(extractTmp)) Directory.Delete(extractTmp, true);
            ZipFile.ExtractToDirectory(partPath, extractTmp);

            // the zip contains a top folder named after the model
            var inner = Path.Combine(extractTmp, model.Id);
            var finalDir = Path.Combine(baseDir, model.Id);
            if (Directory.Exists(finalDir)) Directory.Delete(finalDir, true);
            if (Directory.Exists(inner))
                Directory.Move(inner, finalDir);
            else
                Directory.Move(extractTmp, finalDir);

            progress.Report(new DownloadProgress { Percent = 100, Phase = "done" });
        }
        finally
        {
            try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
            try { if (Directory.Exists(extractTmp)) Directory.Delete(extractTmp, true); } catch { }
        }
    }
}
