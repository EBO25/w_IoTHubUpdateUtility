using IoTHubUpdateUtility.Models;
using IoTHubUpdateUtility.Services;

namespace IoTHubUpdateUtility.App;

/// <summary>
/// Single orchestrator the UI layer talks to.
/// Sequences service calls; owns no business logic itself.
/// </summary>
public class AppService
{
    private readonly ArchiveService    _archive    = new();
    private readonly ConfigService     _config     = new();
    private readonly ValidationService _validation = new();
    private readonly UpdateService     _update     = new();

    // Loaded once at startup, kept in memory, saved on every user edit.
    public AppConfig Config { get; private set; } = new();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void LoadConfig(Action<string> log)
    {
        Config = _config.Load(log);
    }

    public void SaveConfig(Action<string> log, string? savedSetting = null)
    {
        _config.Save(Config, log, savedSetting);
    }

    // ── Source handling ───────────────────────────────────────────────────────

    /// <summary>
    /// Inspects the path chosen by the user and returns a SourceInfo.
    /// Does not extract yet.
    /// </summary>
    public SourceInfo IdentifySource(string path, Action<string> log)
    {
        log($"[SOURCE] Identifying: {path}");
        var source = _archive.Identify(path);
        log($"[SOURCE] Type: {source.Type}");
        return source;
    }

    /// <summary>
    /// Extracts the archive (if needed) and returns the path to the
    /// extracted/source folder. Updates source.ExtractedPath in-place.
    /// </summary>
    public async Task<string> ExtractAsync(
        SourceInfo source, Action<string> log, CancellationToken ct = default)
    {
        if (!source.IsArchive)
        {
            log("[SOURCE] Source is a folder — no extraction needed.");
            return source.ExtractedPath;
        }

        return await _archive.ExtractAsync(source, Config.ExtractionPath, log, ct);
    }

    // ── Validation (dry-run) ─────────────────────────────────────────────────

    /// <summary>
    /// Runs a complete dry-run and returns the plan. No files are written.
    /// </summary>
    public UpgradePlan CheckUpgradeCriteria(
        SourceInfo source, string destinationRoot, Action<string> log)
    {
        return _validation.Validate(source, destinationRoot, Config.Rules, log);
    }

    // ── Execution ────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates, then executes the upgrade if the plan is valid.
    /// Returns false if validation found errors that block execution.
    /// </summary>
    public async Task<bool> ExecuteUpgradeAsync(
        SourceInfo source, string destinationRoot,
        Action<string> log, CancellationToken ct = default)
    {
        var plan = _validation.Validate(source, destinationRoot, Config.Rules, log);

        if (!plan.IsValid)
        {
            log("[EXEC] Plan has errors — aborting. Fix the issues and try again.");
            return false;
        }

        await _update.ExecuteAsync(plan, Config.Backup, Config.Services, log, ct);
        return true;
    }

    // ── Service commands ─────────────────────────────────────────────────────

    public async Task StopServicesAsync(Action<string> log, CancellationToken ct = default)
    {
        await RunCommandListAsync(Config.Services.GetStopCommandLines(), "STOP", log, ct);
    }

    public async Task StartServicesAsync(Action<string> log, CancellationToken ct = default)
    {
        await RunCommandListAsync(Config.Services.GetStartCommandLines(), "START", log, ct);
    }

    private static async Task RunCommandListAsync(
        IEnumerable<string> commands, string tag,
        Action<string> log, CancellationToken ct)
    {
        foreach (var cmd in commands)
        {
            ct.ThrowIfCancellationRequested();
            log($"[{tag}] {cmd}");
            var parts = cmd.Split(' ', 2, StringSplitOptions.TrimEntries);
            var psi   = new System.Diagnostics.ProcessStartInfo
            {
                FileName              = parts[0],
                Arguments             = parts.Length > 1 ? parts[1] : "",
                RedirectStandardOutput = true,
                UseShellExecute       = false
            };
            try
            {
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc is not null) await proc.WaitForExitAsync(ct);
            }
            catch (Exception ex) { log($"[{tag}] ERROR: {ex.Message}"); }
        }
    }
}
