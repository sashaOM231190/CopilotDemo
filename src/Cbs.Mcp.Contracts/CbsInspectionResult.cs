namespace Cbs.Mcp.Contracts;

public sealed record CbsInspectionResult(
    string AnalysisId,
    string InputPath,
    string? WorkspaceDirectory,
    string LogDomain,
    bool ServicingEvidenceFound,
    long TotalBytes,
    IReadOnlyList<CbsArtifact> Artifacts,
    IReadOnlyList<string> Warnings);
