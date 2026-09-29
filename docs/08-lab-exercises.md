# 8. Hands-On Exercises and Final Mental Model

## Exercise 1 - Architecture before implementation

1. Read the HLD.
2. For each box, state its owned decision.
3. Identify forbidden responsibilities:
   - Copilot must not parse CBS.
   - `SKILL.md` must not contain the parser.
   - `CbsTools` must not enumerate files.
   - The parser must not decide MCP errors.
4. Draw the inspect and diagnose call chains from memory.

## Exercise 2 - Contracts first

Inspect `Cbs.Mcp.Contracts` and answer:

- Why is `EvidenceReference` separate from `FailureFinding`?
- Why can `Timestamp` and `HResult` be null?
- Why is `ToolResult<T>` generic?
- Which errors are expected operational outcomes rather than exceptions?

Add a serialization test for one success and one failure result.

## Exercise 3 - Minimal MCP server

Temporarily register only a simple test tool, build the server, and use the smoke client to list it. Then restore the four CBS tools.

Observe:

- Attributes create tool metadata.
- The MCP client does not call C# methods directly.
- The MCP server performs parameter binding.
- stdout must remain protocol-only.

## Exercise 4 - Skill behavior

Use these prompts:

```text
Analyze E:\copilot_mcp_skill_lab\samples\CBS and determine why the update failed.
```

```text
Show the exact CBS evidence and line numbers for the primary finding.
```

```text
Trace the failure from initiation through rollback.
```

Expected tool sequences:

```text
inspect_cbs -> diagnose_cbs
inspect_cbs -> diagnose_cbs -> show_cbs_evidence
inspect_cbs -> diagnose_cbs -> trace_cbs_failure
```

## Exercise 5 - Parser

Add CBS lines with:

- No timestamp
- Lowercase HRESULT
- No package
- Warning severity
- Terminal success
- Terminal failure
- Rollback

Assert the parser retains exact evidence and source line numbers. Do not modify the parser to “fix” evidence text.

## Exercise 6 - Diagnosis semantics

Create two sessions:

```text
Old session: error -> success
New session: error -> failure -> rollback
```

Expected result:

- Old error suppressed.
- New error retained.
- Latest completed session points to the new session.
- `SuppressedCandidateCount` is nonzero.

This exercise demonstrates why grep for “Error” is not a diagnosis engine.

## Exercise 7 - Error paths

| Input | Expected result |
|---|---|
| Null path | `INVALID_PATH` |
| Missing directory | `PATH_NOT_FOUND` |
| Expired/unknown analysis ID | `ANALYSIS_NOT_FOUND` |
| Cancellation | `CANCELLED` |
| Unexpected parser failure | `ANALYSIS_FAILED` plus stderr details |

Verify Copilot explains the error and does not invent a root cause.

## Exercise 8 - Expected tool responses

Inspection shape:

```json
{
  "success": true,
  "data": {
    "analysisId": "A-...",
    "inputPath": "E:\\copilot_mcp_skill_lab\\samples\\CBS",
    "workspaceDirectory": "C:\\Users\\...\\CbsMcpLab\\analyses\\A-...",
    "logDomain": "Windows Component Based Servicing",
    "servicingEvidenceFound": true,
    "totalBytes": 638,
    "artifacts": [],
    "warnings": []
  },
  "error": null
}
```

Diagnosis shape:

```json
{
  "success": true,
  "data": {
    "analysisId": "A-...",
    "summary": "The highest-ranked current failure is CBS servicing failure 0X800F081F.",
    "latestCompletedSession": "101_1",
    "lastSuccessfulServicingPoint": "2026-09-25T10:00:05+00:00",
    "findings": [
      {
        "findingId": "F-...",
        "hResult": "0X800F081F",
        "session": "101_1",
        "package": "Package_for_KB5000002",
        "rank": 1,
        "actionId": "RepairComponentStore"
      }
    ],
    "suppressedCandidateCount": 1
  },
  "error": null
}
```

Exact IDs, timestamps, byte counts, and generated paths vary.

## Exercise 9 - Extend safely

Possible extensions:

- Parse compressed `CbsPersist*.cab` through a bounded archive abstraction.
- Add durable encrypted repository storage.
- Add HRESULT family knowledge as data, not a giant tool handler.
- Add schema validation for contracts.
- Add a remote HTTP MCP transport with authentication and strict host validation.
- Add a separate remediation MCP server that accepts only allowlisted `actionId` values.

## Complete control-flow summary

```text
1. User expresses troubleshooting intent.
2. Copilot reasons that CBS skill applies.
3. SKILL.md supplies ordered workflow and safety rules.
4. MCP client discovers tools from the configured server.
5. inspect_cbs crosses the protocol boundary.
6. CbsTools forwards the typed request.
7. LocalCbsToolService coordinates inspection, workspace, and state.
8. diagnose_cbs retrieves state and invokes parser plus diagnosis engine.
9. Domain logic suppresses obsolete evidence and ranks current findings.
10. Contracts carry the result through MCP.
11. Copilot turns the structured result into a user-facing explanation.
12. Optional remediation uses a separate deterministic, consented handoff.
```

## Final mental model

```text
COPILOT
    decides what needs to happen

SKILL.md
    tells Copilot the workflow to follow

MCP
    provides the protocol boundary

MCP TOOL
    exposes an operation

APPLICATION SERVICE
    coordinates the operation

DOMAIN ENGINE
    performs the real analysis

CONTRACT
    carries structured information

COPILOT
    converts structured results into a user-facing answer
```

Core principle:

```text
Copilot owns reasoning and orchestration.
SKILL.md owns workflow instructions.
MCP owns capability exposure.
Application services own use-case orchestration.
Domain components own CBS intelligence.
Contracts keep every boundary explicit.
```
