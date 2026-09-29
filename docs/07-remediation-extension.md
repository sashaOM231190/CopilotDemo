# 7. Optional Remediation Extension

This phase is intentionally separate from the read-only diagnosis lab.

## 7.1 Extended architecture

```text
CBS Diagnosis
  -> Recommended remediation
  -> deterministic actionId
  -> Generate handoff
  -> GetHelp
  -> Validate handoff and consent
  -> DiagSvc
  -> GetHelp Server
  -> Select DAF by actionId
  -> Download signed DAF
  -> Verify publisher/hash/policy
  -> Execute remediation
  -> Return structured execution result
  -> Copilot
  -> rerun inspect_cbs and diagnose_cbs
  -> compare before/after state
```

The diagnosis MCP server should not silently become a general command runner. Prefer a separate remediation capability with stronger authorization and audit.

## 7.2 Handoff contract

```json
{
  "analysisId": "A-123",
  "scenario": "CBS_UPDATE_FAILURE",
  "actionId": "RepairComponentStore",
  "evidence": {
    "hresult": "0x800F081F"
  }
}
```

The C# contract is `RemediationHandoff`.

### Why `actionId` is deterministic

- It maps to an allowlisted, versioned remediation definition.
- Policy can approve or deny it without interpreting natural language.
- Telemetry can aggregate success and rollback rates.
- The server can prevent parameter injection and arbitrary command execution.
- Repeated diagnoses produce the same action intent for the same condition.

### Why evidence accompanies remediation

- The executor can validate that the action matches the diagnosed condition.
- Auditors can reconstruct why a state-changing operation was proposed.
- Policy can require specific HRESULTs, confidence, or evidence classes.
- Post-remediation verification can compare the original finding.

### Why arbitrary commands are excluded

A handoff such as `{ "command": "powershell ..." }` transfers unbounded authority from model-generated text to an executor. It is difficult to authorize, validate, sign, audit, or make idempotent. `actionId` should resolve server-side to a reviewed implementation.

## 7.3 Suggested remediation contracts

```text
RemediationRequest
  analysisId
  scenario
  actionId
  evidence
  userConsentToken

RemediationResult
  executionId
  actionId
  status
  startedAt
  completedAt
  rebootRequired
  safeSummary
  verificationRequired
```

## 7.4 Verification loop

Never equate “remediation command exited successfully” with “Windows Update is fixed.”

```text
Execution result
  -> new inspect_cbs
  -> new analysisId
  -> new diagnose_cbs
  -> compare current finding/session/terminal state
  -> tell user whether the original condition is gone
```

The verification analysis must be new because the old repository state and artifacts describe the pre-remediation filesystem.
