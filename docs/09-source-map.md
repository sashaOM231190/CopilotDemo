# 9. Exact Source Map

Use this map while tracing the implementation in a debugger. Line numbers refer to the completed lab source.

## MCP bootstrap and discovery

| Concern | Source |
|---|---|
| Create Generic Host | `src\Cbs.Mcp.Server\Program.cs:10` |
| Route console diagnostics to stderr | `src\Cbs.Mcp.Server\Program.cs:12-16` |
| Register domain/application dependencies | `src\Cbs.Mcp.Server\Program.cs:18-24` |
| Register MCP server | `src\Cbs.Mcp.Server\Program.cs:27` |
| Select stdio transport | `src\Cbs.Mcp.Server\Program.cs:28` |
| Discover attributed tools | `src\Cbs.Mcp.Server\Program.cs:29` |

## Skill and client configuration

| Concern | Source |
|---|---|
| Skill objective | `.github\skills\cbs-troubleshooter\SKILL.md:8-10` |
| Inspect-before-diagnose rule | `.github\skills\cbs-troubleshooter\SKILL.md:14-28` |
| Evidence escalation | `.github\skills\cbs-troubleshooter\SKILL.md:31-36` |
| Failure trace escalation | `.github\skills\cbs-troubleshooter\SKILL.md:38-43` |
| Safety rules | `.github\skills\cbs-troubleshooter\SKILL.md:57-66` |
| Tool mapping | `.github\skills\cbs-troubleshooter\SKILL.md:68-75` |
| VS Code stdio server entry | `.vscode\mcp.json:2-12` |
| Portable concrete-path example | `config\mcp.generic.json:2-12` |

## MCP tool layer

| Tool | Source |
|---|---|
| Tool class and DI dependency | `src\Cbs.Mcp.Server\Tools\CbsTools.cs:8-9` |
| `inspect_cbs` | `src\Cbs.Mcp.Server\Tools\CbsTools.cs:11-20` |
| `diagnose_cbs` | `src\Cbs.Mcp.Server\Tools\CbsTools.cs:22-30` |
| `show_cbs_evidence` | `src\Cbs.Mcp.Server\Tools\CbsTools.cs:32-42` |
| `trace_cbs_failure` | `src\Cbs.Mcp.Server\Tools\CbsTools.cs:44-54` |

## Application service

| Operation | Interface | Implementation |
|---|---|---|
| Inspect | `src\Cbs.Mcp.Server\Services\ILocalCbsToolService.cs:7-9` | `src\Cbs.Mcp.Server\Services\LocalCbsToolService.cs:18-69` |
| Diagnose | `src\Cbs.Mcp.Server\Services\ILocalCbsToolService.cs:11-13` | `src\Cbs.Mcp.Server\Services\LocalCbsToolService.cs:71-124` |
| Evidence | `src\Cbs.Mcp.Server\Services\ILocalCbsToolService.cs:15-18` | `src\Cbs.Mcp.Server\Services\LocalCbsToolService.cs:126-152` |
| Trace | `src\Cbs.Mcp.Server\Services\ILocalCbsToolService.cs:20-23` | `src\Cbs.Mcp.Server\Services\LocalCbsToolService.cs:154-186` |
| Cached diagnosis helper | n/a | `src\Cbs.Mcp.Server\Services\LocalCbsToolService.cs:188-199` |
| Analysis validation/error mapping | n/a | `src\Cbs.Mcp.Server\Services\LocalCbsToolService.cs:213-242` |

## Domain engine

