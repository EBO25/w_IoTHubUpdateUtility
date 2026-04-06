using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using IoTHubUpdateUtility.App;
using IoTHubUpdateUtility.Models;

namespace IoTHubUpdateUtility.UI;

/// <summary>
/// All state the MainWindow binds to.
/// Implements INotifyPropertyChanged the simple way — no framework dependency.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly AppService _app = new();
    private readonly Assembly _assembly = typeof(MainViewModel).Assembly;

    public string AppTitle => "IoT Hub Update Utility";
    public string AppVersion => GetAppVersion();
    public string AppReleaseDate => GetReleaseDate();
    public string AppDescription =>
        "IoT Hub Update Utility helps operators validate update packages, preview upgrade actions, and apply file and service changes to Linux-based IoT deployments from one desktop workflow.";

    // ── Observable properties ─────────────────────────────────────────────────

    private string _sourcePath = string.Empty;
    public string SourcePath
    {
        get => _sourcePath;
        set { _sourcePath = value; OnPropertyChanged(); }
    }

    private string _destinationPath = "/home/pi/";
    public string DestinationPath
    {
        get => _destinationPath;
        set { _destinationPath = value; OnPropertyChanged(); }
    }

    private SourceType _sourceType = SourceType.CompressedFile;
    public SourceType SourceType
    {
        get => _sourceType;
        set
        {
            _sourceType = value;
            _app.Config.SourceType = value;
            _app.SaveConfig(Log, $"SourceType={value}");
            OnPropertyChanged();
        }
    }

    private bool _backupEnabled = true;
    public bool BackupEnabled
    {
        get => _backupEnabled;
        set
        {
            _backupEnabled = value;
            _app.Config.Backup.Enabled = value;
            OnPropertyChanged();
        }
    }

    private string _backupPath = "/home/pi";
    public string BackupPath
    {
        get => _backupPath;
        set
        {
            _backupPath = value;
            _app.Config.Backup.Path = value;
            _app.SaveConfig(Log, $"Backup.Path={value}");
            OnPropertyChanged();
        }
    }

    private string _extractionPath = "/tmp";
    public string ExtractionPath
    {
        get => _extractionPath;
        set
        {
            _extractionPath = value;
            _app.Config.ExtractionPath = value;
            _app.SaveConfig(Log, $"ExtractionPath={value}");
            OnPropertyChanged();
        }
    }

    private string _permissions = "chmod 777";
    public string Permissions
    {
        get => _permissions;
        set
        {
            _permissions = value;
            _app.Config.Permissions = value;
            OnPropertyChanged();
        }
    }

    // Services as collections
    public ObservableCollection<string> StopServices { get; } = new();
    public ObservableCollection<string> StartServices { get; } = new();

    // Rules list — the UI ListBox binds to this
    public ObservableCollection<UpdateRule> Rules { get; } = new();

    // Source and destination tree nodes
    public ObservableCollection<FileNode> SourceTree      { get; } = new();
    public ObservableCollection<FileNode> DestinationTree { get; } = new();

    // Log lines bound to the terminal TextBox
    public ObservableCollection<LogEntry> LogLines { get; } = new();

    // ── Internal state ────────────────────────────────────────────────────────

    private SourceInfo? _currentSource;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public MainViewModel()
    {
        _app.LoadConfig(Log);
        ApplyConfigToViewModel();
        ValidateConfiguredPaths();
    }

    private void ApplyConfigToViewModel()
    {
        SourceType      = _app.Config.SourceType;
        BackupEnabled   = _app.Config.Backup.Enabled;
        BackupPath      = _app.Config.Backup.Path;
        ExtractionPath  = _app.Config.ExtractionPath;
        Permissions     = _app.Config.Permissions;
        DestinationPath = _app.Config.LastDestinationPath;
        SourcePath      = _app.Config.LastSourcePath;

        StopServices.Clear();
        foreach (var cmd in _app.Config.Services.StopServices) StopServices.Add(cmd);

        StartServices.Clear();
        foreach (var cmd in _app.Config.Services.StartServices) StartServices.Add(cmd);

        Rules.Clear();
        foreach (var r in _app.Config.Rules) Rules.Add(r);
    }

    // ── Commands called by MainWindow ─────────────────────────────────────────

    public void SetSource(string path)
    {
        try
        {
            _currentSource         = _app.IdentifySource(path, Log);
            SourcePath             = path;
            _app.Config.LastSourcePath = path;
            _app.SaveConfig(Log, $"LastSourcePath={path}");
            RefreshSourceTree();
        }
        catch (Exception ex) { Log($"[ERROR] {ex.Message}"); }
    }

    public async Task ExtractAsync()
    {
        if (_currentSource is null) { Log("[ERROR] No source selected."); return; }
        try   { await _app.ExtractAsync(_currentSource, Log); RefreshSourceTree(); }
        catch (Exception ex) { Log($"[ERROR] Extraction failed: {ex.Message}"); }
    }

    public void SetDestination(string path)
    {
        DestinationPath = path;
        _app.Config.LastDestinationPath = path;
        _app.SaveConfig(Log, $"LastDestinationPath={path}");
        RefreshDestinationTree();
    }

    public void AddRule()
    {
        var rule = new UpdateRule { Name = $"Rule {Rules.Count + 1}" };
        Rules.Add(rule);
        _app.Config.Rules.Add(rule);
        RefreshRulesList();
        _app.SaveConfig(Log, $"Rules.Add={rule.Name}");
    }

    public void RemoveRule(UpdateRule? rule)
    {
        if (rule is null) return;
        Rules.Remove(rule);
        _app.Config.Rules.Remove(rule);
        RefreshRulesList();
        _app.SaveConfig(Log, $"Rules.Remove={rule.Name}");
    }

    public UpdateRule CreateRuleDraft()
    {
        return new UpdateRule { Name = $"Rule {Rules.Count + 1}" };
    }

    public UpdateRule? FindRuleByName(string ruleName, UpdateRule? excludeRule = null)
    {
        return Rules.FirstOrDefault(rule =>
            !ReferenceEquals(rule, excludeRule) &&
            string.Equals(rule.Name, ruleName, StringComparison.OrdinalIgnoreCase));
    }

    public void UpsertRule(UpdateRule draftRule, UpdateRule? selectedRule, UpdateRule? overwrittenRule = null)
    {
        if (overwrittenRule is not null && !ReferenceEquals(overwrittenRule, selectedRule))
        {
            overwrittenRule.Name = draftRule.Name;
            overwrittenRule.SourceRelativePath = draftRule.SourceRelativePath;
            overwrittenRule.DestinationRelativePath = draftRule.DestinationRelativePath;
            overwrittenRule.Permissions = draftRule.Permissions;
            overwrittenRule.Operation = draftRule.Operation;

            if (selectedRule is not null)
            {
                Rules.Remove(selectedRule);
                _app.Config.Rules.Remove(selectedRule);
            }

            RefreshRulesList();
            _app.SaveConfig(Log, $"Rules.Overwrite={draftRule.Name}");
            return;
        }

        if (selectedRule is null)
        {
            Rules.Add(draftRule);
            _app.Config.Rules.Add(draftRule);
            RefreshRulesList();
            _app.SaveConfig(Log, $"Rules.Add={draftRule.Name}");
            return;
        }

        selectedRule.Name = draftRule.Name;
        selectedRule.SourceRelativePath = draftRule.SourceRelativePath;
        selectedRule.DestinationRelativePath = draftRule.DestinationRelativePath;
        selectedRule.Permissions = draftRule.Permissions;
        selectedRule.Operation = draftRule.Operation;
        RefreshRulesList();
        _app.SaveConfig(Log, $"Rules.Update={draftRule.Name}");
    }

    public void CheckUpgradeCriteria()
    {
        if (_currentSource is null) { Log("[ERROR] No source selected."); return; }
        var plan = _app.CheckUpgradeCriteria(_currentSource, DestinationPath, Log);
        Log(plan.IsValid
            ? $"[CHECK] Plan is valid. {plan.FilesCopied} files, {plan.DirsCreated} dirs."
            : "[CHECK] Plan has errors — see above.");
    }

    public async Task StopServicesAndExecuteAsync()
    {
        if (_currentSource is null) { Log("[ERROR] No source selected."); return; }
        try   { await _app.ExecuteUpgradeAsync(_currentSource, DestinationPath, Log); }
        catch (Exception ex) { Log($"[ERROR] {ex.Message}"); }
    }

    public async Task StartServicesAsync()
    {
        try   { await _app.StartServicesAsync(Log); }
        catch (Exception ex) { Log($"[ERROR] {ex.Message}"); }
    }

    public void ClearLogs()
    {
        LogLines.Clear();
    }

    public void RefreshFolderTrees()
    {
        ValidateSourcePath();
        ValidateDirectoryPath(DestinationPath, nameof(DestinationPath), RefreshDestinationTree);
    }

    public void SaveCommands()
    {
        _app.Config.Services.StopServices  = StopServices.ToList();
        _app.Config.Services.StartServices = StartServices.ToList();
        _app.SaveConfig(Log, "Services");
    }

    public void AddStopService(string command)
    {
        if (!string.IsNullOrWhiteSpace(command))
        {
            StopServices.Add(command);
            SaveCommands();
        }
    }

    public void RemoveStopService(string command)
    {
        StopServices.Remove(command);
        SaveCommands();
    }

    public void AddStartService(string command)
    {
        if (!string.IsNullOrWhiteSpace(command))
        {
            StartServices.Add(command);
            SaveCommands();
        }
    }

    public void RemoveStartService(string command)
    {
        StartServices.Remove(command);
        SaveCommands();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public void Log(string message)
    {
        // Must always run on UI thread — MainWindow wires this via Dispatcher
        LogLines.Add(new LogEntry
        {
            Message = $"[{DateTime.Now:HH:mm:ss}] {message}",
            Foreground = GetLogBrush(message)
        });
    }

    private void RefreshSourceTree()
    {
        SourceTree.Clear();

        if (_currentSource is null)
            return;

        if (_currentSource.IsArchive && File.Exists(_currentSource.SelectedPath))
        {
            var sourceRoot = CreateRootNodeFromPath(_currentSource.SelectedPath);
            sourceRoot.Children.Add(new FileNode
            {
                Name = Path.GetFileName(_currentSource.SelectedPath)
            });
            SourceTree.Add(sourceRoot);
            return;
        }

        var root = _currentSource.ExtractedPath;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return;

        var rootNode = CreateRootNodeFromPath(_currentSource.SelectedPath);
        rootNode.Children = BuildTree(root, string.Empty);
        SourceTree.Add(rootNode);
    }

    private void RefreshDestinationTree()
    {
        DestinationTree.Clear();
        if (!Directory.Exists(DestinationPath)) return;

        var rootNode = CreateRootNodeFromPath(DestinationPath);
        rootNode.Children = BuildTree(DestinationPath, string.Empty);
        DestinationTree.Add(rootNode);
    }

    private static List<FileNode> BuildTree(string dir, string parentRelativePath)
    {
        var nodes = new List<FileNode>();
        try
        {
            foreach (var sub in Directory.GetDirectories(dir))
            {
                var folderName = Path.GetFileName(sub);
                var relativePath = CombineRelativePath(parentRelativePath, folderName);
                nodes.Add(new FileNode
                {
                    Name     = folderName,
                    IsFolder = true,
                    RelativePath = relativePath,
                    FullPath = sub,
                    Children = BuildTree(sub, relativePath)
                });
            }

            foreach (var file in Directory.GetFiles(dir))
            {
                var fileName = Path.GetFileName(file);
                nodes.Add(new FileNode
                {
                    Name = fileName,
                    RelativePath = CombineRelativePath(parentRelativePath, fileName),
                    FullPath = file
                });
            }
        }
        catch { /* permission denied etc — skip silently */ }
        return nodes;
    }

    private static FileNode CreateRootNodeFromPath(string path)
    {
        var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (Directory.Exists(normalizedPath))
        {
            return new FileNode
            {
                Name = GetLastFolderName(normalizedPath),
                IsFolder = true,
                RelativePath = string.Empty,
                FullPath = normalizedPath
            };
        }

        var parentDirectory = Path.GetDirectoryName(normalizedPath);
        return new FileNode
        {
            Name = GetLastFolderName(parentDirectory),
            IsFolder = true,
            RelativePath = string.Empty,
            FullPath = parentDirectory ?? string.Empty
        };
    }

    private static string CombineRelativePath(string parentRelativePath, string name)
    {
        return string.IsNullOrWhiteSpace(parentRelativePath)
            ? name
            : $"{parentRelativePath}/{name}";
    }

    private static string GetLastFolderName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folderName = Path.GetFileName(normalizedPath);

        return string.IsNullOrWhiteSpace(folderName) ? normalizedPath : folderName;
    }

    private void RefreshRulesList()
    {
        var snapshot = Rules.ToList();
        Rules.Clear();

        foreach (var rule in snapshot)
            Rules.Add(rule);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void ValidateConfiguredPaths()
    {
        ValidateSourcePath();
        ValidateDirectoryPath(DestinationPath, nameof(DestinationPath), RefreshDestinationTree);
        ValidateDirectoryPath(BackupPath, nameof(BackupPath));
        ValidateDirectoryPath(ExtractionPath, nameof(ExtractionPath));
    }

    private void ValidateSourcePath()
    {
        SourceTree.Clear();

        if (string.IsNullOrWhiteSpace(SourcePath))
            return;

        try
        {
            _currentSource = _app.IdentifySource(SourcePath, _ => { });
            Log($"[CONFIG] path found: {SourcePath}");
            RefreshSourceTree();
        }
        catch
        {
            _currentSource = null;
            Log($"[ERROR] path not found: {SourcePath}");
        }
    }

    private void ValidateDirectoryPath(string path, string propertyName, Action? onSuccess = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (Directory.Exists(path))
        {
            Log($"[CONFIG] path found: {path}");
            onSuccess?.Invoke();
            return;
        }

        if (propertyName == nameof(DestinationPath))
            DestinationTree.Clear();

        Log($"[ERROR] path not found: {path}");
    }

    private string GetAppVersion()
    {
        var informationalVersion = _assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion;

        return _assembly.GetName().Version?.ToString() ?? "Unknown";
    }

    private string GetReleaseDate()
    {
        var releaseDate = _assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == "ReleaseDate")?
            .Value;

        return string.IsNullOrWhiteSpace(releaseDate) ? "Unknown" : releaseDate;
    }

    private static IBrush GetLogBrush(string message)
    {
        if (message.StartsWith("[ERROR]", StringComparison.Ordinal))
            return Brush.Parse("#f44747");

        if (message.StartsWith("[CONFIG]", StringComparison.Ordinal))
            return Brush.Parse("#6a9955");

        return Brush.Parse("#cccccc");
    }
}

/// <summary>Simple node for the tree views.</summary>
public class FileNode
{
    public string        Name     { get; set; } = string.Empty;
    public bool          IsFolder { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public List<FileNode> Children { get; set; } = new();
    public string Icon   => IsFolder ? "📁" : "📄";
}

public class LogEntry
{
    public string Message { get; set; } = string.Empty;
    public IBrush Foreground { get; set; } = Brush.Parse("#cccccc");
}
