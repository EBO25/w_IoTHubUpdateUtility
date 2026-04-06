namespace IoTHubUpdateUtility.Models;

/// <summary>
/// The result of a dry-run validation. Describes exactly what will happen
/// when Execute is called. No filesystem writes occur during plan creation.
/// </summary>
public class UpgradePlan
{
    public bool IsValid { get; set; } = true;

    /// <summary>Human-readable issues found during validation (errors or warnings).</summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>Actions that will be performed in order during execution.</summary>
    public List<PlannedAction> Actions { get; set; } = new();

    public int FilesCopied    => Actions.Count(a => a.Kind == ActionKind.CopyFile);
    public int DirsCreated    => Actions.Count(a => a.Kind == ActionKind.CreateDirectory);
    public int PermsChanged   => Actions.Count(a => a.Kind == ActionKind.SetPermissions);
}

public class PlannedAction
{
    public ActionKind Kind        { get; set; }
    public string SourcePath      { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string Permissions     { get; set; } = string.Empty;

    public override string ToString() => Kind switch
    {
        ActionKind.CreateDirectory => $"MKDIR  {DestinationPath}",
        ActionKind.CopyFile        => $"COPY   {SourcePath}  →  {DestinationPath}",
        ActionKind.SetPermissions  => $"CHMOD  {Permissions}  {DestinationPath}",
        _                          => $"{Kind}  {DestinationPath}"
    };
}

public enum ActionKind
{
    CreateDirectory,
    CopyFile,
    SetPermissions
}
