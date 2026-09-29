# 2. Low-Level Design and Code Walkthrough

## 2.1 Folder structure

```text
E:\copilot_mcp_skill_lab
|-- .github\skills\cbs-troubleshooter\SKILL.md
|-- .vscode\mcp.json
|-- config\mcp.generic.json
|-- docs\
|-- samples\CBS\CBS.log
|-- src\
|   |-- Cbs.Mcp.Contracts\
|   |   |-- ToolResult.cs
|   |   |-- CbsArtifact.cs
|   |   |-- CbsInspectionResult.cs
|   |   |-- ParsedCbsRecord.cs
|   |   |-- FailureFinding.cs
|   |   |-- CbsDiagnosisResult.cs
|   |   `-- RemediationHandoff.cs
|   `-- Cbs.Mcp.Server\
|       |-- Program.cs
|       |-- Tools\CbsTools.cs
|       |-- Services\ILocalCbsToolService.cs
|       |-- Services\LocalCbsToolService.cs
|       |-- Analysis\CbsPathInspector.cs
|       |-- Analysis\CbsLogParser.cs
|       |-- Analysis\CbsDiagnosisEngine.cs
|       |-- Analysis\CbsFailureTracer.cs
|       |-- State\AnalysisState.cs
|       |-- State\AnalysisRepository.cs
|       `-- Infrastructure\CbsAnalysisArtifactWriter.cs
|-- tests\Cbs.Mcp.Tests\
`-- tools\Cbs.Mcp.SmokeClient\
```

Folders express architectural boundaries. `Contracts` has no server dependency. `Tools` knows only the application interface. `Analysis` knows CBS rules but not MCP. `State` and `Infrastructure` isolate mutable state and I/O.

## 2.2 Class relationship diagram

```text
CbsTools
    |
    | ILocalCbsToolService
    v
LocalCbsToolService
    |
    +---------------------+----------------------+
    |                     |                      |
    v                     v                      v
CbsPathInspector    AnalysisRepository    ArtifactWriter
                          |
                          v
                     AnalysisState
                          |
                    +-----+------+
                    |            |
                    v            v
              CbsLogParser  cached diagnosis
                    |
          +---------+----------+
          |                    |
          v                    v
 ParsedCbsRecord       FailureCandidate
          |                    |
          +---------+----------+
                    |
                    v
          CbsDiagnosisEngine
                    |
                    v
             FailureFinding
                    |
          +---------+----------+
          |                    |
          v                    v
CbsDiagnosisResult      CbsFailureTracer
```

## 2.3 `Program.cs`

**Purpose:** Build the process, dependency graph, MCP server, and stdio transport.

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<CbsPathInspector>();
builder.Services.AddSingleton<ILocalCbsToolService, LocalCbsToolService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

**Syntax:**

- `var builder`: local variable with compile-time inferred type.
- `Host.CreateApplicationBuilder(args)`: creates a .NET Generic Host builder.
- `AddSingleton<T>()`: registers one dependency-injection instance for the process lifetime.
- `AddMcpServer()`: registers MCP server services.
- `WithStdioServerTransport()`: binds MCP protocol input/output to stdin/stdout.
- `WithToolsFromAssembly()`: discovers `[McpServerToolType]` classes and `[McpServerTool]` methods.
- `await ... RunAsync()`: starts the asynchronous host until shutdown.

**Runtime flow:** Process starts -> DI graph is built -> MCP reads stdin -> tool requests are dispatched -> shutdown cancellation stops the host.

**HLD layer:** MCP server bootstrap plus infrastructure composition root.

**Critical rule:** stdout is the stdio protocol channel. Console logging is configured to stderr; arbitrary `Console.WriteLine` in the server can corrupt JSON-RPC framing.

**Test:** The smoke client proves initialization and tool discovery.

## 2.4 `CbsTools`

**Responsibility:** Publish tool names, descriptions, parameter descriptions, and forward calls.

**Constructor dependency:** `ILocalCbsToolService service`.

