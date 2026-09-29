using Cbs.Mcp.Server.Analysis;

namespace Cbs.Mcp.Tests;

public sealed class CbsPathInspectorTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"cbs-inspector-{Guid.NewGuid():N}");

    [Fact]
    public async Task InspectAsync_FindsCbsTextArtifacts()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "CBS.log"),
            "2026-01-01 10:00:00 Info CBS Session: 1_1 started");

        var inspector = new CbsPathInspector();
        var (inspection, artifacts) =
            await inspector.InspectAsync(_directory, CancellationToken.None);

        Assert.True(inspection.ServicingEvidenceFound);
        Assert.StartsWith("A-", inspection.AnalysisId);
        Assert.Single(artifacts);
        Assert.True(artifacts[0].Parseable);
    }

    [Fact]
    public async Task InspectAsync_RejectsMissingDirectory()
    {
        var inspector = new CbsPathInspector();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            inspector.InspectAsync(_directory, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
