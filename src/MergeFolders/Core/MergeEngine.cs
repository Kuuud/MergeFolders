using System.Security.Cryptography;

namespace MergeFolders.Core;

public sealed class MergeEngine
{
    private readonly IConflictResolver _resolver;

    public MergeEngine(IConflictResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<MergeStats> MergeAsync(
        IReadOnlyList<string> sources,
        string destination,
        MergeOptions options,
        IProgress<MergeProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (sources.Count == 0)
            throw new ArgumentException("至少需要一个源文件夹。", nameof(sources));

        destination = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var sourceRoot in sources)
        {
            var sourceFull = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(sourceFull, destination, StringComparison.OrdinalIgnoreCase) ||
                destination.StartsWith(sourceFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"目标文件夹不能位于源文件夹内部：{sourceRoot}");
        }

        Directory.CreateDirectory(destination);
        var stats = new MergeStats();
        var totalBytes = await CalculateTotalBytesAsync(sources, cancellationToken);
        long currentBytes = 0;

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(source))
                continue;

            currentBytes = await MergeDirectoryContentsAsync(source, destination, options, stats, totalBytes,
                currentBytes, progress, cancellationToken);

            if (options.Mode == CopyMode.Move && options.DeleteEmptySourceDirectories)
            {
                TryDeleteEmptyDirectory(source, stats);
            }
        }

        return stats;
    }

    private async Task<long> MergeDirectoryContentsAsync(
        string sourceDirectory,
        string destinationDirectory,
        MergeOptions options,
        MergeStats stats,
        long totalBytes,
        long currentBytes,
        IProgress<MergeProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);
        stats.DirectoriesMerged++;

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(directory);
            var target = Path.Combine(destinationDirectory, name);

            if (!Directory.Exists(target))
            {
                Directory.CreateDirectory(target);
                currentBytes = await MergeDirectoryContentsAsync(directory, target, options, stats, totalBytes,
                    currentBytes, progress, cancellationToken);
                if (options.Mode == CopyMode.Move) TryDeleteDirectoryAfterMove(directory, stats);
                continue;
            }

            if (File.Exists(target))
            {
                var decision = await _resolver.ResolveAsync(new ConflictContext(true, directory, target), cancellationToken);
                if (decision == ConflictDecision.Cancel) throw new OperationCanceledException();
                if (decision == ConflictDecision.Skip) continue;
                if (decision == ConflictDecision.Rename)
                {
                    target = GetUniqueDirectoryPath(target);
                    Directory.CreateDirectory(target);
                    stats.DirectoriesRenamed++;
                    currentBytes = await MergeDirectoryContentsAsync(directory, target, options, stats, totalBytes,
                        currentBytes, progress, cancellationToken);
                }
                else
                {
                    File.Delete(target);
                    Directory.CreateDirectory(target);
                    currentBytes = await MergeDirectoryContentsAsync(directory, target, options, stats, totalBytes,
                        currentBytes, progress, cancellationToken);
                }
                if (options.Mode == CopyMode.Move) TryDeleteDirectoryAfterMove(directory, stats);
                continue;
            }

            // Same-name folders: merge recursively by default.
            if (options.DirectoryConflict == DirectoryConflictAction.Merge)
            {
                currentBytes = await MergeDirectoryContentsAsync(directory, target, options, stats, totalBytes,
                    currentBytes, progress, cancellationToken);
                if (options.Mode == CopyMode.Move) TryDeleteDirectoryAfterMove(directory, stats);
                continue;
            }

            var conflict = await _resolver.ResolveAsync(new ConflictContext(true, directory, target), cancellationToken);
            if (conflict == ConflictDecision.Cancel) throw new OperationCanceledException();
            if (conflict == ConflictDecision.Skip) continue;
            if (conflict == ConflictDecision.Rename)
            {
                target = GetUniqueDirectoryPath(target);
                Directory.CreateDirectory(target);
                stats.DirectoriesRenamed++;
                currentBytes = await MergeDirectoryContentsAsync(directory, target, options, stats, totalBytes,
                    currentBytes, progress, cancellationToken);
            }
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            var target = Path.Combine(destinationDirectory, name);

            if (!File.Exists(target) && !Directory.Exists(target))
            {
                currentBytes = await CopyFileAsync(file, target, options, stats, totalBytes, currentBytes, progress, cancellationToken);
                continue;
            }

            if (Directory.Exists(target))
            {
                var decision = await _resolver.ResolveAsync(new ConflictContext(false, file, target), cancellationToken);
                if (decision == ConflictDecision.Cancel) throw new OperationCanceledException();
                if (decision == ConflictDecision.Skip) { stats.FilesSkipped++; continue; }
                if (decision == ConflictDecision.Rename)
                {
                    target = GetUniqueFilePath(target);
                    stats.FilesRenamed++;
                }
                else
                    Directory.Delete(target, true);
            }
            else
            {
                var decision = await _resolver.ResolveAsync(new ConflictContext(false, file, target), cancellationToken);
                if (decision == ConflictDecision.Cancel) throw new OperationCanceledException();
                if (decision == ConflictDecision.Skip) { stats.FilesSkipped++; continue; }
                if (decision == ConflictDecision.Rename)
                {
                    target = GetUniqueFilePath(target);
                    stats.FilesRenamed++;
                }
                else if (decision == ConflictDecision.Overwrite)
                    stats.FilesOverwritten++;
            }

            var existed = File.Exists(target);
            currentBytes = await CopyFileAsync(file, target, options, stats, totalBytes, currentBytes, progress, cancellationToken);
        }

        return currentBytes;
    }

    private static async Task<long> CopyFileAsync(
        string source,
        string target,
        MergeOptions options,
        MergeStats stats,
        long totalBytes,
        long currentBytes,
        IProgress<MergeProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var targetDir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);

            const int bufferSize = 8 * 1024 * 1024;
            await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
            await using var targetStream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);

            var buffer = new byte[bufferSize];
            int read;
            while ((read = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await targetStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                currentBytes += read;
                stats.BytesCopied += read;
                progress?.Report(new MergeProgress(source, target, currentBytes, totalBytes,
                    $"正在复制：{Path.GetFileName(source)}"));
            }

            await targetStream.FlushAsync(cancellationToken);

            if (options.PreserveTimestamp)
            {
                File.SetCreationTimeUtc(target, File.GetCreationTimeUtc(source));
                File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
                File.SetLastAccessTimeUtc(target, File.GetLastAccessTimeUtc(source));
            }
            if (options.PreserveAttributes)
            {
                File.SetAttributes(target, File.GetAttributes(source));
            }

            if (options.Verification == VerificationMode.Size)
            {
                if (new FileInfo(source).Length != new FileInfo(target).Length)
                    throw new IOException("复制后文件大小校验失败。" );
            }
            else if (options.Verification == VerificationMode.Sha256)
            {
                var a = await Sha256Async(source, cancellationToken);
                var b = await Sha256Async(target, cancellationToken);
                if (!CryptographicOperations.FixedTimeEquals(a, b))
                    throw new IOException("复制后 SHA-256 校验失败。" );
            }

            stats.FilesCopied++;
            if (options.Mode == CopyMode.Move)
                File.Delete(source);
        }
        catch (Exception ex)
        {
            stats.FilesFailed++;
            stats.Errors.Add($"{source} -> {target}: {ex.Message}");
            throw;
        }

        return currentBytes;
    }

    private static async Task<byte[]> Sha256Async(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        using var sha = SHA256.Create();
        return await sha.ComputeHashAsync(stream, ct);
    }

    private static async Task<long> CalculateTotalBytesAsync(IEnumerable<string> sources, CancellationToken ct)
    {
        long total = 0;
        foreach (var root in sources)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try { total += new FileInfo(file).Length; } catch { }
                await Task.Yield();
            }
        }
        return total;
    }

    private static string GetUniqueFilePath(string original)
    {
        var directory = Path.GetDirectoryName(original) ?? ".";
        var name = Path.GetFileNameWithoutExtension(original);
        var extension = Path.GetExtension(original);
        for (int i = 1; ; i++)
        {
            var candidate = Path.Combine(directory, $"{name} ({i}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }

    private static string GetUniqueDirectoryPath(string original)
    {
        var directory = Path.GetDirectoryName(original) ?? ".";
        var name = Path.GetFileName(original);
        for (int i = 1; ; i++)
        {
            var candidate = Path.Combine(directory, $"{name} ({i})");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }

    private static void TryDeleteDirectoryAfterMove(string directory, MergeStats stats)
    {
        TryDeleteEmptyDirectory(directory, stats);
    }

    private static void TryDeleteEmptyDirectory(string directory, MergeStats stats)
    {
        try
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
        catch (Exception ex)
        {
            stats.Errors.Add($"删除空文件夹失败 {directory}: {ex.Message}");
        }
    }
}
