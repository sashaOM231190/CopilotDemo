# Copilot + Skill + MCP CBS Troubleshooting Lab

This lab demonstrates a complete, working control flow:

```text
User -> Copilot -> Skill -> SKILL.md -> MCP client -> MCP server
     -> MCP tool -> application service -> CBS domain engine
     -> structured result -> Copilot -> User
```

The implementation uses .NET 10 and the official `ModelContextProtocol` C# SDK. It analyzes local Windows Component Based Servicing (CBS) text logs without putting parsing or diagnosis logic in the MCP tool handlers.

## Lab order

Do the lab in this order; do not start by reading the finished server:

| Phase | Outcome | Material |
|---|---|---|
| 1 | Understand the HLD and boundaries | [01-architecture.md](docs/01-architecture.md) |
| 2 | Understand classes, interfaces, and calls | [02-lld-and-code.md](docs/02-lld-and-code.md) |
| 3 | Build the solution and contracts | Source under `src\` |
| 4 | Start a minimal stdio MCP server | `Program.cs`, `CbsTools.cs` |
| 5 | Test MCP initialize, tools/list, tools/call | `tools\Cbs.Mcp.SmokeClient` |
| 6 | Configure the MCP client | [04-skill-and-configuration.md](docs/04-skill-and-configuration.md) |
| 7 | Add the CBS skill | `.github\skills\cbs-troubleshooter\SKILL.md` |
| 8 | Implement inspection and state | `CbsPathInspector`, `AnalysisRepository` |
| 9 | Implement streaming parsing | `CbsLogParser` |
| 10 | Implement session-aware diagnosis | `CbsDiagnosisEngine` |
| 11 | Add evidence and tracing | `CbsFailureTracer`, service methods |
| 12 | Add reports and persistence boundary | `CbsAnalysisArtifactWriter` |
| 13 | Run the complete pipeline | [03-end-to-end-flow.md](docs/03-end-to-end-flow.md) |
| 14 | Design remediation handoff | [07-remediation-extension.md](docs/07-remediation-extension.md) |

## Build and run

```powershell
cd E:\copilot_mcp_skill_lab
dotnet build CbsCopilotLab.slnx
dotnet test CbsCopilotLab.slnx --no-build
dotnet run --project tools\Cbs.Mcp.SmokeClient --no-build -- `
  src\Cbs.Mcp.Server\bin\Debug\net10.0\Cbs.Mcp.Server.dll `
  samples\CBS
```

The smoke client launches the server over stdio, performs MCP initialization, lists the four tools, and calls `inspect_cbs`.

## Deliverable map

| Required deliverable | Location |
|---|---|
| Complete HLD and final HLD | `docs\01-architecture.md` |
| Incremental 14-phase build plan | `docs\00-incremental-lab-plan.md` |
| Complete LLD and class call diagrams | `docs\02-lld-and-code.md` |
| End-to-end workflow and sequence | `docs\03-end-to-end-flow.md` |
| Folder structure | `docs\02-lld-and-code.md` |
| Production-style skill | `.github\skills\cbs-troubleshooter\SKILL.md` |
| MCP configuration | `.vscode\mcp.json`, `config\mcp.generic.json` |
| MCP server and tools | `src\Cbs.Mcp.Server` |
| Contracts | `src\Cbs.Mcp.Contracts` |
| Parser and diagnosis engine | `src\Cbs.Mcp.Server\Analysis` |
| Testing and debugging | `docs\05-testing-and-debugging.md` |
| Security and observability | `docs\06-security-and-observability.md` |
| Remediation extension | `docs\07-remediation-extension.md` |
| Exercises, prompts, expected calls/results | `docs\08-lab-exercises.md` |
| Exact source map | `docs\09-source-map.md` |

## Scope

The core lab is read-only diagnosis. The remediation section is an architecture extension only: it produces a deterministic handoff contract but does not download or execute a remediation package.
