namespace IoTHubUpdateUtility.Models;

/// <summary>
/// Controls whether and how existing files are backed up before the update.
/// </summary>
public class BackupPlan
{
    /// <summary>Whether backup is enabled. Stored in settings.json.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Path for backups. A timestamped sub-folder is created per run.
    /// Default: /home/pi/_backup_ddmmyyyy_hhmmss/
    /// </summary>
    public string Path { get; set; } = "/home/pi";

    /// <summary>
    /// Builds the actual backup path for the current run using a timestamp.
    /// </summary>
    public string BuildTimestampedPath()
    {
        var stamp = DateTime.Now.ToString("ddMMyyy_HHmmss");
        return System.IO.Path.Combine(Path, $"_backup_{stamp}");
    }
}
