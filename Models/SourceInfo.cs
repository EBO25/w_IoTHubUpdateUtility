namespace IoTHubUpdateUtility.Models;

/// <summary>
/// Represents the input source the user has selected.
/// </summary>
public class SourceInfo
{
    /// <summary>Full path to the file or folder the user selected.</summary>
    public string SelectedPath { get; set; } = string.Empty;

    /// <summary>Whether the source is a compressed archive or a plain folder.</summary>
    public SourceType Type { get; set; } = SourceType.Folder;

    /// <summary>
    /// After extraction (or immediately, if type is Folder) this is the root
    /// directory that rules operate against.
    /// </summary>
    public string ExtractedPath { get; set; } = string.Empty;

    public bool IsArchive => Type == SourceType.CompressedFile;
}

public enum SourceType
{
    CompressedFile,
    Folder
}