```csharp
public Task<ToolResult<CbsInspectionResult>> InspectCbsAsync(
    string? path,
    CancellationToken cancellationToken) =>
    service.InspectAsync(path, cancellationToken);
```

**Syntax:**

- `public`: callable by the MCP SDK after discovery.
- `Task<T>`: asynchronous operation whose eventual value is `T`.
- `ToolResult<CbsInspectionResult>`: a generic envelope carrying either typed data or a typed error.
- `string?`: nullable reference; invalid/missing values are handled by the application workflow.
- `CancellationToken`: cooperative cancellation propagated from client to filesystem work.
- `=>`: expression-bodied method; it performs one forwarding call.

**Caller:** MCP SDK tool dispatcher.

**Callee:** `ILocalCbsToolService.InspectAsync`.

**Input:** `{ "path": "C:\\Logs\\CBS" }`.

**Output:** `ToolResult<CbsInspectionResult>`.

**Why no parsing here:** Parsing would couple protocol metadata to domain rules, make handlers hard to test, duplicate workflows, and prevent reuse outside MCP.

### Tool methods

| Method | MCP name | Input | Output | Next call |
|---|---|---|---|---|
| `InspectCbsAsync` | `inspect_cbs` | path | inspection + analysis ID | `DiagnoseCbsAsync` |
| `DiagnoseCbsAsync` | `diagnose_cbs` | analysis ID | ranked diagnosis | evidence or trace |
| `ShowCbsEvidenceAsync` | `show_cbs_evidence` | analysis ID, optional finding ID | exact lines | Copilot presentation |
| `TraceCbsFailureAsync` | `trace_cbs_failure` | analysis ID, optional finding ID | ordered causal stages | Copilot explanation |

Tool descriptions are operational metadata for the model. “Always call this before diagnose” and “requires analysisId” reduce invalid tool ordering.

## 2.5 `ILocalCbsToolService`

**Purpose:** Keep MCP tools dependent on a stable application contract rather than a concrete implementation.

```csharp
Task<ToolResult<CbsDiagnosisResult>> DiagnoseAsync(
    string analysisId,
    CancellationToken cancellationToken);
```

**Input contract:** A non-empty analysis ID created by inspection.

**Output contract:** Success with a diagnosis or failure with `ToolError`.

**Called by:** `CbsTools`.

**Implemented by:** `LocalCbsToolService`.

**Test:** Substitute a fake implementation when testing tool forwarding.

## 2.6 `LocalCbsToolService`

**Responsibility:** Coordinate use cases and translate internal failures at the MCP boundary.

**Constructor dependencies:**

| Dependency | Reason |
|---|---|
| `CbsPathInspector` | Validate and inventory input |
| `CbsLogParser` | Stream and structure evidence |
| `CbsDiagnosisEngine` | Correlate and rank failures |
| `CbsFailureTracer` | Build a causal sequence |
| `AnalysisRepository` | Preserve multi-call state |
| `CbsAnalysisArtifactWriter` | Persist reports |
| `ILogger` | Diagnostics on stderr |

### `InspectAsync` line-by-line logic

```csharp
var (inspection, artifacts) =
    await inspector.InspectAsync(path, cancellationToken);
```

The inspector validates the root, safely enumerates it, creates an analysis ID, and returns two related values:

- `inspection`: public summary sent to the client.
- `artifacts`: complete internal inventory stored for later parsing.

Returning both prevents later tools from re-enumerating a directory that may change between calls.

```csharp
var workspace = artifactWriter.CreateWorkspace(inspection.AnalysisId);
inspection = inspection with { WorkspaceDirectory = workspace };
```

The first line creates a deterministic per-analysis output directory. The second uses record `with` syntax to create a copy with the workspace populated; records remain immutable at contract boundaries.

```csharp
repository.Add(inspection, artifacts);
```

This is required because `diagnose_cbs` is a separate MCP request. The original method stack will no longer exist when that request arrives.

```csharp
return ToolResult<CbsInspectionResult>.FromData(inspection);
```

