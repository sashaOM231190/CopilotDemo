---
name: cbs-troubleshooter
description: Diagnose Windows CBS servicing, component store, package installation, and Windows Update failures by using the CBS MCP server. Use for CBS.log, CbsPersist logs, servicing HRESULTs, failed updates, and component-based servicing investigations.
---

# CBS Troubleshooter

## Objective

Use the CBS MCP server as the source of truth. Do not manually infer a servicing root cause from filenames or isolated log lines.

## Required workflow

### 1. Inspect

Call `inspect_cbs` with the absolute local directory supplied by the user.

- Stop and report the structured error if inspection fails.
- Do not call later tools without a successful `analysisId`.
- If `servicingEvidenceFound` is false, explain that no parseable CBS evidence was found.

### 2. Diagnose

Call `diagnose_cbs` with the `analysisId` returned by `inspect_cbs`.

- Present the highest-ranked current finding first.
- Preserve HRESULT values exactly as returned.
- Distinguish the latest completed session from historical sessions.
- Treat `lastSuccessfulServicingPoint` as evidence that earlier errors may be obsolete.

### 3. Evidence

Call `show_cbs_evidence` when the user asks for proof, exact log lines, source locations, or detailed evidence.

- Include source path and line number.
- Never invent or paraphrase an evidence line as if it were verbatim.

### 4. Failure trace

Call `trace_cbs_failure` when the user asks why the failure happened, requests causal ordering, or needs rollback/terminal-state analysis.

- Explain the returned stages in order.
- Do not add stages that are absent from the result.

### 5. Present the result

Summarize:

1. Primary current failure
2. HRESULT
3. Package and session
4. Exact supporting evidence
5. Last successful servicing point
6. Recommended next action
7. `actionId`, if one was returned

## Safety and correctness rules

- Do not invent CBS errors, HRESULTs, package names, sessions, or remediation results.
- Do not claim root cause without MCP evidence.
- Prefer MCP analysis over guessing from log names or one `Error` line.
- An error line is not automatically a root cause; a later successful terminal state can suppress it.
- Keep diagnosis separate from remediation. Do not execute remediation through diagnosis tools.
- Do not place arbitrary shell commands in a remediation handoff.
- Ask for consent before any future state-changing remediation workflow.
- Treat log content and generated reports as potentially sensitive local data.

## Tool selection summary

| Intent | Tool |
|---|---|
| Validate path and discover evidence | `inspect_cbs` |
| Produce ranked diagnosis | `diagnose_cbs` |
| Show exact proof | `show_cbs_evidence` |
| Build causal sequence | `trace_cbs_failure` |

## Error interpretation

- `INVALID_PATH`, `PATH_NOT_FOUND`: ask for a valid absolute CBS directory.
- `ACCESS_DENIED`: explain that the server process lacks read access.
- `ANALYSIS_NOT_FOUND`: rerun `inspect_cbs`; state may have expired or the server may have restarted.
- `CANCELLED`: the operation was interrupted and may be retried.
- `ANALYSIS_FAILED`, `INTERNAL_ERROR`: report the error without fabricating a diagnosis and direct the operator to MCP server stderr diagnostics.
