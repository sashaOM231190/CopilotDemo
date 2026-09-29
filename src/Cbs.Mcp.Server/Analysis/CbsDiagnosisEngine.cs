using Cbs.Mcp.Contracts;

namespace Cbs.Mcp.Server.Analysis;

public sealed class CbsDiagnosisEngine
{
    public CbsDiagnosisResult Diagnose(
        string analysisId,
        IReadOnlyList<ParsedCbsRecord> records,
        IReadOnlyList<FailureCandidate> candidates)
    {
        var sessions = records
            .Where(r => r.Session is not null)
            .GroupBy(r => r.Session!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(r => r.Timestamp).ThenBy(r => r.LineNumber).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var terminalBySession = sessions.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.LastOrDefault(r => r.TerminalState != CbsTerminalState.None),
            StringComparer.OrdinalIgnoreCase);

        var lastSuccessfulPoint = terminalBySession.Values
            .Where(r => r?.TerminalState == CbsTerminalState.Success)
            .Max(r => r?.Timestamp);

        var latestCompletedSession = terminalBySession
            .Where(pair => pair.Value is not null)
            .OrderBy(pair => pair.Value!.Timestamp)
            .ThenBy(pair => pair.Value!.LineNumber)
            .LastOrDefault();

        var surviving = new List<FailureCandidate>();
        var suppressed = 0;

        foreach (var candidate in candidates)
        {
            if (WasSupersededBySessionSuccess(candidate, terminalBySession)
                || WasHistorical(candidate, lastSuccessfulPoint, latestCompletedSession.Key))
            {
                suppressed++;
                continue;
            }

            surviving.Add(candidate);
        }

        var rootCauseCandidates = surviving
            .Where(candidate =>
                !IsRollbackOnlyConsequence(candidate, surviving))
            .ToArray();

        var findings = rootCauseCandidates
            .GroupBy(
                candidate => new
                {
                    HResult = candidate.HResult ?? "NO_HRESULT",
                    Session = candidate.Session ?? "NO_SESSION"
                })
            .Select(group => CreateFinding(group.ToArray()))
            .OrderByDescending(finding => finding.Confidence)
            .ThenByDescending(finding => finding.Evidence[0].LineNumber)
            .Take(5)
            .Select((finding, index) => finding with { Rank = index + 1 })
            .ToArray();

        var summary = findings.Length == 0
            ? "No current root-cause candidate survived session correlation. Review evidence for incomplete or unrecognized terminal states."
            : $"The highest-ranked current failure is {findings[0].Title}.";

        return new CbsDiagnosisResult(
            analysisId,
            summary,
            latestCompletedSession.Key,
            lastSuccessfulPoint,
            findings,
            records.Count,
            suppressed,
            []);
    }

    private static bool WasSupersededBySessionSuccess(
        FailureCandidate candidate,
        IReadOnlyDictionary<string, ParsedCbsRecord?> terminalBySession)
    {
        if (candidate.Session is null
            || !terminalBySession.TryGetValue(candidate.Session, out var terminal)
            || terminal?.TerminalState != CbsTerminalState.Success)
        {
            return false;
        }

        return terminal.Timestamp is null
            || candidate.Timestamp is null
            || terminal.Timestamp >= candidate.Timestamp;
    }

    private static bool WasHistorical(
        FailureCandidate candidate,
        DateTimeOffset? lastSuccessfulPoint,
        string? latestCompletedSession)
    {
        if (lastSuccessfulPoint is null || candidate.Timestamp is null)
        {
            return false;
        }

        return candidate.Timestamp < lastSuccessfulPoint
            && !string.Equals(
                candidate.Session,
                latestCompletedSession,
                StringComparison.OrdinalIgnoreCase);
    }

    private static FailureFinding CreateFinding(IReadOnlyList<FailureCandidate> candidates)
    {
        var representative = candidates
            .OrderByDescending(candidate => candidate.BaseScore)
            .ThenByDescending(candidate => candidate.Timestamp)
            .First();
        var confidence = Math.Min(
            99,
            representative.BaseScore + Math.Min(15, (candidates.Count - 1) * 5));
        var hresult = representative.HResult;
        var package = candidates
            .Select(candidate => candidate.Package)
            .FirstOrDefault(packageName => packageName is not null);
        var title = hresult is null
            ? "CBS servicing operation failed"
            : $"CBS servicing failure {hresult}";
        var action = MapAction(hresult);

        return new FailureFinding(
            $"F-{Guid.NewGuid():N}"[..14],
            title,
            hresult,
            representative.Session,
            package,
            confidence,
            0,
            "This candidate remained after successful-session suppression, historical-failure suppression, deduplication, and confidence ranking.",
            candidates
                .Take(5)
                .Select(candidate => new EvidenceReference(
                    candidate.SourcePath,
                    candidate.LineNumber,
                    candidate.Evidence))
                .ToArray(),
            action.Recommendation,
            action.ActionId);
    }

    private static (string Recommendation, string? ActionId) MapAction(string? hresult) =>
        hresult switch
        {
            "0X800F081F" => (
                "Provide a matching repair source and repair the component store before retrying the update.",
                "RepairComponentStore"),
            "0X80073712" => (
                "Repair component-store corruption, restart if required, and retry servicing.",
                "RepairComponentStore"),
            _ => (
                "Collect the ranked evidence and validate the package/session context before remediation.",
                null)
        };

    private static bool IsRollbackOnlyConsequence(
        FailureCandidate candidate,
        IReadOnlyCollection<FailureCandidate> allCandidates) =>
        candidate.HResult is null
        && candidate.Message.Contains("rollback", StringComparison.OrdinalIgnoreCase)
        && candidate.Session is not null
        && allCandidates.Any(other =>
            other.HResult is not null
            && string.Equals(
                other.Session,
                candidate.Session,
                StringComparison.OrdinalIgnoreCase));
}
