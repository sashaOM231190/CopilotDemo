using System.Text.Json;
using Cbs.Mcp.Contracts;

namespace Cbs.Mcp.Server.Infrastructure;

public sealed class CbsAnalysisArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _rootDirectory;

    public CbsAnalysisArtifactWriter()
    {
        _rootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CbsMcpLab",
            "analyses");
    }

    public string CreateWorkspace(string analysisId)
    {
        var workspace = Path.Combine(_rootDirectory, analysisId);
        Directory.CreateDirectory(workspace);
        return workspace;
    }

    public async Task<IReadOnlyList<string>> WriteDiagnosisAsync(
        string workspace,
        CbsDiagnosisResult diagnosis,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(workspace);
        var jsonPath = Path.Combine(workspace, "diagnosis.json");
        var textPath = Path.Combine(workspace, "diagnosis.txt");

        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                diagnosis,
                JsonOptions,
                cancellationToken);
        }

        var lines = new List<string>
        {
            $"Analysis: {diagnosis.AnalysisId}",
            $"Summary: {diagnosis.Summary}",
            $"Latest completed session: {diagnosis.LatestCompletedSession ?? "unknown"}",
            $"Last successful servicing point: {diagnosis.LastSuccessfulServicingPoint?.ToString("O") ?? "unknown"}"
        };
        lines.AddRange(diagnosis.Findings.Select(finding =>
            $"#{finding.Rank} {finding.Title} confidence={finding.Confidence}% action={finding.ActionId ?? "manual-review"}"));
        await File.WriteAllLinesAsync(textPath, lines, cancellationToken);

        return [jsonPath, textPath];
    }
}