The application returns a structured envelope rather than throwing across MCP. Copilot can reliably distinguish success from `INVALID_PATH`, `ACCESS_DENIED`, or `IO_ERROR`.

### Inspect call chain

```text
CbsTools.InspectCbsAsync()
  -> ILocalCbsToolService.InspectAsync()
  -> LocalCbsToolService.InspectAsync()
  -> CbsPathInspector.InspectAsync()
  -> filesystem enumeration
  -> CbsAnalysisArtifactWriter.CreateWorkspace()
  -> AnalysisRepository.Add()
  -> ToolResult<CbsInspectionResult>
```

### Diagnosis call chain

```text
CbsTools.DiagnoseCbsAsync()
  -> LocalCbsToolService.DiagnoseAsync()
  -> AnalysisRepository.TryGet()
  -> AnalysisState.Gate.WaitAsync()
  -> CbsLogParser.ParseAsync()
  -> CbsDiagnosisEngine.Diagnose()
  -> CbsAnalysisArtifactWriter.WriteDiagnosisAsync()
  -> cache AnalysisState.Diagnosis
  -> ToolResult<CbsDiagnosisResult>
```

The per-analysis `SemaphoreSlim` prevents two simultaneous calls from parsing and writing the same analysis concurrently.

## 2.7 `CbsPathInspector`

**Responsibility:** Convert an untrusted path into a bounded CBS artifact inventory.

**Algorithm:**

```text
Validate non-empty path
  -> expand environment variables
  -> canonicalize with Path.GetFullPath
  -> require existing directory
  -> reject reparse-point root
  -> walk directories without following child reparse points
  -> classify CBS.log, CbsPersist*, Sessions.xml
  -> enforce file-count and aggregate-size limits
  -> mark text artifacts as parseable
  -> create analysis ID
  -> return inspection + inventory
```

**Input:** Nullable user path and cancellation token.

**Output:** Tuple of `CbsInspectionResult` and `IReadOnlyList<CbsArtifact>`.

**Errors:** Argument, missing directory, unauthorized access, I/O, safety-limit violations, or cancellation. The application layer maps these to tool errors.

**Test:** Existing CBS directory, missing directory, reparse point, artifact limit, and cancellation.

## 2.8 `CbsLogParser`

**Responsibility:** Stream parseable artifacts into exact source records and failure candidates.

```csharp
while (await reader.ReadLineAsync(cancellationToken) is { } line)
{
    lineNumber++;
    if (!LooksRelevant(line))
    {
        continue;
    }

    yield return ParseLine(path, lineNumber, line);
}
```

**Why streaming:** `File.ReadAllLines` allocates an array and every line for the complete file. CBS logs can be large. `FileStream` plus `StreamReader` reads sequentially, honors cancellation, tolerates concurrent CBS writers through `FileShare.ReadWrite | FileShare.Delete`, and retains bounded working memory.

**Produced fields:** Source path, line number, verbatim evidence, timestamp, severity, component, session, package, HRESULT, message, and terminal state.

**Evidence invariant:** `Evidence` is the original line and `LineNumber` is captured before parsing. Diagnosis may summarize, but proof remains traceable.

**FailureCandidate:** A projection of records with errors, HRESULTs, failure terminal states, rollback, or failure language. It carries a base score, not a final diagnosis.

**Test:** Known formats, missing optional fields, lowercase HRESULT, malformed timestamp, huge streamed file, cancellation, and file sharing.

## 2.9 `CbsDiagnosisEngine`

**Responsibility:** Turn noisy candidates into current ranked findings.

```text
Parsed records
  -> group by servicing session
  -> locate each session's terminal record
  -> find last successful servicing point
  -> locate latest completed session
  -> suppress candidate if its own session later succeeded
  -> suppress historical candidate before a later success
  -> deduplicate by HRESULT/session/normalized message
  -> rank by terminal state, HRESULT, repetition, and context
  -> create FailureFinding
  -> build CbsDiagnosisResult
```

### The central rule

