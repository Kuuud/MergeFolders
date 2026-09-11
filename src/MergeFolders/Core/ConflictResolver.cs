namespace MergeFolders.Core;

public interface IConflictResolver
{
    Task<ConflictDecision> ResolveAsync(ConflictContext context, CancellationToken cancellationToken);
}

public sealed class FixedConflictResolver : IConflictResolver
{
    private readonly FileConflictAction _fileAction;
    private readonly DirectoryConflictAction _directoryAction;

    public FixedConflictResolver(FileConflictAction fileAction, DirectoryConflictAction directoryAction)
    {
        _fileAction = fileAction;
        _directoryAction = directoryAction;
    }

    public Task<ConflictDecision> ResolveAsync(ConflictContext context, CancellationToken cancellationToken)
    {
        ConflictDecision result;
        if (context.IsDirectory)
        {
            result = _directoryAction switch
            {
                DirectoryConflictAction.Merge => ConflictDecision.Overwrite,
                DirectoryConflictAction.Skip => ConflictDecision.Skip,
                DirectoryConflictAction.Rename => ConflictDecision.Rename,
                _ => ConflictDecision.Cancel
            };
        }
        else
        {
            result = _fileAction switch
            {
                FileConflictAction.Overwrite => ConflictDecision.Overwrite,
                FileConflictAction.Skip => ConflictDecision.Skip,
                FileConflictAction.Rename => ConflictDecision.Rename,
                _ => ConflictDecision.Cancel
            };
        }
        return Task.FromResult(result);
    }
}
