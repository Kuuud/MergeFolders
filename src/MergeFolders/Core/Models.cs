namespace MergeFolders.Core;

public enum FileConflictAction
{
    Overwrite,
    Skip,
    Rename,
    Ask
}

public enum DirectoryConflictAction
{
    Merge,
    Skip,
    Rename,
    Ask
}

public enum CopyMode
{
    Copy,
    Move
}

public enum VerificationMode
{
    None,
    Size,
    Sha256
}

public sealed class MergeOptions
{
    public FileConflictAction FileConflict { get; set; } = FileConflictAction.Ask;
    public DirectoryConflictAction DirectoryConflict { get; set; } = DirectoryConflictAction.Merge;
    public CopyMode Mode { get; set; } = CopyMode.Copy;
    public VerificationMode Verification { get; set; } = VerificationMode.None;
    public bool PreserveTimestamp { get; set; } = true;
    public bool PreserveAttributes { get; set; } = false;
    public bool DeleteEmptySourceDirectories { get; set; } = false;
    public bool UseFirstSourceAsDestination { get; set; } = false;
}

public sealed class MergeStats
{
    public long FilesCopied { get; set; }
    public long FilesSkipped { get; set; }
    public long FilesOverwritten { get; set; }
    public long FilesRenamed { get; set; }
    public long FilesFailed { get; set; }
    public long BytesCopied { get; set; }
    public long DirectoriesMerged { get; set; }
    public long DirectoriesRenamed { get; set; }
    public List<string> Errors { get; } = new();
}

public sealed record MergeProgress(
    string Source,
    string Destination,
    long CurrentBytes,
    long TotalBytes,
    string Message);

public sealed record ConflictContext(
    bool IsDirectory,
    string SourcePath,
    string DestinationPath);

public enum ConflictDecision
{
    Overwrite,
    Skip,
    Rename,
    Cancel
}