```text
Session 100:
  10:00 Error 0x800F081F
  10:05 terminal success
  => suppress the error

Session 101:
  11:00 Error 0x800F081F
  11:04 terminal failure
  => retain and rank the error
```

An `Error` line is evidence, not automatically a root cause. Session outcome can change its meaning.

**Input:** Analysis ID, parsed records, and candidates.

**Output:** Immutable `CbsDiagnosisResult`.

**Error handling:** The pure engine does not catch programming errors. The application service owns boundary translation.

**Test:** Successful-session suppression, historical suppression, latest failed session, deduplication, ranking, and no-finding result.

## 2.10 `CbsFailureTracer`

**Responsibility:** Explain temporal causality rather than only ranking the best cause.

```text
Initiation
  -> PrimaryFailure
  -> DerivedFailure
  -> Consequence
  -> Rollback
  -> TerminalResult
```

Diagnosis asks, “What is the best current root-cause finding?” Tracing asks, “What ordered chain led from servicing start to terminal outcome?”

The tracer filters records by the selected finding's session or HRESULT, orders by timestamp and source line, selects representative stages, and returns exact evidence for each stage.

**Test:** Complete chain, absent optional stages, rollback-only sequences, same-timestamp line ordering, and unknown finding ID.

## 2.11 State and infrastructure

### `AnalysisState`

Holds immutable inspection/artifacts and mutable cached records, candidates, and diagnosis. `Gate` serializes work for one analysis. `LastAccessedAt` supports expiry.

### `AnalysisRepository`

Uses `ConcurrentDictionary<string, AnalysisState>` for thread-safe lookup. Entries expire after two hours. This lab uses process-local memory: a server restart invalidates IDs.

For production, replace this implementation behind the same conceptual boundary with encrypted durable storage or a distributed cache if server restarts and horizontal scaling must preserve analyses.

### `CbsAnalysisArtifactWriter`

Creates `%LOCALAPPDATA%\CbsMcpLab\analyses\<analysisId>` and writes `diagnosis.json` plus `diagnosis.txt`. It receives a completed diagnosis; it does not decide root cause.

## 2.12 Contracts

| Contract | Meaning |
|---|---|
| `ToolResult<T>` | Success/data or failure/error envelope |
| `ToolError` | Stable code, safe message, retryability, details |
| `CbsArtifact` | Inspected file metadata |
| `CbsInspectionResult` | Public inventory and analysis handle |
| `ParsedCbsRecord` | Structured exact source record |
| `FailureCandidate` | Unresolved potential failure |
| `FailureFinding` | Ranked, explained diagnosis |
| `CbsDiagnosisResult` | Analysis-level result |
| `EvidenceReference` | File, line, exact text |
| `CbsFailureTraceResult` | Ordered causal sequence |
| `RemediationHandoff` | Deterministic diagnosis-to-remediation intent |

Contracts are in a separate project so the domain, server, tests, clients, and future remediation adapter can share data without taking a dependency on MCP hosting.

## 2.13 Final LLD

```text
CbsTools.InspectCbsAsync()
          |
          v
ILocalCbsToolService.InspectAsync()
          |
          v
LocalCbsToolService.InspectAsync()
          |
          +-------------------------------+
          |                               |
          v                               v
CbsPathInspector.InspectAsync()    ArtifactWriter.CreateWorkspace()
          |
          v
AnalysisRepository.Add()
          |
          v
ToolResult<CbsInspectionResult>
```

```text
CbsTools.DiagnoseCbsAsync()
          |
          v
LocalCbsToolService.DiagnoseAsync()
          |
          v
AnalysisRepository.TryGet()
          |
          v
CbsLogParser.ParseAsync()
          |
          +---------------------+
          |                     |
          v                     v
ParsedCbsRecord[]       FailureCandidate[]
          |                     |
          +----------+----------+
                     |
                     v
          CbsDiagnosisEngine.Diagnose()
                     |
                     v
            CbsDiagnosisResult
                     |
          +----------+-----------+
          |                      |
          v                      v
ArtifactWriter.Write()     AnalysisState cache
```
