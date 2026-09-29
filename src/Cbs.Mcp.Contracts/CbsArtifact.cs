namespace Cbs.Mcp.Contracts;

public sealed record CbsArtifact(
    string Path,
    string Kind,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc,
    bool Parseable);
