using Cbs.Mcp.Contracts;

namespace Cbs.Mcp.Server.Analysis;

public sealed class CbsFailureTracer
{
    public CbsFailureTraceResult Trace(
        string analysisId,
        FailureFinding finding,
        IReadOnlyList<ParsedCbsRecord> records)
    {
        var related = records
            .Where(record => finding.Session is not null
                ? string.Equals(
                    record.Session,
                    finding.Session,
                    StringComparison.OrdinalIgnoreCase)
                : finding.HResult is not null
                    && string.Equals(
                        record.HResult,
                        finding.HResult,
                        StringComparison.OrdinalIgnoreCase))
            .OrderBy(record => record.Timestamp)
            .ThenBy(record => record.LineNumber)
            .ToArray();

        var selected = new List<(string Stage, ParsedCbsRecord Record)>();
        AddFirst(selected, "Initiation", related, r =>
            r.Message.Contains("start", StringComparison.OrdinalIgnoreCase)
            || r.Message.Contains("initiating", StringComparison.OrdinalIgnoreCase));
        AddFirst(selected, "PrimaryFailure", related, r =>
            r.HResult is not null || r.Severity == "Error");
        AddFirst(selected, "DerivedFailure", related, r =>
            r.Message.Contains("failed", StringComparison.OrdinalIgnoreCase)
            && r.HResult is null);
        AddFirst(selected, "Consequence", related, r =>
            r.Message.Contains("cannot", StringComparison.OrdinalIgnoreCase)
            || r.Message.Contains("aborting", StringComparison.OrdinalIgnoreCase));
        AddFirst(selected, "Rollback", related, r =>
            r.TerminalState == CbsTerminalState.Rollback);
        AddFirst(selected, "TerminalResult", related, r =>
            r.TerminalState is CbsTerminalState.Success or CbsTerminalState.Failure);

        if (selected.Count == 0 && related.Length > 0)
        {
            selected.Add(("PrimaryFailure", related[0]));
        }

        var steps = selected
            .DistinctBy(item => (item.Stage, item.Record.SourcePath, item.Record.LineNumber))
            .Select((item, index) => new FailureTraceStep(
                index + 1,
                item.Stage,
                item.Record.Timestamp,
                item.Record.Session,
                item.Record.HResult,
                new EvidenceReference(
                    item.Record.SourcePath,
                    item.Record.LineNumber,
                    item.Record.Evidence)))
            .ToArray();

        return new CbsFailureTraceResult(analysisId, finding.FindingId, steps);
    }

    private static void AddFirst(
        ICollection<(string Stage, ParsedCbsRecord Record)> selected,
        string stage,
        IEnumerable<ParsedCbsRecord> records,
        Func<ParsedCbsRecord, bool> predicate)
    {
        var record = records.FirstOrDefault(predicate);
        if (record is not null)
        {
            selected.Add((stage, record));
        }
    }
}
