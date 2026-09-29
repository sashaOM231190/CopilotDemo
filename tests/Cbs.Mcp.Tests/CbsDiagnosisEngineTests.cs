using Cbs.Mcp.Contracts;
using Cbs.Mcp.Server.Analysis;

namespace Cbs.Mcp.Tests;

public sealed class CbsDiagnosisEngineTests
{
    [Fact]
    public void Diagnose_SuppressesFailureWhenSameSessionLaterSucceeds()
    {
        var failure = Record(
            1,
            "2026-01-01T10:00:00Z",
            "10_1",
            "0X800F081F",
            "Error CBS failed 0x800F081F");
        var success = Record(
            2,
            "2026-01-01T10:01:00Z",
            "10_1",
            null,
            "Info CBS session completed successfully",
            CbsTerminalState.Success);
        var candidate = Candidate(failure);

        var result = new CbsDiagnosisEngine().Diagnose(
            "A-test",
            [failure, success],
            [candidate]);

        Assert.Empty(result.Findings);
        Assert.Equal(1, result.SuppressedCandidateCount);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T10:01:00Z"), result.LastSuccessfulServicingPoint);
    }

    [Fact]
    public void Diagnose_RanksCurrentTerminalFailure()
    {
        var packageFailure = Record(
            4,
            "2026-01-02T09:59:59Z",
            "11_1",
            "0X800F081F",
            "Error CBS Package: Package_for_KB123 failed 0x800F081F");
        var terminalFailure = Record(
            5,
            "2026-01-02T10:00:00Z",
            "11_1",
            "0X800F081F",
            "Error CBS failed 0x800F081F",
            CbsTerminalState.Failure);

        var result = new CbsDiagnosisEngine().Diagnose(
            "A-test",
            [packageFailure, terminalFailure],
            [Candidate(packageFailure), Candidate(terminalFailure)]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("0X800F081F", finding.HResult);
        Assert.Equal("Package_for_KB123", finding.Package);
        Assert.Equal(2, finding.Evidence.Count);
        Assert.Equal("RepairComponentStore", finding.ActionId);
        Assert.Equal(1, finding.Rank);
    }

    private static ParsedCbsRecord Record(
        int line,
        string timestamp,
        string session,
        string? hresult,
        string evidence,
        CbsTerminalState terminalState = CbsTerminalState.None) =>
        new(
            "CBS.log",
            line,
            evidence,
            DateTimeOffset.Parse(timestamp),
            hresult is null ? "Info" : "Error",
            "CBS",
            session,
            "Package_for_KB123",
            hresult,
            evidence,
            terminalState);

    private static FailureCandidate Candidate(ParsedCbsRecord record) =>
        new(
            record.SourcePath,
            record.LineNumber,
            record.Timestamp,
            record.Session,
            record.Package,
            record.HResult,
            record.Message,
            record.Evidence,
            85);
}
