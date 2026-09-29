namespace Cbs.Mcp.Contracts;

public enum CbsTerminalState
{
    None,
    Success,
    Failure,
    Rollback
}

public sealed record ParsedCbsRecord(
    string SourcePath,
    int LineNumber,
    string Evidence,
    DateTimeOffset? Timestamp,
    string Severity,
    string Component,
    string? Session,
    string? Package,
    string? HResult,
    string Message,
    CbsTerminalState TerminalState);

public sealed record FailureCandidate(
    string SourcePath,
    int LineNumber,
    DateTimeOffset? Timestamp,
    string? Session,
    string? Package,
    string? HResult,
    string Message,
    string Evidence,
    int BaseScore);
