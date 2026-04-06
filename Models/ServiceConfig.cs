namespace IoTHubUpdateUtility.Models;

/// <summary>
/// Linux service commands edited by the user and persisted in settings.json.
/// </summary>
public class ServiceConfig
{
    /// <summary>Command(s) to stop services before update. List of commands.</summary>
    public List<string> StopServices { get; set; } = new();

    /// <summary>Command(s) to start services after update. List of commands.</summary>
    public List<string> StartServices { get; set; } = new();

    /// <summary>
    /// Whether to run stop/execute/start sequence automatically.
    /// Default: true (stop is enabled by default per spec).
    /// </summary>
    public bool StopBeforeUpdate { get; set; } = true;

    /// <summary>
    /// Whether to start services after the update completes.
    /// Default: false (manual verification before restart per spec).
    /// </summary>
    public bool StartAfterUpdate { get; set; } = false;

    /// <summary>Returns stop commands.</summary>
    public IEnumerable<string> GetStopCommandLines() => StopServices;

    /// <summary>Returns start commands.</summary>
    public IEnumerable<string> GetStartCommandLines() => StartServices;

    // Backward compatibility: if old string properties are used, convert to lists
    private string _stopCommands = string.Empty;
    public string StopCommands
    {
        get => string.Join('\n', StopServices);
        set
        {
            _stopCommands = value;
            StopServices = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
    }

    private string _startCommands = string.Empty;
    public string StartCommands
    {
        get => string.Join('\n', StartServices);
        set
        {
            _startCommands = value;
            StartServices = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
    }
}
