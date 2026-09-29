using Cbs.Mcp.Contracts;
using Cbs.Mcp.Server.Analysis;

namespace Cbs.Mcp.Tests;

public sealed class CbsLogParserTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"cbs-parser-{Guid.NewGuid():N}");

    [Fact]
    public async Task ParseAsync_StreamsAndExtractsStructuredFailure()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "CBS.log");
        await File.WriteAllLinesAsync(
            path,
            [
                "2026-01-01 10:00:00 Info CBS Session: 42_1 Package: Package_for_KB123 started",
                "2026-01-01 10:00:01 Error CBS Session: 42_1 Package: Package_for_KB123 failed HRESULT 0x800F081F",
                "2026-01-01 10:00:02 Error CBS Session: 42_1 session complete failed 0x800F081F"
            ]);
        var artifact = new CbsArtifact(
            path,
            "CbsTextLog",
            new FileInfo(path).Length,
            File.GetLastWriteTimeUtc(path),
            true);

        var parser = new CbsLogParser();
        var result = await parser.ParseAsync([artifact], CancellationToken.None);

        Assert.Equal(3, result.Records.Count);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal("0X800F081F", result.Candidates[0].HResult);
        Assert.Equal("42_1", result.Candidates[0].Session);
        Assert.Equal(2, result.Candidates[0].LineNumber);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
