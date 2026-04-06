using IoTHubUpdateUtility.Models;

namespace IoTHubUpdateUtility.Services;

/// <summary>
/// Executes an UpgradePlan produced by ValidationService.
/// Sequence: stop services → backup → apply actions → start services.
/// </summary>
public class UpdateService
{
    /// <summary>
    /// Full upgrade execution. Logs every step. Does not throw on individual
    /// file failures — logs them and continues, then reports at the end.
    /// </summary>
    public async Task ExecuteAsync(
        UpgradePlan    plan,
        BackupPlan     backup,
        ServiceConfig  services,
        Action<string> log,
        CancellationToken ct = default)
    {
        var errors = new List<string>();

        // 1. Stop services
        if (services.StopBeforeUpdate)
            await RunCommandsAsync(services.GetStopCommandLines(), "STOP", log, ct);

        // 2. Backup
        if (backup.Enabled)
            await BackupAsync(plan, backup, log, ct);

        // 3. Execute plan actions
        log("[UPDATE] Applying rules...");
        foreach (var action in plan.Actions)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                ApplyAction(action, log);
            }
            catch (Exception ex)
            {
                var msg = $"[UPDATE] ERROR on {action}: {ex.Message}";
                log(msg);
                errors.Add(msg);
            }
        }

        // 4. Start services
        if (services.StartAfterUpdate)
            await RunCommandsAsync(services.GetStartCommandLines(), "START", log, ct);

        // 5. Summary
        if (errors.Count == 0)
            log("[UPDATE] ✓ Upgrade completed successfully.");
        else
        {
            log($"[UPDATE] Upgrade finished with {errors.Count} error(s):");
            foreach (var e in errors) log($"  {e}");
        }
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private static void ApplyAction(PlannedAction action, Action<string> log)
    {
        switch (action.Kind)
        {
            case ActionKind.CreateDirectory:
                if (!Directory.Exists(action.DestinationPath))
                {
                    Directory.CreateDirectory(action.DestinationPath);
                    log($"[UPDATE] MKDIR  {action.DestinationPath}");
                }
                break;

            case ActionKind.CopyFile:
                var dir = Path.GetDirectoryName(action.DestinationPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                File.Copy(action.SourcePath, action.DestinationPath, overwrite: true);
                log($"[UPDATE] COPY   {action.SourcePath}  →  {action.DestinationPath}");
                break;

            case ActionKind.SetPermissions:
                var permissionArgument = ExtractPermissionArgument(action.Permissions);
                if (string.IsNullOrWhiteSpace(permissionArgument))
                    break;

                // chmod via system call — works on Linux/macOS
                var chmod = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = "chmod",
                    Arguments              = $"{permissionArgument} \"{action.DestinationPath}\"",
                    RedirectStandardError  = true,
                    UseShellExecute        = false
                });
                chmod?.WaitForExit();
                log($"[UPDATE] CHMOD  {action.Permissions}  {action.DestinationPath}");
                break;
        }
    }

    private static async Task BackupAsync(
        UpgradePlan plan, BackupPlan backup, Action<string> log, CancellationToken ct)
    {
        var backupPath = backup.BuildTimestampedPath();
        log($"[BACKUP] Creating backup at {backupPath} ...");
        Directory.CreateDirectory(backupPath);

        foreach (var action in plan.Actions.Where(a => a.Kind == ActionKind.CopyFile))
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(action.DestinationPath)) continue;

            // Replicate the destination folder structure inside the backup folder
            var relative = action.DestinationPath.TrimStart('/');
            var dest     = Path.Combine(backupPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            try
            {
                File.Copy(action.DestinationPath, dest, overwrite: true);
                log($"[BACKUP]   {action.DestinationPath}  →  {dest}");
            }
            catch (Exception ex)
            {
                log($"[BACKUP]   WARN: Could not back up {action.DestinationPath}: {ex.Message}");
            }
        }

        log("[BACKUP] Done.");
        await Task.CompletedTask;
    }

    private static string ExtractPermissionArgument(string permissionText)
    {
        if (string.IsNullOrWhiteSpace(permissionText))
            return string.Empty;

        var trimmed = permissionText.Trim();
        return trimmed.StartsWith("chmod ", StringComparison.OrdinalIgnoreCase)
            ? trimmed["chmod ".Length..].Trim()
            : trimmed;
    }

    private static async Task RunCommandsAsync(
        IEnumerable<string> commands, string tag, Action<string> log, CancellationToken ct)
    {
        foreach (var cmd in commands)
        {
            ct.ThrowIfCancellationRequested();
            log($"[{tag}] Running: {cmd}");

            var parts = cmd.Split(' ', 2, StringSplitOptions.TrimEntries);
            var psi   = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = parts[0],
                Arguments              = parts.Length > 1 ? parts[1] : "",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false
            };

            try
            {
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc is null) { log($"[{tag}] WARN: Could not start process for: {cmd}"); continue; }

                proc.OutputDataReceived += (_, e) => { if (e.Data is not null) log($"[{tag}]   {e.Data}"); };
                proc.BeginOutputReadLine();

                await proc.WaitForExitAsync(ct);

                if (proc.ExitCode != 0)
                    log($"[{tag}] WARN: Command exited with code {proc.ExitCode}: {cmd}");
                else
                    log($"[{tag}] OK: {cmd}");
            }
            catch (Exception ex)
            {
                log($"[{tag}] ERROR running '{cmd}': {ex.Message}");
            }
        }
    }
}
