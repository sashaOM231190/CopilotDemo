# 5. Testing and Debugging

## 5.1 Test pyramid by layer

| Layer | Test | Expected assertion |
|---|---|---|
| Contracts | Serialize success/error DTOs | Stable property names and nullability |
| Path inspection | Inspect controlled directory | Correct classification, limits, and warnings |
| Parser | Parse known CBS lines | Timestamp/session/package/HRESULT/evidence retained |
| Diagnosis | Session later succeeds | Earlier candidate is suppressed |
| Diagnosis | Latest session terminal failure | Current candidate survives and ranks first |
| Failure trace | Ordered session records | Correct optional stages and exact evidence |
| Application service | Missing analysis ID | `ANALYSIS_NOT_FOUND`, no exception crosses boundary |
| MCP connectivity | Initialize and list tools | Four expected tools appear |
| MCP call | Call `inspect_cbs` | Structured success and analysis ID |
| Skill | CBS prompt | Correct skill and inspect-before-diagnose order |
| End to end | Prompt through explanation | No invented facts; current/historical distinction preserved |

## 5.2 Commands

```powershell
dotnet build CbsCopilotLab.slnx
dotnet test CbsCopilotLab.slnx --no-build
dotnet run --project tools\Cbs.Mcp.SmokeClient --no-build -- `
  src\Cbs.Mcp.Server\bin\Debug\net10.0\Cbs.Mcp.Server.dll `
  samples\CBS
```

The included tests cover path inspection, streaming field extraction, successful-session suppression, and current-failure ranking.

## 5.3 Manual MCP connectivity test

1. Build the solution.
2. Run the smoke client.
3. Confirm all tools are listed.
4. Confirm `inspect_cbs` returns `success: true`.
5. Copy the returned `analysisId`.
6. In the Copilot host, ask for a diagnosis and inspect the tool timeline.
7. Confirm `diagnose_cbs` uses that exact ID.

## 5.4 End-to-end acceptance test

**Input:**

```text
Analyze E:\copilot_mcp_skill_lab\samples\CBS and tell me why the update failed.
```

**Expected calls:**

```text
inspect_cbs(path)
diagnose_cbs(analysisId)
```

**Expected semantic result:**

- Session `100_1` has an error followed by success and is suppressed.
- Session `101_1` has `0x800F081F`, terminal failure, and rollback.
- The highest-ranked current finding maps to `RepairComponentStore`.
- The answer does not report session `100_1` as the root cause.

## 5.5 Boundary debugging table

| Symptom | Likely layer | How to verify | Logs/debugger | Likely fix |
|---|---|---|---|---|
| Copilot did not use skill | Skill discovery | Check skill path, frontmatter, trigger wording | Copilot skill/tool timeline, `/skills` where supported | Correct `SKILL.md` name/description/location; reload host |
| Wrong MCP tool selected | Skill/tool metadata | Inspect tool descriptions and prompt ambiguity | MCP client tool-call trace | Make descriptions distinct; strengthen ordering guidance |
| MCP server did not start | Configuration/process | Run `dotnet <server.dll>` from configured cwd | Host MCP logs, stderr, Windows process error | Fix command, DLL path, runtime, cwd, or permissions |
| stdio protocol corruption | MCP interface/logging | Look for non-JSON output on stdout | Capture process stdout/stderr separately | Remove `Console.WriteLine`; route diagnostics to stderr |
| Tool absent from `tools/list` | Tool discovery | Run smoke client and list names | Server startup logs, debugger in assembly scan | Add `[McpServerToolType]`, `[McpServerTool]`, public method, assembly registration |
| Incorrect parameters | MCP binding/contracts | Compare generated schema with requested JSON | MCP client call payload | Improve descriptions/types; preserve nullable/required semantics |
| Analysis ID not found | State | Confirm same server process and ID | Repository/application logs | Re-run inspect; increase expiry or add durable storage |
| CBS path rejected | Inspector/security | Canonicalize path; inspect attributes and access | Inspector debugger, stderr | Supply existing readable directory; avoid reparse root |
| Parser finds no records | Parser/input | Confirm parseable files and recognizable CBS lines | Parser counters and sample line debugger | Expand supported formats without weakening evidence retention |
| Diagnosis has no finding | Diagnosis logic/data | Inspect records, candidates, terminal states, suppressed count | Engine debugger, report JSON | Verify session patterns; distinguish valid no-current-failure from parser gap |
| Serialization failure | Contracts/MCP SDK | Serialize DTO in isolated test | SDK/server exception on stderr | Use public serializable properties and supported types |
| Tool timeout | Any long-running layer | Check file sizes, cancellation, lock contention | Timings at tool/service/parser boundaries | Bound input, stream, honor cancellation, tune host timeout |

## 5.6 Debugging order

Debug from the outside inward:

```text
Prompt
  -> skill activation
  -> selected tool and arguments
  -> MCP process launch
  -> initialize/tools/list
  -> CbsTools breakpoint
  -> LocalCbsToolService breakpoint
  -> inspector/parser/engine breakpoint
  -> serialized ToolResult
  -> Copilot interpretation
```

This order prevents spending time in the parser when the server was never launched or the skill selected the wrong tool.

## 5.7 Failure injection exercises

1. Pass a missing directory and verify `PATH_NOT_FOUND`.
2. Remove read permission and verify `ACCESS_DENIED`.
3. Restart the server after inspection and verify `ANALYSIS_NOT_FOUND`.
4. Add an old failed session followed by success and verify suppression.
5. Add a current failed session and verify it outranks historical evidence.
6. Cancel a large parse and verify `CANCELLED`.
7. Temporarily write to stdout in the server, observe protocol failure, then restore stderr-only logging.
