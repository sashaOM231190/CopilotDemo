namespace Cbs.Mcp.Contracts;

public sealed record EvidenceReference(
    string SourcePath,
    int LineNumber,
    string Text);

public sealed record FailureFinding(
    string FindingId,
    string Title,
    string? HResult,
    string? Session,
    string? Package,
    int Confidence,
    int Rank,
    string Rationale,
    IReadOnlyList<EvidenceReference> Evidence,
    string RecommendedAction,
    string? ActionId);
