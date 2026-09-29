using System.ComponentModel;
using Cbs.Mcp.Contracts;
using Cbs.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Cbs.Mcp.Server.Tools;

[McpServerToolType]
public sealed class CbsTools(ILocalCbsToolService service)
{
    [McpServerTool(Name = "inspect_cbs")]
    [Description(
        "Inspect a local directory for Windows CBS servicing evidence. "
        + "Always call this before diagnose_cbs. Returns an analysisId for later calls.")]
    public Task<ToolResult<CbsInspectionResult>> InspectCbsAsync(
        [Description("Absolute local directory containing CBS.log or CbsPersist logs.")]
        string? path,
        CancellationToken cancellationToken) =>
        service.InspectAsync(path, cancellationToken);

    [McpServerTool(Name = "diagnose_cbs")]
    [Description(
        "Parse and diagnose a previously inspected CBS analysis. "
        + "Requires the analysisId returned by inspect_cbs.")]
    public Task<ToolResult<CbsDiagnosisResult>> DiagnoseCbsAsync(
        [Description("Analysis identifier returned by inspect_cbs.")]
        string analysisId,
        CancellationToken cancellationToken) =>
        service.DiagnoseAsync(analysisId, cancellationToken);

    [McpServerTool(Name = "show_cbs_evidence")]
    [Description(
        "Return exact source-file and line evidence for a CBS diagnosis finding. "
        + "Omit findingId to use the highest-ranked finding.")]
    public Task<ToolResult<CbsEvidenceResult>> ShowCbsEvidenceAsync(
        [Description("Analysis identifier returned by inspect_cbs.")]
        string analysisId,
        [Description("Optional finding identifier returned by diagnose_cbs.")]
        string? findingId,
        CancellationToken cancellationToken) =>
        service.ShowEvidenceAsync(analysisId, findingId, cancellationToken);

    [McpServerTool(Name = "trace_cbs_failure")]
    [Description(
        "Build a causal CBS sequence from initiation through failure, rollback, "
        + "and terminal result. Omit findingId to trace the highest-ranked finding.")]
    public Task<ToolResult<CbsFailureTraceResult>> TraceCbsFailureAsync(
        [Description("Analysis identifier returned by inspect_cbs.")]
        string analysisId,
        [Description("Optional finding identifier returned by diagnose_cbs.")]
        string? findingId,
        CancellationToken cancellationToken) =>
        service.TraceFailureAsync(analysisId, findingId, cancellationToken);
}
