using Cbs.Mcp.Contracts;

namespace Cbs.Mcp.Server.Analysis;

public sealed class CbsPathInspector
{
    private static readonly string[] CbsNames =
    [
        "CBS.log",
        "CbsPersist"
    ];

    private const int MaximumArtifacts = 256;
    private const long MaximumTotalBytes = 2L * 1024 * 1024 * 1024;

    public Task<(CbsInspectionResult Inspection, IReadOnlyList<CbsArtifact> Artifacts)> InspectAsync(
        string? path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A CBS log directory path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"CBS log directory was not found: {fullPath}");
        }

        var root = new DirectoryInfo(fullPath);
        if (root.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException("The analysis root cannot be a reparse point.");
        }

        var warnings = new List<string>();
        var artifacts = new List<CbsArtifact>();
        var pending = new Stack<DirectoryInfo>();
        pending.Push(root);
        long totalBytes = 0;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            foreach (var child in directory.EnumerateDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!child.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    pending.Push(child);
                }
                else
                {
                    warnings.Add($"Skipped reparse-point directory: {child.FullName}");
                }
            }

            foreach (var file in directory.EnumerateFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    warnings.Add($"Skipped reparse-point file: {file.FullName}");
                    continue;
                }

                if (!IsCbsArtifact(file))
                {
                    continue;
                }

                if (artifacts.Count >= MaximumArtifacts)
                {
                    throw new InvalidOperationException(
                        $"The path contains more than {MaximumArtifacts} CBS artifacts.");
                }

                totalBytes = checked(totalBytes + file.Length);
                if (totalBytes > MaximumTotalBytes)
                {
                    throw new InvalidOperationException(
                        $"CBS artifacts exceed the {MaximumTotalBytes} byte safety limit.");
                }

                artifacts.Add(new CbsArtifact(
                    file.FullName,
                    Classify(file),
                    file.Length,
                    file.LastWriteTimeUtc,
                    IsParseable(file)));
            }
        }

        artifacts.Sort((left, right) =>
            left.LastWriteTimeUtc.CompareTo(right.LastWriteTimeUtc));

        var analysisId = $"A-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..28];
        var result = new CbsInspectionResult(
            analysisId,
            fullPath,
            null,
            artifacts.Count > 0 ? "Windows Component Based Servicing" : "Unknown",
            artifacts.Any(a => a.Parseable),
            totalBytes,
            artifacts,
            warnings);

        return Task.FromResult<(CbsInspectionResult, IReadOnlyList<CbsArtifact>)>((result, artifacts));
    }

    private static bool IsCbsArtifact(FileInfo file) =>
        CbsNames.Any(name => file.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
        || file.Name.Equals("Sessions.xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsParseable(FileInfo file) =>
        file.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase)
        || file.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);

    private static string Classify(FileInfo file)
    {
        if (file.Extension.Equals(".cab", StringComparison.OrdinalIgnoreCase))
        {
            return "CompressedCbsLog";
        }

        if (file.Name.Equals("Sessions.xml", StringComparison.OrdinalIgnoreCase))
        {
            return "ServicingSessionMetadata";
        }

        return "CbsTextLog";
    }
}
