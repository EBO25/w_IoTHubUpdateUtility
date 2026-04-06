namespace IoTHubUpdateUtility.Models;

/// <summary>
/// Root object that maps directly to settings.json.
/// Everything the user configures is stored here.
/// </summary>
public class AppConfig
{
    /// <summary>Schema version — increment when adding breaking fields.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Type of source: compressed file or folder.</summary>
    public SourceType SourceType { get; set; } = SourceType.CompressedFile;

    /// <summary>Path for extracting compressed files.</summary>
    public string ExtractionPath { get; set; } = "/tmp";

    /// <summary>Default permission command text shown in the rule editor.</summary>
    public string Permissions { get; set; } = "chmod 777";

    public BackupPlan  Backup   { get; set; } = new();
    public ServiceConfig Services { get; set; } = new();

    /// <summary>Last source path the user selected (UI convenience).</summary>
    public string LastSourcePath      { get; set; } = string.Empty;

    /// <summary>Last destination path the user selected (UI convenience).</summary>
    public string LastDestinationPath { get; set; } = "/home/pi";

    /// <summary>Saved rule list. Survives app restarts.</summary>
    public List<UpdateRule> Rules     { get; set; } = new();
}
