using System.Text.Json;
using IoTHubUpdateUtility.Models;

namespace IoTHubUpdateUtility.Services;

/// <summary>
/// Loads and saves app configuration from the same folder as the running executable.
/// If the file is missing or corrupt, returns defaults without crashing.
/// </summary>
public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configPath;

    public ConfigService()
    {
        var appDir  = AppContext.BaseDirectory;
        _configPath = Path.Combine(appDir, "settings.json");
    }

    /// <summary>
    /// Loads config from disk. Returns a new default AppConfig if the file
    /// does not exist or cannot be parsed.
    /// </summary>
    public AppConfig Load(Action<string> log)
    {
        if (!File.Exists(_configPath))
        {
            log($"[CONFIG] configuration not found at {_configPath} — using defaults.");
            return new AppConfig();
        }

        try
        {
            var json   = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config is null)
            {
                log("[CONFIG] configuration deserialized as null — using defaults.");
                return new AppConfig();
            }
            log($"[CONFIG] Loaded configuration (version {config.Version}).");
            return config;
        }
        catch (Exception ex)
        {
            log($"[CONFIG] Failed to parse configuration: {ex.Message} — using defaults.");
            return new AppConfig();
        }
    }

    /// <summary>
    /// Saves the config to disk. Logs any failure without throwing.
    /// </summary>
    public void Save(AppConfig config, Action<string> log, string? savedSetting = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configPath, json);
            if (string.IsNullOrWhiteSpace(savedSetting))
                log("[CONFIG] Saved configuration.");
            else
                log($"[CONFIG] Saved configuration: {savedSetting}");
        }
        catch (Exception ex)
        {
            log($"[CONFIG] Failed to save configuration: {ex.Message}");
        }
    }
}
