# 4. Skill and MCP Client Configuration

## 4.1 Why `SKILL.md` exists

The model can see tools, but tool availability alone does not encode the safest multi-step operating procedure. The skill adds domain workflow:

```text
CBS intent
  -> inspect first
  -> require analysisId
  -> diagnose only when evidence exists
  -> request exact evidence when needed
  -> request a causal trace when needed
  -> preserve HRESULTs and avoid invented causes
```

The implementation lives in C#. The workflow lives in `SKILL.md`. Changing the wording or ordering guidance does not require changing the parser.

## 4.2 Skill sections

| Section | Why it exists |
|---|---|
| YAML `name` | Stable skill identity |
| YAML `description` | Discovery trigger; it must mention concrete user intents |
| Objective | Declares the MCP server as the evidence source |
| Required workflow | Orders the tools and defines stop conditions |
| Presentation rules | Shapes the final user answer |
| Safety rules | Prevents invented evidence and accidental remediation |
| Tool selection table | Makes intent-to-tool mapping explicit |
| Error interpretation | Converts machine error codes into next actions |

The skill is stored at `.github\skills\cbs-troubleshooter\SKILL.md` for project-level discovery. In GitHub Copilot CLI, `/skills` is the management surface. Product support and skill discovery locations can differ, so verify the host's current documentation.

## 4.3 Generic stdio configuration

```json
{
  "servers": {
    "cbs": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "E:\\copilot_mcp_skill_lab\\src\\Cbs.Mcp.Server\\bin\\Debug\\net10.0\\Cbs.Mcp.Server.dll"
      ],
      "cwd": "E:\\copilot_mcp_skill_lab",
      "env": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

| Field | Meaning |
|---|---|
| `cbs` | Client-local server name; not an MCP tool name |
| `type` | Transport. `stdio` launches a child process and connects its standard streams |
| `command` | Executable to start; here the .NET host |
| `args` | Arguments to the command; here the built server DLL |
| `cwd` | Predictable working directory for relative paths and process behavior |
| `env` | Non-secret process settings. Use the host's secret-input mechanism for secrets |

## 4.4 Product-specific separation

Do not copy one configuration shape into every Copilot product.

| Host | How to approach configuration |
|---|---|
| GitHub Copilot CLI | Use `/mcp` to manage MCP server configuration and `/skills` to inspect skills. Confirm the exact persisted config with the installed CLI version. |
| VS Code with GitHub Copilot | Use workspace `.vscode\mcp.json` or the MCP configuration UI. This lab includes `.vscode\mcp.json`. |
| Visual Studio with GitHub Copilot | Use Visual Studio's MCP server support and its supported configuration surface; do not assume VS Code paths. |
| Microsoft Copilot products | Capabilities differ by product. Confirm whether the product supports direct MCP, connectors, plugins, or an agent gateway. A local stdio executable may not be usable by a cloud-hosted product. |
| Copilot Studio | Treat it as a managed/cloud agent platform. Expose a supported remote capability or connector if direct local stdio is unavailable. Apply authentication and consent. |
| Custom Copilot host | Use an MCP SDK client directly. `Cbs.Mcp.SmokeClient` demonstrates `StdioClientTransport`, `McpClient.CreateAsync`, `ListToolsAsync`, and `CallToolAsync`. |

The invariant is architectural, not product-specific:

```text
Host reasoning layer
  -> MCP client
  -> configured transport
  -> CBS MCP server
```

## 4.5 How descriptions influence tool selection

Compare weak and strong descriptions:

```text
Weak: "Inspects CBS."

Strong: "Inspect a local directory for Windows CBS servicing evidence.
Always call this before diagnose_cbs. Returns an analysisId for later calls."
```

The strong description supplies:

- The applicable domain
- The expected input
- Ordering constraint
- Output dependency
- Relationship to another tool

Descriptions are not security controls. The application service still validates every input and analysis ID.

## 4.6 Skill test prompts

| Prompt | Expected behavior |
|---|---|
| “Analyze `C:\Logs\CBS` and find why update failed.” | Skill activates; inspect then diagnose |
| “Show exact lines proving that CBS diagnosis.” | Evidence tool after diagnosis |
| “Trace the rollback chain.” | Failure trace tool |
| “Analyze an IIS access log.” | CBS skill should not activate |
| “Run DISM to fix it.” | Core skill should separate diagnosis from remediation and request consent in a future remediation flow |
