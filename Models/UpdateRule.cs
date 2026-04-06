using System.Text.Json.Serialization;

namespace IoTHubUpdateUtility.Models;

/// <summary>
/// A single upgrade rule that describes how to move content from source to destination.
/// Rules are first-class objects: they can be added, removed, and serialised to config.
/// </summary>
public class UpdateRule
{
    /// <summary>Display name shown in the rules list.</summary>
    public string Name { get; set; } = "New rule";

    /// <summary>
    /// Path relative to the extracted source root.
    /// E.g. "app/bin" means operate on _temp_xxx/app/bin/.
    /// Leave empty to mean the root of the extracted source.
    /// </summary>
    public string SourceRelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Path relative to the destination root (e.g. /home/pi/).
    /// Leave empty to mean the destination root itself.
    /// </summary>
    public string DestinationRelativePath { get; set; } = string.Empty;

    /// <summary>What this rule does when executed.</summary>
    public OperationType Operation { get; set; } = OperationType.RecursiveCopy;

    /// <summary>
    /// Permission text captured from the rule editor, e.g. "chmod 777".
    /// The executor normalises this before calling chmod.
    /// </summary>
    public string Permissions { get; set; } = string.Empty;

    public string GetEffectivePermissions() =>
        Operation == OperationType.FilesOnly && !string.IsNullOrWhiteSpace(Permissions)
            ? Permissions
            : string.Empty;

    public override string ToString() =>
        $"{Name}  [{Operation}]  {SourceRelativePath} → {DestinationRelativePath}";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationType
{
    /// <summary>Copy folder and all sub-folders/files recursively.</summary>
    RecursiveCopy,

    /// <summary>Copy only the files directly inside the source folder (no sub-folders).</summary>
    FilesOnly,

    /// <summary>Replace a single file and optionally set permissions.</summary>
    ReplaceFile
}
