using Cbs.Mcp.Contracts;

namespace Cbs.Mcp.Server.State;

public sealed class AnalysisState
{
    public AnalysisState(
        CbsInspectionResult inspection,
        IReadOnlyList<CbsArtifact> artifacts)
    {
        Inspection = inspection;
        Artifacts = artifacts;
        CreatedAt = DateTimeOffset.UtcNow;
        LastAccessedAt = CreatedAt;
    }

    public CbsInspectionResult Inspection { get; }

    public IReadOnlyList<CbsArtifact> Artifacts { get; }

    public IReadOnlyList<ParsedCbsRecord>? Records { get; set; }

    public IReadOnlyList<FailureCandidate>? Candidates { get; set; }

    public CbsDiagnosisResult? Diagnosis { get; set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastAccessedAt { get; private set; }

    public SemaphoreSlim Gate { get; } = new(1, 1);

    public void Touch() => LastAccessedAt = DateTimeOffset.UtcNow;
}
