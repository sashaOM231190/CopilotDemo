namespace Cbs.Mcp.Contracts;

public sealed record CbsDiagnosisResult(
    string AnalysisId,
    string Summary,
    string? LatestCompletedSession,
    DateTimeOffset? LastSuccessfulServicingPoint,
    IReadOnlyList<FailureFinding> Findings,
    int ParsedRecordCount,
    int SuppressedCandidateCount,
    IReadOnlyList<string> ReportPaths);

public sealed record CbsEvidenceResult(
    string AnalysisId,
    string? FindingId,
    IReadOnlyList<EvidenceReference> Evidence);

public sealed record FailureTraceStep(
    int Order,
    string Stage,
    DateTimeOffset? Timestamp,
    string? Session,
    string? HResult,
    EvidenceReference Evidence);

public sealed record CbsFailureTraceResult(
    string AnalysisId,
    string FindingId,
    IReadOnlyList<FailureTraceStep> Steps);
