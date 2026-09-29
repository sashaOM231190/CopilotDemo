using Cbs.Mcp.Contracts;
using Cbs.Mcp.Server.Analysis;
using Cbs.Mcp.Server.Infrastructure;
using Cbs.Mcp.Server.State;
using Microsoft.Extensions.Logging;

namespace Cbs.Mcp.Server.Services;

public sealed class LocalCbsToolService(
    CbsPathInspector inspector,
    CbsLogParser parser,
    CbsDiagnosisEngine diagnosisEngine,
    CbsFailureTracer failureTracer,
    AnalysisRepository repository,
    CbsAnalysisArtifactWriter artifactWriter,
    ILogger<LocalCbsToolService> logger) : ILocalCbsToolService
{
    public async Task<ToolResult<CbsInspectionResult>> InspectAsync(
        string? path,
        CancellationToken cancellationToken)
    {
        try
        {
            var (inspection, artifacts) =
                await inspector.InspectAsync(path, cancellationToken);
            var workspace = artifactWriter.CreateWorkspace(inspection.AnalysisId);
            inspection = inspection with { WorkspaceDirectory = workspace };
            repository.Add(inspection, artifacts);
            logger.LogInformation(
                "Created CBS analysis {AnalysisId} with {ArtifactCount} artifacts.",
                inspection.AnalysisId,
                artifacts.Count);
            return ToolResult<CbsInspectionResult>.FromData(inspection);
        }
        catch (OperationCanceledException)
        {
            return ToolResult<CbsInspectionResult>.FromError(
                "CANCELLED",
                "CBS inspection was cancelled.",
                retryable: true);
        }
        catch (ArgumentException ex)
        {
            return ToolResult<CbsInspectionResult>.FromError("INVALID_PATH", ex.Message);
        }
        catch (DirectoryNotFoundException ex)
        {
            return ToolResult<CbsInspectionResult>.FromError("PATH_NOT_FOUND", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResult<CbsInspectionResult>.FromError("ACCESS_DENIED", ex.Message);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "I/O failure while inspecting CBS path {Path}.", path);
            return ToolResult<CbsInspectionResult>.FromError(
                "IO_ERROR",
                ex.Message,
                retryable: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected CBS inspection failure.");
            return ToolResult<CbsInspectionResult>.FromError(
                "INTERNAL_ERROR",
                "The CBS inspection failed unexpectedly.");
        }
    }

    public async Task<ToolResult<CbsDiagnosisResult>> DiagnoseAsync(
        string analysisId,
        CancellationToken cancellationToken)
    {
        if (!TryGetState<CbsDiagnosisResult>(analysisId, out var state, out var error))
        {
            return error!;
        }

        await state!.Gate.WaitAsync(cancellationToken);
        try
        {
            if (state.Diagnosis is not null)
            {
                return ToolResult<CbsDiagnosisResult>.FromData(state.Diagnosis);
            }

            var parsed = await parser.ParseAsync(state.Artifacts, cancellationToken);
            state.Records = parsed.Records;
            state.Candidates = parsed.Candidates;
            var diagnosis = diagnosisEngine.Diagnose(
                analysisId,
                parsed.Records,
                parsed.Candidates);

            var workspace = state.Inspection.WorkspaceDirectory
                ?? artifactWriter.CreateWorkspace(analysisId);
            var reportPaths = await artifactWriter.WriteDiagnosisAsync(
                workspace,
                diagnosis,
                cancellationToken);
            diagnosis = diagnosis with { ReportPaths = reportPaths };
            state.Diagnosis = diagnosis;
            return ToolResult<CbsDiagnosisResult>.FromData(diagnosis);
        }
        catch (OperationCanceledException)
        {
            return ToolResult<CbsDiagnosisResult>.FromError(
                "CANCELLED",
                "CBS diagnosis was cancelled.",
                retryable: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CBS diagnosis failed for {AnalysisId}.", analysisId);
            return ToolResult<CbsDiagnosisResult>.FromError(
                "ANALYSIS_FAILED",
                "CBS parsing or diagnosis failed. Review server diagnostics.");
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public async Task<ToolResult<CbsEvidenceResult>> ShowEvidenceAsync(
        string analysisId,
        string? findingId,
        CancellationToken cancellationToken)
    {
        var diagnosisResult = await EnsureDiagnosisAsync(analysisId, cancellationToken);
        if (!diagnosisResult.Success)
        {
            return ToolResult<CbsEvidenceResult>.FromError(
                diagnosisResult.Error!.Code,
                diagnosisResult.Error.Message,
                diagnosisResult.Error.Retryable,
                diagnosisResult.Error.Details);
        }

        var diagnosis = diagnosisResult.Data!;
        var finding = ResolveFinding(diagnosis, findingId);
        if (finding is null)
        {
            return ToolResult<CbsEvidenceResult>.FromError(
                "FINDING_NOT_FOUND",
                $"Finding was not found: {findingId}");
        }

        return ToolResult<CbsEvidenceResult>.FromData(
            new CbsEvidenceResult(analysisId, finding.FindingId, finding.Evidence));
    }

    public async Task<ToolResult<CbsFailureTraceResult>> TraceFailureAsync(
        string analysisId,
        string? findingId,
        CancellationToken cancellationToken)
    {
        var diagnosisResult = await EnsureDiagnosisAsync(analysisId, cancellationToken);
        if (!diagnosisResult.Success)
        {
            return ToolResult<CbsFailureTraceResult>.FromError(
                diagnosisResult.Error!.Code,
                diagnosisResult.Error.Message,
                diagnosisResult.Error.Retryable,
                diagnosisResult.Error.Details);
        }

        if (!repository.TryGet(analysisId, out var state) || state?.Records is null)
        {
            return ToolResult<CbsFailureTraceResult>.FromError(
                "ANALYSIS_NOT_FOUND",
                $"Analysis state was not found: {analysisId}");
        }

        var finding = ResolveFinding(diagnosisResult.Data!, findingId);
        if (finding is null)
        {
            return ToolResult<CbsFailureTraceResult>.FromError(
                "FINDING_NOT_FOUND",
                $"Finding was not found: {findingId}");
        }

        return ToolResult<CbsFailureTraceResult>.FromData(
            failureTracer.Trace(analysisId, finding, state.Records));
    }

    private Task<ToolResult<CbsDiagnosisResult>> EnsureDiagnosisAsync(
        string analysisId,
        CancellationToken cancellationToken)
    {
        if (repository.TryGet(analysisId, out var state) && state?.Diagnosis is not null)
        {
            return Task.FromResult(
                ToolResult<CbsDiagnosisResult>.FromData(state.Diagnosis));
        }

        return DiagnoseAsync(analysisId, cancellationToken);
    }

    private static FailureFinding? ResolveFinding(
        CbsDiagnosisResult diagnosis,
        string? findingId) =>
        string.IsNullOrWhiteSpace(findingId)
            ? diagnosis.Findings.FirstOrDefault()
            : diagnosis.Findings.FirstOrDefault(finding =>
                string.Equals(
                    finding.FindingId,
                    findingId,
                    StringComparison.OrdinalIgnoreCase));

    private bool TryGetState<T>(
        string analysisId,
        out AnalysisState? state,
        out ToolResult<T>? error)
    {
        if (string.IsNullOrWhiteSpace(analysisId))
        {
            state = null;
            error = ToolResult<T>.FromError(
                "INVALID_ANALYSIS_ID",
                "An analysis ID is required.");
            return false;
        }

        if (!repository.TryGet(analysisId, out state))
        {
            error = ToolResult<T>.FromError(
                "ANALYSIS_NOT_FOUND",
                $"Analysis state was not found or expired: {analysisId}",
                details: new Dictionary<string, string>
                {
                    ["nextStep"] = "Call inspect_cbs again to create a new analysis."
                });
            return false;
        }

        error = null;
        return true;
    }
}
