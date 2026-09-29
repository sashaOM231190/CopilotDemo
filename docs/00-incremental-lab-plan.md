# 0. Incremental Development Plan

Each phase has a concrete success gate. Complete the gate before moving to the next phase.

## Phase 1 - Architecture and HLD

**Build:** Draw the user, Copilot, skill, MCP client/server, application, domain, infrastructure, and contract boundaries.

**Learn:** Ownership is more important than process names. Decide where parsing is forbidden before writing parsing code.

**Success gate:** A student can explain why `SKILL.md`, `CbsTools`, and `CbsDiagnosisEngine` are three different components.

## Phase 2 - Create the solution and projects

```powershell
dotnet new sln -n CbsCopilotLab
dotnet new classlib -n Cbs.Mcp.Contracts -o src\Cbs.Mcp.Contracts -f net10.0
dotnet new console -n Cbs.Mcp.Server -o src\Cbs.Mcp.Server -f net10.0
dotnet new xunit -n Cbs.Mcp.Tests -o tests\Cbs.Mcp.Tests -f net10.0
```

**Success gate:** All projects restore and appear in `CbsCopilotLab.slnx`.

## Phase 3 - Define contracts

Create `ToolResult<T>`, inspection, parsed record, candidate, finding, diagnosis, evidence, and trace records.

**Success gate:** The contracts project builds without depending on the MCP server project.

## Phase 4 - Create the minimal MCP server

Add `ModelContextProtocol` and `Microsoft.Extensions.Hosting`. Configure Generic Host, stderr logging, stdio transport, and assembly tool discovery.

**Success gate:** The process starts and waits for MCP input without printing diagnostics to stdout.

## Phase 5 - Expose a test tool

Create one attributed tool and connect with an MCP client.

**Success gate:** `initialize`, `tools/list`, and one `tools/call` complete.

## Phase 6 - Configure the Copilot MCP client

Add the product-appropriate server entry. For VS Code, use `.vscode\mcp.json`; for another host, translate the same command/args/transport into its supported configuration.

**Success gate:** The Copilot host lists the server's tools.

## Phase 7 - Create `SKILL.md`

Add the trigger description, inspect-first workflow, evidence/trace escalation, result presentation, and safety rules.

**Success gate:** A CBS prompt activates the skill; an unrelated log prompt does not.

## Phase 8 - Implement `inspect_cbs`

Build path validation, safe enumeration, classification, analysis ID creation, workspace creation, and repository insertion.

**Success gate:** A valid directory returns an ID; invalid, missing, and inaccessible paths return structured errors.

## Phase 9 - Implement the parser

Use `FileStream` and `StreamReader`, preserve line numbers and exact text, and extract timestamp, severity, session, package, HRESULT, and terminal state.

**Success gate:** Known CBS lines produce expected records and candidates without `File.ReadAllLines`.

## Phase 10 - Implement diagnosis

Group by session, determine terminal states, compute last success, suppress superseded failures, deduplicate, rank, and map findings to recommendations.

**Success gate:** An error followed by terminal success is not reported as the root cause.

## Phase 11 - Add evidence and tracing

Expose exact finding evidence and causal stage tracing.

**Success gate:** Evidence contains file and line; trace stays within the selected finding's session.

## Phase 12 - Add state and artifacts

Add repository expiry, per-analysis locks, cached diagnosis, and JSON/text reports.

**Success gate:** Repeated diagnosis returns stable cached state and concurrent calls do not duplicate work.

## Phase 13 - Run the complete Copilot-to-MCP test

Use the sample CBS directory, observe inspect then diagnose, and request evidence and a trace.

**Success gate:** The result suppresses the historical successful session and identifies the current failed session.

## Phase 14 - Add remediation handoff

Define a deterministic `actionId` handoff without implementing arbitrary command execution.

**Success gate:** Diagnosis remains read-only; remediation requires a separate consented and authorized boundary.
