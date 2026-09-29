# 1. High-Level Architecture

## 1.1 Big picture

```text
+------------------------------+
| USER EXPERIENCE              |
| Prompt, conversation, result |
+---------------+--------------+
                |
                v
+------------------------------+
| COPILOT                      |
| Intent, reasoning, skill use |
| tool choice, explanation     |
+---------------+--------------+
                |
                v
+------------------------------+
| CBS SKILL                    |
| SKILL.md                     |
| triggers, workflow, safety   |
+---------------+--------------+
                |
                v
+------------------------------+
| MCP CLIENT                   |
| initialize, tools/list, call |
| stdio process connection     |
+---------------+--------------+
                |
                v
+------------------------------+
| MCP SERVER / INTERFACE       |
| Program.cs + CbsTools.cs     |
| inspect, diagnose, evidence, |
| trace                        |
+---------------+--------------+
                |
                v
+------------------------------+
| APPLICATION / ORCHESTRATION  |
| LocalCbsToolService          |
| workflow, locks, errors      |
+---------------+--------------+
                |
                v
+------------------------------+
| DOMAIN / ANALYSIS            |
| inspector, parser, diagnosis |
| failure tracer               |
+---------------+--------------+
                |
                v
+------------------------------+
| STATE / INFRASTRUCTURE       |
| repository, filesystem, JSON |
| and text artifacts           |
+---------------+--------------+
                |
                v
+------------------------------+
| CONTRACTS                    |
| ToolResult<T>, DTO records   |
+------------------------------+
```

## 1.2 Why each box exists

### Layer 1 - User experience

**Why:** A person needs a natural-language entry point and a comprehensible diagnosis.

**Owns:** The prompt, clarification, consent for later remediation, and display of the answer.

**Does not own:** File enumeration, CBS parsing, HRESULT correlation, or direct remediation.

**Control transfer:** The prompt is sent to Copilot. The final structured tool result is translated back into a user-facing explanation.

**Test:** Ask a representative prompt and verify that the answer includes the current failure, evidence, uncertainty, and next action without fabricating facts.

### Layer 2 - Copilot

**Why:** The solution needs an intelligence and orchestration layer that can understand intent and choose capabilities.

**Owns:** Intent recognition, skill discovery, following skill instructions, MCP tool selection, carrying `analysisId` between calls, and explaining results.

**Lower-level implementation:** The Copilot host contains an MCP client. The repository contributes a skill and MCP configuration; it does not implement Copilot itself.

**Control transfer:** Copilot matches the request to the CBS skill, reads its workflow, and asks the MCP client to call `inspect_cbs`.

**Error behavior:** Copilot should present structured errors and follow their next-step guidance. It must not replace a failed tool call with a guessed diagnosis.

**Test:** Verify that a CBS prompt activates the skill and that `inspect_cbs` precedes `diagnose_cbs`.

### Layer 3 - Skill

**Why:** A general model needs reusable, domain-specific operating instructions.

**Owns:** Trigger conditions, the inspect-before-diagnose workflow, evidence and trace escalation, safety rules, and output interpretation.

**Artifact:** `.github\skills\cbs-troubleshooter\SKILL.md`.

**Does not own:** Parsing code or MCP transport.

**Control transfer:** The skill tells Copilot which named tool to call next and how to interpret its contract.

**Test:** Give prompts for CBS, unrelated logs, proof requests, and causal trace requests; inspect which skill and tool sequence is selected.

### Layer 4 - MCP client

**Why:** Copilot needs a protocol implementation for discovering and invoking external capabilities.

**Owns:** Process launch, MCP initialization, capability negotiation, `tools/list`, `tools/call`, cancellation, and serialization.

**Lower-level implementation:** The Copilot product supplies this layer. `Cbs.Mcp.SmokeClient` is the lab's transparent reference client.

**Control transfer:** It serializes tool name and arguments into MCP JSON-RPC messages over stdio and receives content blocks.

**Test:** Launch the server, list tools, and call `inspect_cbs`.

### Layer 5 - MCP server/interface

**Why:** The CBS capability needs a stable, discoverable external boundary.

**Owns:** Server startup, stdio transport, tool metadata, parameter binding, and returning serializable results.

**Implemented by:** `Program.cs` and `Tools\CbsTools.cs`.

**Control transfer:** `CbsTools.InspectCbsAsync` forwards to `ILocalCbsToolService.InspectAsync`.

**Design rule:** Keep handlers thin. Tool descriptions guide model selection; handlers should not become the business layer.

**Test:** Verify all four names appear in `tools/list`, descriptions are precise, invalid parameters return structured errors, and stdout contains protocol traffic only.

