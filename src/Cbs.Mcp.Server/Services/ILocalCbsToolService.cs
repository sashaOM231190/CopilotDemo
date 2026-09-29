using Cbs.Mcp.Contracts;

namespace Cbs.Mcp.Server.Services;

public interface ILocalCbsToolService
{
    Task<ToolResult<CbsInspectionResult>> InspectAsync(
        string? path,
        CancellationToken cancellationToken);

    Task<ToolResult<CbsDiagnosisResult>> DiagnoseAsync(
        string analysisId,
        CancellationToken cancellationToken);

    Task<ToolResult<CbsEvidenceResult>> ShowEvidenceAsync(
        string analysisId,
        string? findingId,
        CancellationToken cancellationToken);

    Task<ToolResult<CbsFailureTraceResult>> TraceFailureAsync(
        string analysisId,
        string? findingId,
        CancellationToken cancellationToken);
}