| Concern | Source |
|---|---|
| Safe path inspection entry | `src\Cbs.Mcp.Server\Analysis\CbsPathInspector.cs:16-114` |
| CBS artifact classification | `src\Cbs.Mcp.Server\Analysis\CbsPathInspector.cs:116-137` |
| Parse workflow and record cap | `src\Cbs.Mcp.Server\Analysis\CbsLogParser.cs:10-46` |
| Streaming file reader | `src\Cbs.Mcp.Server\Analysis\CbsLogParser.cs:50-76` |
| Field extraction | `src\Cbs.Mcp.Server\Analysis\CbsLogParser.cs:78-111` |
| Candidate scoring | `src\Cbs.Mcp.Server\Analysis\CbsLogParser.cs:144-164` |
| Terminal-state detection | `src\Cbs.Mcp.Server\Analysis\CbsLogParser.cs:166-188` |
| Regex definitions | `src\Cbs.Mcp.Server\Analysis\CbsLogParser.cs:190-206` |
| Diagnosis workflow | `src\Cbs.Mcp.Server\Analysis\CbsDiagnosisEngine.cs:7-82` |
| Same-session success suppression | `src\Cbs.Mcp.Server\Analysis\CbsDiagnosisEngine.cs:84-98` |
| Historical failure suppression | `src\Cbs.Mcp.Server\Analysis\CbsDiagnosisEngine.cs:100-115` |
| Finding construction/ranking data | `src\Cbs.Mcp.Server\Analysis\CbsDiagnosisEngine.cs:117-153` |
| HRESULT-to-action mapping | `src\Cbs.Mcp.Server\Analysis\CbsDiagnosisEngine.cs:155-167` |
| Rollback consequence suppression | `src\Cbs.Mcp.Server\Analysis\CbsDiagnosisEngine.cs:169-181` |
| Failure trace construction | `src\Cbs.Mcp.Server\Analysis\CbsFailureTracer.cs:7-77` |

## State and artifacts

| Concern | Source |
|---|---|
| Analysis cache object and lock | `src\Cbs.Mcp.Server\State\AnalysisState.cs:5-34` |
| Repository add | `src\Cbs.Mcp.Server\State\AnalysisRepository.cs:12-21` |
| Repository lookup | `src\Cbs.Mcp.Server\State\AnalysisRepository.cs:23-34` |
| Expiry cleanup | `src\Cbs.Mcp.Server\State\AnalysisRepository.cs:36-44` |
| Workspace creation | `src\Cbs.Mcp.Server\Infrastructure\CbsAnalysisArtifactWriter.cs:23-28` |
| JSON/text report writing | `src\Cbs.Mcp.Server\Infrastructure\CbsAnalysisArtifactWriter.cs:30-62` |

## Contracts

| Contract | Source |
|---|---|
| `ToolError`, `ToolResult<T>` | `src\Cbs.Mcp.Contracts\ToolResult.cs:3-24` |
| `CbsInspectionResult` | `src\Cbs.Mcp.Contracts\CbsInspectionResult.cs:3-11` |
| `ParsedCbsRecord`, `FailureCandidate` | `src\Cbs.Mcp.Contracts\ParsedCbsRecord.cs:3-34` |
| `FailureFinding`, evidence | `src\Cbs.Mcp.Contracts\FailureFinding.cs:3-20` |
| Diagnosis, evidence, trace results | `src\Cbs.Mcp.Contracts\CbsDiagnosisResult.cs:3-30` |
| Remediation handoff | `src\Cbs.Mcp.Contracts\RemediationHandoff.cs:3-9` |

## Tests and live protocol validation

| Validation | Source |
|---|---|
| Path inspector behavior | `tests\Cbs.Mcp.Tests\CbsPathInspectorTests.cs:11-40` |
| Streaming parser extraction | `tests\Cbs.Mcp.Tests\CbsLogParserTests.cs:12-43` |
| Successful-session suppression | `tests\Cbs.Mcp.Tests\CbsDiagnosisEngineTests.cs:9-34` |
| Current failure ranking/deduplication | `tests\Cbs.Mcp.Tests\CbsDiagnosisEngineTests.cs:37-67` |
| MCP initialization and `tools/list` | `tools\Cbs.Mcp.SmokeClient\Program.cs:20-46` |
| `inspect_cbs` protocol call | `tools\Cbs.Mcp.SmokeClient\Program.cs:48-70` |
| `diagnose_cbs` protocol call | `tools\Cbs.Mcp.SmokeClient\Program.cs:72-94` |
| Evidence and trace protocol calls | `tools\Cbs.Mcp.SmokeClient\Program.cs:96-116` |
