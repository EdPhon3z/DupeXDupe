namespace DupeXDupe;

public sealed record DuplicateFile(string Path, string Name, long Size, DateTime ModifiedUtc);

public sealed record DuplicateGroup(int GroupId, List<DuplicateFile> Files);

public sealed record DuplicateScanResult(List<DuplicateGroup> Groups, int ScannedFiles);

public enum ScanMode
{
    Fast,
    Full
}

public static class DuplicateScanner
{
    public static Task<DuplicateScanResult> ScanAsync(
        string scanPath,
        ScanMode scanMode,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => ScanInternal(scanPath, progress, cancellationToken), cancellationToken);
    }

    public static string FormatSize(long sizeBytes)
    {
        var size = (double)sizeBytes;
        string[] units = ["B", "KB", "MB", "GB", "TB"];

        foreach (var unit in units)
        {
            if (size < 1024 || unit == units[^1])
            {
                return $"{size:0.00} {unit}";
            }

            size /= 1024;
        }

        return $"{sizeBytes} B";
    }

    private static DuplicateScanResult ScanInternal(
        string scanPath,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var bySize = new Dictionary<long, List<DuplicateFile>>();
        var scannedFiles = 0;

        foreach (var path in EnumerateFilesSafe(scanPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    continue;
                }

                var file = new DuplicateFile(path, info.Name, info.Length, info.LastWriteTimeUtc);
                if (!bySize.TryGetValue(file.Size, out var list))
                {
                    list = [];
                    bySize[file.Size] = list;
                }

                list.Add(file);
                scannedFiles++;

                if (scannedFiles % 500 == 0)
                {
                    progress.Report($"Scanned {scannedFiles:N0} files...");
                }
            }
            catch
            {
                // Skip unreadable files.
            }
        }

        var candidateGroups = bySize.Values.Where(g => g.Count > 1).ToList();
        progress.Report($"Size pass complete. Candidate groups: {candidateGroups.Count:N0}");

        var result = candidateGroups
            .SelectMany(sizeGroup =>
                sizeGroup
                    .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(nameGroup => nameGroup.Count() > 1)
                    .Select(nameGroup => nameGroup.ToList()))
            .Select((files, idx) => new DuplicateGroup(idx + 1, files))
            .ToList();

        progress.Report("Scan complete (size + name).");
        return new DuplicateScanResult(result, scannedFiles);
    }

    private static IEnumerable<string> EnumerateFilesSafe(string rootPath)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
            {
                pending.Push(directory);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }
}

