namespace Cbs.Mcp.Contracts;

public sealed record RemediationEvidence(string? HResult);

public sealed record RemediationHandoff(
    string AnalysisId,
    string Scenario,
    string ActionId,
    RemediationEvidence Evidence);
