# 6. Security and Observability

## 6.1 Security controls

| Risk | Control in this lab | Production extension |
|---|---|---|
| Path confusion | `Path.GetFullPath` and existence check | Allowlisted roots; explicit user-selected paths |
| Reparse traversal | Reject root reparse point and skip child reparse directories | Resolve final handles and enforce volume/root policy |
| Unexpected files | CBS name classification and parseable extension filter | Content sniffing and archive validation |
| Large input | Artifact-count and aggregate-byte limits | Per-file limits, quotas, and streaming archive extraction |
| Memory exhaustion | Sequential `StreamReader`, no `ReadAllLines`, and a relevant-record cap | Bounded channels and incremental aggregation |
| Long-running work | `CancellationToken` propagated through I/O and locks | Explicit per-tool deadlines and host quotas |
| Concurrent duplicate work | Per-analysis `SemaphoreSlim` | Distributed lock if repository is distributed |
| Sensitive log content | Local processing and local artifacts | Redaction policy, encryption, retention, access auditing |
| Remote transmission | None in core lab | Explicit consent, TLS, tenant boundary, data classification |
| Excessive tool permission | Read-only diagnosis tools | Separate server/identity for state-changing remediation |
| Arbitrary command execution | No command parameter or shell invocation | Deterministic action registry; signed packages |
| Error disclosure | Safe `ToolError` messages; details logged on stderr | Correlation IDs and operator-only diagnostics |

## 6.2 Trust boundaries

```text
Untrusted user path
  -> validated inspector
  -> local read-only file access

Untrusted log text
  -> parser treats it as data
  -> structured records
  -> escaped JSON serialization

Copilot-selected tool arguments
  -> application validation
  -> no implicit trust because an LLM produced them
```

Tool descriptions improve selection but never replace validation or authorization.

## 6.3 Consent

Diagnosis is read-only but can expose sensitive local logs. The host should make tool execution visible and obey its approval model.

Remediation must be a separate, explicit, state-changing phase. Require:

- A diagnosed and evidence-backed action
- A deterministic allowlisted `actionId`
- User consent
- Authorization
- Package integrity verification
- Execution audit
- Post-action verification

## 6.4 Observability path

```text
Copilot skill/tool decision
  -> MCP client lifecycle and tool-call logs
  -> transport/process diagnostics
  -> MCP server stderr
  -> application service operation logs
  -> parser counts/timings
  -> diagnosis suppression/ranking diagnostics
  -> artifact paths and correlation by analysisId
```

Use `analysisId` as the correlation key across application logs and artifacts. A production implementation should also attach a per-call correlation ID.

## 6.5 What to log

| Layer | Log |
|---|---|
| MCP process | Startup, shutdown, protocol exceptions on stderr |
| Tool boundary | Tool name, analysis ID, elapsed time, success/error code |
| Inspector | Canonical root, artifact count, skipped reparse points, total bytes |
| Parser | Files parsed, records emitted, candidates emitted, cancellation |
| Diagnosis | Session count, candidate count, suppression reasons, finding count |
| Repository | Add, hit, miss, expiry; never dump full sensitive state by default |
| Artifact writer | Destination paths and write outcome |

Avoid logging full CBS lines by default because they may contain machine, package, or environment details. Exact evidence belongs in the explicitly requested result/report with appropriate local access controls.

## 6.6 stdout versus stderr

For stdio MCP:

```text
stdin  = MCP requests
stdout = MCP responses and notifications
stderr = diagnostics
```

Mixing application logs into stdout can make a valid JSON-RPC stream unparsable. The server configures `AddConsole` with `LogToStandardErrorThreshold = Trace`, which routes all configured console logs to stderr.
