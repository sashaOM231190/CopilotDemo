# 3. Complete Request and Protocol Flow

## 3.1 User request

```text
Analyze C:\Logs\CBS and tell me why the update failed.
```

## 3.2 End-to-end workflow

```text
User
  -> Copilot recognizes CBS intent
  -> CBS skill is activated
  -> SKILL.md requires inspection
  -> MCP client calls inspect_cbs
  -> CbsTools forwards to application service
  -> path inspector inventories evidence
  -> repository stores analysis state
  -> structured inspection returns analysisId
  -> skill requires diagnosis
  -> MCP client calls diagnose_cbs
  -> parser streams logs
  -> engine correlates sessions and terminal states
  -> artifact writer persists reports
  -> structured diagnosis returns
  -> Copilot explains the current failure
```

## 3.3 Numbered trace with code ownership

1. **Copilot receives the prompt.** The user-experience layer supplies natural language; no repository code runs yet.
2. **Copilot recognizes CBS troubleshooting intent.** The words CBS, Windows Update failure, servicing, package, or servicing HRESULT match the skill description.
3. **Copilot loads the CBS skill.** The artifact is `.github\skills\cbs-troubleshooter\SKILL.md`.
4. **`SKILL.md` says inspect first.** This prevents diagnose calls without stable state.
5. **Copilot requests `inspect_cbs`.**

   ```json
   {
     "path": "C:\\Logs\\CBS"
   }
   ```

6. **The MCP client sends a `tools/call` JSON-RPC request over stdio.** Framing and serialization are owned by the MCP SDK.
7. **`CbsTools.InspectCbsAsync` receives bound arguments.** The SDK uses the tool attribute and method signature.
8. **The tool forwards to `ILocalCbsToolService.InspectAsync`.**
9. **`LocalCbsToolService.InspectAsync` calls `CbsPathInspector.InspectAsync`.**
10. **The inspector validates and inventories files.** It canonicalizes the path, avoids reparse-point traversal, enforces bounds, and classifies artifacts.
11. **The application creates a workspace and calls `AnalysisRepository.Add`.**
12. **Inspection returns an analysis handle.**

   ```json
   {
     "success": true,
     "data": {
       "analysisId": "A-123",
       "servicingEvidenceFound": true,
       "artifacts": [
         {
           "path": "C:\\Logs\\CBS\\CBS.log",
           "kind": "CbsTextLog",
           "parseable": true
         }
       ]
     },
     "error": null
   }
   ```

13. **Copilot continues the skill workflow.** Because servicing evidence exists, diagnosis is next.
14. **Copilot calls `diagnose_cbs`.**

   ```json
   {
     "analysisId": "A-123"
   }
   ```

15. **`LocalCbsToolService` retrieves `AnalysisState`.** A missing or expired ID becomes `ANALYSIS_NOT_FOUND`.
16. **`CbsLogParser.ParseAsync` streams the inventoried text logs.** It retains source file and line numbers.
17. **`CbsDiagnosisEngine.Diagnose` correlates sessions, terminal states, failures, and HRESULT families.**
18. **The engine creates `FailureFinding` objects.** Historical or intermediate errors are suppressed before ranking.
19. **`CbsAnalysisArtifactWriter` writes JSON and text reports.**
20. **MCP returns `ToolResult<CbsDiagnosisResult>`.**
21. **Copilot interprets the contract according to the skill.** It preserves HRESULTs and distinguishes current from historical evidence.
22. **Copilot presents a user-friendly diagnosis.**

   ```text
   The latest failed servicing session is 101_1. The highest-ranked
   current failure is 0x800F081F for Package_for_KB5000002. An earlier
   occurrence in session 100_1 was suppressed because that session later
   completed successfully. The recommended action is to provide a matching
   repair source and repair the component store before retrying the update.
   ```

## 3.4 Sequence diagram

```text
User      Copilot     Skill      MCP Client   CbsTools    App Service   Domain/State
 |           |          |             |           |            |              |
 | prompt    |          |             |           |            |              |
 |---------->|          |             |           |            |              |
 |           | discover |             |           |            |              |
 |           |--------->|             |           |            |              |
 |           | inspect workflow       |           |            |              |
 |           |<---------|             |           |            |              |
 |           | tools/call inspect_cbs |           |            |              |
 |           |----------------------->|---------->|            |              |
 |           |                        |           | InspectAsync|              |
 |           |                        |           |----------->| Inspect + Add |
 |           |                        |           |            |------------->|
 |           |                        | ToolResult inspection  |              |
 |           |<-----------------------|<----------|<-----------|<-------------|
 |           | diagnose workflow      |           |            |              |
 |           | tools/call diagnose_cbs|           |            |              |
 |           |----------------------->|---------->|----------->| Get/Parse/Run|
 |           |                        |           |            |------------->|
 |           |                        | diagnosis result       |              |
 |           |<-----------------------|<----------|<-----------|<-------------|
 | answer    |                        |           |            |              |
 |<----------|                        |           |            |              |
```

## 3.5 MCP protocol view

```text
Copilot / MCP Client                    CBS MCP Server

      | initialize                         |
      |----------------------------------->|
      | capabilities / server info         |
      |<-----------------------------------|
      | tools/list                         |
      |----------------------------------->|
      | inspect_cbs                        |
      | diagnose_cbs                       |
      | show_cbs_evidence                  |
      | trace_cbs_failure                  |
      |<-----------------------------------|
      | tools/call inspect_cbs             |
      |----------------------------------->|
      | structured content / ToolResult    |
      |<-----------------------------------|
      | tools/call diagnose_cbs            |
      |----------------------------------->|
      | structured content / ToolResult    |
      |<-----------------------------------|
```

JSON-RPC is the request/response envelope used by MCP. Conceptually, each request has a method, an ID for correlation, and parameters; the response carries the matching ID and either a result or protocol-level error. Tool-level domain errors remain inside `ToolResult<T>` so expected conditions such as a missing path are machine-readable and do not masquerade as transport failures.

The MCP SDK owns exact wire serialization. Application code should not manually print JSON-RPC.

## 3.6 Evidence escalation

If the user asks, “Show me the proof,” Copilot calls:

```json
{
  "analysisId": "A-123",
  "findingId": "F-456"
}
```

`show_cbs_evidence` returns exact `sourcePath`, `lineNumber`, and `text`.

If the user asks, “Trace how it failed and rolled back,” Copilot calls `trace_cbs_failure` with the same identifiers. The returned stages are a temporal explanation, not a new diagnosis.