### Layer 6 - Application/orchestration

**Why:** A use case crosses several domain and infrastructure components and needs one transaction-like coordinator.

**Owns:** Analysis lookup, locking, caching, call ordering, artifact generation, logging, and exception-to-contract translation.

**Implemented by:** `ILocalCbsToolService` and `LocalCbsToolService`.

**Control transfer:** It calls path inspection, parser, diagnosis engine, tracer, repository, and artifact writer as required by each tool.

**Test:** Mock or use real collaborators to verify successful workflows, missing analysis IDs, cancellation, and cached diagnosis reuse.

### Layer 7 - Domain/analysis

**Why:** CBS intelligence must remain reusable and testable without MCP.

**Owns:** Safe artifact classification, streaming parsing, session correlation, HRESULT extraction, successful-session suppression, ranking, and causal trace construction.

**Implemented by:** `CbsPathInspector`, `CbsLogParser`, `CbsDiagnosisEngine`, and `CbsFailureTracer`.

**Control transfer:** Domain methods accept contracts or domain records and return deterministic data. They do not know about JSON-RPC or Copilot.

**Test:** Use known log lines and session timelines. Especially verify that a later successful terminal state suppresses an earlier intermediate error.

### Layer 8 - State/infrastructure

**Why:** One conversation uses several independent MCP calls, and reports must survive beyond a single method stack.

**Owns:** In-memory analysis state, expiry, per-analysis locks, local workspaces, and JSON/text reports.

**Implemented by:** `AnalysisRepository`, `AnalysisState`, `CbsAnalysisArtifactWriter`, and the local filesystem.

**Control transfer:** `inspect_cbs` stores state keyed by `analysisId`; later tools retrieve and enrich it.

```text
inspect_cbs
  -> A-123 stored
  -> diagnose_cbs(A-123)
  -> show_cbs_evidence(A-123)
  -> trace_cbs_failure(A-123)
```

**Test:** Verify lookup, expiry behavior, concurrent diagnosis serialization, cache reuse, and report creation.

### Contracts

**Why:** Every boundary needs explicit, serializable, implementation-independent meaning.

**Owns:** Input/output shape, optionality, enum values, evidence references, and error envelopes.

**Implemented by:** The `Cbs.Mcp.Contracts` project.

**Control transfer:** Contracts pass through domain, application, MCP serialization, and Copilot interpretation.

**Test:** Serialize representative success/failure objects and reject semantically invalid combinations in contract tests or consumers.

## 1.3 Final polished HLD

```text
                          USER
                            |
                            v
                     +-------------+
                     |   COPILOT   |
                     +------+------+
                            |
                   Skill discovery
                            |
                            v
                     +-------------+
                     |  SKILL.md   |
                     +------+------+
                            |
                     Tool selection
                            |
                            v
               +-------------------------+
               |       MCP CLIENT        |
               +------------+------------+
                            |
                         MCP/stdio
                            |
                            v
               +-------------------------+
               |       MCP SERVER        |
               |      CbsTools.cs        |
               +------------+------------+
                            |
                            v
               +-------------------------+
               | APPLICATION SERVICE     |
               | LocalCbsToolService     |
               +------------+------------+
                            |
          +-----------------+----------------+
          |                 |                |
          v                 v                v
  Path Inspector        Log Parser      Repository
          |                 |
          +--------+--------+
                   |
                   v
           Diagnosis Engine
                   |
                   v
            Failure Tracer
                   |
                   v
              Contracts
                   |
          +--------+---------+
          |                  |
          v                  v
    Artifact Writer    Copilot presenter
          |                  |
          +--------+---------+
                   |
                   v
                 MCP
                   |
                   v
               COPILOT
                   |
                   v
                 USER
```

## 1.4 Component comparison

| Component | Purpose | Key boundary |
|---|---|---|
| Copilot | Reasoning and orchestration | Natural language to workflow |
| Skill | Reusable instructions | Intent to ordered tool use |
| `SKILL.md` | Skill definition artifact | Declarative guidance, not code |
| MCP client | Protocol consumer | Host to server JSON-RPC |
| MCP server | Capability provider | External protocol endpoint |
| MCP tool | Individual operation | Named typed request/response |
| Application service | Use-case orchestration | Tool call to collaborators |
| Domain engine | CBS intelligence | Records to diagnosis |
| Contract | Structured information | Stable data across boundaries |

`SKILL.md != MCP server`. The skill tells Copilot **what workflow to follow**. MCP provides **how an external operation is discovered and executed**.
