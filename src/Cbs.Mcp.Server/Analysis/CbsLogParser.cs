using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Cbs.Mcp.Contracts;

namespace Cbs.Mcp.Server.Analysis;

public sealed partial class CbsLogParser
{
    private const int MaximumRelevantRecords = 250_000;

    public async Task<(IReadOnlyList<ParsedCbsRecord> Records, IReadOnlyList<FailureCandidate> Candidates)>
        ParseAsync(
            IEnumerable<CbsArtifact> artifacts,
            CancellationToken cancellationToken)
    {
        var records = new List<ParsedCbsRecord>();
        var candidates = new List<FailureCandidate>();

        foreach (var artifact in artifacts.Where(a => a.Parseable))
        {
            await foreach (var record in ParseFileAsync(artifact.Path, cancellationToken))
            {
                if (records.Count >= MaximumRelevantRecords)
                {
                    throw new InvalidOperationException(
                        $"CBS analysis exceeded the {MaximumRelevantRecords} relevant-record limit.");
                }

                records.Add(record);
                if (IsFailureCandidate(record))
                {
                    candidates.Add(new FailureCandidate(
                        record.SourcePath,
                        record.LineNumber,
                        record.Timestamp,
                        record.Session,
                        record.Package,
                        record.HResult,
                        record.Message,
                        record.Evidence,
                        CalculateBaseScore(record)));
                }
            }
        }

        return (records, candidates);
    }

    public async IAsyncEnumerable<ParsedCbsRecord> ParseFileAsync(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

        var lineNumber = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            cancellationToken.ThrowIfCancellationRequested();

            if (!LooksRelevant(line))
            {
                continue;
            }

            yield return ParseLine(path, lineNumber, line);
        }
    }

    private static ParsedCbsRecord ParseLine(string path, int lineNumber, string line)
    {
        var timestamp = ParseTimestamp(line);
        var hresult = HResultRegex().Match(line) is { Success: true } hresultMatch
            ? hresultMatch.Value.ToUpperInvariant()
            : null;
        var session = SessionRegex().Match(line) is { Success: true } sessionMatch
            ? sessionMatch.Groups["session"].Value
            : null;
        var package = PackageRegex().Match(line) is { Success: true } packageMatch
            ? packageMatch.Groups["package"].Value.TrimEnd(',', ';')
            : null;
        var severity = line.Contains(" Error", StringComparison.OrdinalIgnoreCase)
            ? "Error"
            : line.Contains(" Warning", StringComparison.OrdinalIgnoreCase)
                ? "Warning"
                : "Info";
        var component = line.Contains(" CBS ", StringComparison.OrdinalIgnoreCase)
            ? "CBS"
            : "Servicing";

        return new ParsedCbsRecord(
            path,
            lineNumber,
            line,
            timestamp,
            severity,
            component,
            session,
            package,
            hresult,
            line.Trim(),
            DetectTerminalState(line));
    }

    private static DateTimeOffset? ParseTimestamp(string line)
    {
        var match = TimestampRegex().Match(line);
        if (!match.Success)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            match.Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var timestamp)
            ? timestamp
            : null;
    }

    private static bool LooksRelevant(string line) =>
        line.Contains("CBS", StringComparison.OrdinalIgnoreCase)
        || line.Contains("0x800", StringComparison.OrdinalIgnoreCase)
        || line.Contains("HRESULT", StringComparison.OrdinalIgnoreCase)
        || line.Contains("session", StringComparison.OrdinalIgnoreCase)
        || line.Contains("package", StringComparison.OrdinalIgnoreCase)
        || line.Contains("rollback", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailureCandidate(ParsedCbsRecord record) =>
        record.HResult is not null
        || record.Severity == "Error"
        || record.TerminalState is CbsTerminalState.Failure or CbsTerminalState.Rollback
        || record.Message.Contains("failed", StringComparison.OrdinalIgnoreCase);

    private static int CalculateBaseScore(ParsedCbsRecord record)
    {
        var score = 20;
        if (record.HResult is not null)
        {
            score += 30;
        }

        if (record.TerminalState == CbsTerminalState.Failure)
        {
            score += 35;
        }

        if (record.Message.Contains("finalize", StringComparison.OrdinalIgnoreCase)
            || record.Message.Contains("cannot repair", StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
        }

        return Math.Min(score, 100);
    }

    private static CbsTerminalState DetectTerminalState(string line)
    {
        if (line.Contains("rollback", StringComparison.OrdinalIgnoreCase))
        {
            return CbsTerminalState.Rollback;
        }

        if (line.Contains("completed successfully", StringComparison.OrdinalIgnoreCase)
            || line.Contains("finalize: success", StringComparison.OrdinalIgnoreCase)
            || line.Contains("session complete. result: 0x0", StringComparison.OrdinalIgnoreCase))
        {
            return CbsTerminalState.Success;
        }

        if (line.Contains("session complete", StringComparison.OrdinalIgnoreCase)
            && (line.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || line.Contains("0x800", StringComparison.OrdinalIgnoreCase)))
        {
            return CbsTerminalState.Failure;
        }

        return CbsTerminalState.None;
    }

    [GeneratedRegex(@"\b0x[89A-Fa-f][0-9A-Fa-f]{7}\b", RegexOptions.CultureInvariant)]
    private static partial Regex HResultRegex();

    [GeneratedRegex(
        @"(?<timestamp>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:[+-]\d{2}:\d{2}|Z)?)",
        RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(
        @"(?:Session(?:\s+ID)?[:=]\s*)(?<session>[A-Za-z0-9_.-]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SessionRegex();

    [GeneratedRegex(
        @"(?:Package(?:\s+Identity)?[:=]\s*)(?<package>[^\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PackageRegex();
}
