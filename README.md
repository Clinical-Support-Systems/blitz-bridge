![Blitz Bridge icon](icon_medium.png)

# Blitz Bridge

Blitz Bridge is a read-only MCP server for Azure SQL diagnostics: it lets agents run a tightly allowlisted Brent Ozar First Responder Kit (FRK) surface against preconfigured targets so teams can get fast, structured diagnostics without handing agents raw SQL credentials or arbitrary query access.

```mermaid
flowchart LR
    A[Need SQL diagnostics for agents] --> B{Choose install path}
    B --> C[Stdio local tool<br/>fastest dev setup]
    B --> D[Docker Compose demo<br/>5-minute eval]
    B --> E[Azure deployment with azd<br/>production posture]
    C --> F[Configure profiles + role grants]
    D --> F
    E --> F
    F --> G[Connect agent client config]
```

## Install

### As a local tool (stdio)

Use this when you want the quickest path for local or workstation use.

```bash
dotnet tool install -g BlitzBridge.McpServer
blitzbridge --init-config
blitzbridge --transport stdio --config path/to/profiles.json
```

`--init-config` creates a starter `profiles.json` and exits without starting the server.

- Default path (when `--config` is omitted):
  - Windows: `%APPDATA%\blitz-bridge\profiles.json`
  - Linux/macOS: `~/.config/blitz-bridge/profiles.json`
- Optional custom path:
  - `blitzbridge --init-config --config ./profiles.beta.json`

Client config examples:

- `examples/client-configs/claude-desktop.json`
- `examples/client-configs/claude-code.json`
- `examples/client-configs/cursor.json`
- `examples/client-configs/vscode-mcp.json` (VS Code: `.vscode/mcp.json` or user `settings.json` under `mcp`)

### Try it in 5 minutes (Docker Compose)

Use this when you want to evaluate behavior quickly with the included sample environment.

```bash
cd samples/docker-compose-demo
cp .env.example .env
# Edit .env with your token/password values
docker compose up --build
```

See full walkthrough: `samples/docker-compose-demo/README.md`.

### Deploy to Azure (`azd`)

Use this for production-style deployment on Azure Container Apps with managed identity, Key Vault, and diagnostics.

```bash
azd env new
azd up
```

Or one-click via ARM template (Azure portal):

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FClinical-Support-Systems%2Fblitz-bridge%2Fmain%2Finfra%2Fmain.json)

Deployment guide: `docs/deployment-azure.md`.

## Configure

### 1) Apply least-privilege SQL role grants

Run `docs/sql/blitz-bridge-role.sql` in the target database. It grants only:

- `EXECUTE` on allowed FRK procedures
- `VIEW SERVER STATE`
- `VIEW DATABASE STATE`

It also includes commented examples for managed identity and SQL auth user mapping.

### 2) Configure target profiles

Blitz Bridge uses profile-based target config:

```json
{
  "SqlTargets": {
    "Profiles": {
      "primary-sql-target": {
        "ConnectionString": "Server=tcp:...;Database=DBAtools;Authentication=Active Directory Default;Encrypt=True;ApplicationIntent=ReadOnly;",
        "AllowedDatabases": ["AppDb"],
        "AllowedProcedures": ["sp_Blitz", "sp_BlitzCache", "sp_BlitzFirst", "sp_BlitzIndex", "sp_BlitzLock", "sp_BlitzWho"],
        "Enabled": true,
        "CommandTimeoutSeconds": 60,
        "AiMode": 2
      }
    }
  }
}
```

## Connect an agent

Start with `examples/client-configs/`:

- `claude-desktop.json` (stdio)
- `claude-code.json` (stdio)
- `cursor.json` (stdio)
- `vscode-mcp.json` (stdio; VS Code uses `servers` key, not `mcpServers`)
- `claude-desktop-hosted.json` (HTTP + bearer token)
- `python-mcp-client.py` (Python MCP SDK sample: list tools + call `azure_sql_target_capabilities`)

### VS Code troubleshooting

If VS Code logs `spawn http://localhost:5000/mcp ENOENT`, a stale HTTP entry is registered for the same server name. Run **MCP: List Servers** from the Command Palette, remove conflicting `blitz-bridge` entries (workspace `.vscode/mcp.json` and user `settings.json` → `mcp.servers`), then **MCP: Restart Server**.

## Tool surface

### Query tools

- `azure_sql_target_capabilities` — list profiles and allowed procedures
- `azure_sql_health_check` — run sp_Blitz to diagnose database health issues
- `azure_sql_blitz_cache` — run sp_BlitzCache for query plan analysis and cached-query performance
- `azure_sql_blitz_index` — run sp_BlitzIndex for index recommendations and table structure review
- `azure_sql_current_incident` — run sp_BlitzFirst to surface immediate blocking, waits, and active problems

### Detail fetching (progressive disclosure)

- `azure_sql_fetch_detail_by_handle` — fetch expanded sections from query tools without re-querying the entire result set

**Default behavior:** All query tools return a summary plus handles to expandable sections. Agents that need full detail can drill down on demand; agents that only need summaries avoid re-running expensive procedures.

See `docs/mcp-tools.md` for interaction patterns and when to use progressive disclosure.

## Security and responsibility boundaries

Blitz Bridge enforces read-only intent, allowlisted procedures, and profile-scoped access, but you own credential lifecycle, token distribution, and target authorization decisions.

See `SECURITY.md` for vulnerability reporting, guarantees, and non-goals.

## Additional docs

- Product requirements: `docs/PRD.md`
- Implementation plan/work tracking: `docs/implementation-work-items.md`
- Architecture overview: `docs/architecture.md`
- Azure deployment guide: `docs/deployment-azure.md`
- SQL least-privilege role script: `docs/sql/blitz-bridge-role.sql`

