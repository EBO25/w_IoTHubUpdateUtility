using SharpCompress.Archives;
using SharpCompress.Common;
using IoTHubUpdateUtility.Models;

namespace IoTHubUpdateUtility.Services;

/// <summary>
/// Detects the source type and extracts compressed archives to a temp folder.
/// Supported: .zip, .tar, .tar.gz, .tgz, .7z via SharpCompress.
/// .rar requires the system 'unrar' binary.
/// </summary>
public class ArchiveService
{
    private static readonly string[] ArchiveExtensions =
        { ".zip", ".tar", ".gz", ".tgz", ".7z", ".rar" };

    /// <summary>
    /// Inspects the given path and returns a SourceInfo with Type set correctly.
    /// Does not extract yet.
    /// </summary>
    public SourceInfo Identify(string path)
    {
        var info = new SourceInfo { SelectedPath = path };

        if (Directory.Exists(path))
        {
            info.Type         = SourceType.Folder;
            info.ExtractedPath = path;
            return info;
        }

        if (File.Exists(path))
        {
            var ext = GetEffectiveExtension(path);
            if (ArchiveExtensions.Contains(ext))
            {
                info.Type = SourceType.CompressedFile;
                return info;
            }
        }

        throw new ArgumentException($"Path is not a recognised file or folder: {path}");
    }

    /// <summary>
    /// Extracts the archive to a deterministic temp folder in the specified base path.
    /// If the temp folder already exists it is deleted first.
    /// </summary>
    public async Task<string> ExtractAsync(SourceInfo source, string extractionBasePath, Action<string> log,
        CancellationToken ct = default)
    {
        if (!source.IsArchive)
            throw new InvalidOperationException("Source is not an archive — nothing to extract.");

        var archiveName = Path.GetFileNameWithoutExtension(source.SelectedPath);
        // Strip double extension for .tar.gz
        if (archiveName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            archiveName = Path.GetFileNameWithoutExtension(archiveName);

        var tempRoot = Path.Combine(extractionBasePath, $"_temp_{archiveName}");

        if (Directory.Exists(tempRoot))
        {
            log($"[EXTRACT] Temp folder exists, removing: {tempRoot}");
            Directory.Delete(tempRoot, recursive: true);
        }

        Directory.CreateDirectory(tempRoot);
        log($"[EXTRACT] Extracting to {tempRoot} ...");

        var ext = GetEffectiveExtension(source.SelectedPath);

        if (ext == ".rar")
            await ExtractRarAsync(source.SelectedPath, tempRoot, log, ct);
        else
            await ExtractWithSharpCompressAsync(source.SelectedPath, tempRoot, log, ct);

        log($"[EXTRACT] Done.");
        source.ExtractedPath = tempRoot;
        return tempRoot;
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private static async Task ExtractWithSharpCompressAsync(
        string archivePath, string destDir, Action<string> log, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            using var archive = ArchiveFactory.Open(archivePath);
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                ct.ThrowIfCancellationRequested();
                log($"[EXTRACT]   {entry.Key}");
                entry.WriteToDirectory(destDir, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite       = true
                });
            }
        }, ct);
    }

    private static async Task ExtractRarAsync(
        string archivePath, string destDir, Action<string> log, CancellationToken ct)
    {
        log("[EXTRACT] RAR detected — invoking system 'unrar' binary.");

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName               = "unrar",
            Arguments              = $"x -y \"{archivePath}\" \"{destDir}/\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false
        };

        using var proc = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start unrar process.");

        // Stream output lines to the log
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) log($"[UNRAR] {e.Data}"); };
        proc.BeginOutputReadLine();

        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"unrar exited with code {proc.ExitCode}.");
    }

    /// <summary>
    /// Returns the "effective" extension, collapsing .tar.gz → .gz, .tar.bz2 → .bz2 etc.
    /// </summary>
    private static string GetEffectiveExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".gz" or ".bz2" or ".xz")
        {
            var withoutInner = Path.GetFileNameWithoutExtension(path);
            if (Path.GetExtension(withoutInner).Equals(".tar", StringComparison.OrdinalIgnoreCase))
                return ".tar"; // treat all .tar.* as .tar
        }
        return ext;
    }
}
