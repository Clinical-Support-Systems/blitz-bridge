![Blitz Bridge icon](icon_medium.png)

# Blitz Bridge

Blitz Bridge is a read-only .NET MCP server that lets agents run Brent Ozar First Responder Kit diagnostics against preconfigured Azure SQL targets.

This project is opinionated around an existing Azure SQL setup where FRK procedures are already installed and we want an agent-friendly bridge rather than arbitrary SQL execution.

## Install

### CLI Tool

Install Blitz Bridge as a global .NET tool:

```bash
dotnet tool install -g BlitzBridge.McpServer
```

### Claude Desktop

Add the following to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "blitz-bridge": {
      "command": "blitz-bridge",
      "args": ["--transport", "stdio"]
    }
  }
}
```

Then configure targets in your system config file (see [Configuration](#configuration)):

```bash
blitz-bridge --transport stdio --config path/to/profiles.json
```

### Claude Code

Use similar configuration in your Claude Code workspace settings:

```json
{
  "mcpServers": {
    "blitz-bridge": {
      "command": "blitz-bridge",
      "args": ["--transport", "stdio", "--config", "path/to/profiles.json"]
    }
  }
}
```

### Cursor

Configure in `.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "blitz-bridge": {
      "command": "blitz-bridge",
      "args": ["--transport", "stdio", "--config", "path/to/profiles.json"]
    }
  }
}
```

## Configuration

Blitz Bridge reads target profiles from a JSON config file:

- Stdio mode: `blitz-bridge --transport stdio --config path/to/profiles.json`
- Stdio default path when `--config` is omitted:
  - Windows: `%APPDATA%\blitz-bridge\profiles.json`
  - Linux/macOS: `~/.config/blitz-bridge/profiles.json`
- HTTP mode ignores `--config` and continues to use appsettings and environment variable binding.

### Config file structure

```json
{
  "SqlTargets": {
    "Profiles": {
      "primary-sql-target": {
        "ConnectionString": "Server=tcp:...;Database=DBAtools;Authentication=Active Directory Default;Encrypt=True;",
        "AllowedDatabases": [ "AppDb" ],
        "AllowedProcedures": [ "sp_Blitz", "sp_BlitzCache", "sp_BlitzFirst", "sp_BlitzIndex", "sp_BlitzLock", "sp_BlitzWho" ],
        "Enabled": true,
        "CommandTimeoutSeconds": 60,
        "AiMode": 2
      }
    }
  }
}
```

**Key fields:**

- `ConnectionString` — Azure SQL connection string (stored server-side, never exposed)
- `AllowedDatabases` — restricts which databases this profile may analyze (optional; if omitted, no restriction)
- `AllowedProcedures` — allowlist of FRK procedures available to agents
- `CommandTimeoutSeconds` — query execution timeout (default: 60)
- `AiMode` — AI participation level: `0` (off), `2` (FRK prompts only), `1` (FRK direct AI calls, requires setup)

### Local development without Aspire

For quick local testing with the CLI:

1. Create a config file with your target profile
2. Run: `blitz-bridge --transport stdio --config your-profiles.json`
3. Test via HTTP using the included `.http` file or direct MCP tool invocations

## Current MCP tools

- `azure_sql_target_capabilities`
  - Returns installed FRK procedures, current execution database, allowlisted databases, and AI readiness hints.
- `azure_sql_blitz_cache`
  - Wraps `sp_BlitzCache` with supported `SortOrder` values of `executions`, `cpu`, `reads`, and `duration`.
  - Surfaces FRK AI output such as generated prompts or direct advice when present.
- `azure_sql_blitz_index`
  - Wraps single-table `sp_BlitzIndex` analysis and surfaces FRK AI output when present.
- `azure_sql_health_check`
  - Wraps `sp_Blitz`.
- `azure_sql_current_incident`
  - Wraps `sp_BlitzFirst`.

## Design constraints

- No arbitrary T-SQL execution
- No write operations or schema changes through MCP
- Targets are configured server-side by profile name
- Optional database allowlists can restrict which databases a profile may analyze
- Procedures are limited to an allowlisted FRK surface

## AI support

`sp_BlitzCache` and `sp_BlitzIndex` can return FRK-crafted AI prompts and, when configured in the database context, direct AI advice.

- `AiMode = 0`
  - No AI-specific FRK parameters.
- `AiMode = 2`
  - Returns the FRK-generated prompt when the installed procedure version supports it.
- `AiMode = 1`
  - Attempts direct AI provider calls through FRK, which requires credentials and role setup in the execution database context.

Reference: [Using AI with the First Responder Kit](https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit/blob/dev/Documentation/Using_AI.md)

## Notes

- `sp_BlitzCache @AI = 2` is especially useful for agents because the generated prompt is already tailored to the query, plan, and performance data.
- `sp_BlitzIndex` is modeled here as a focused single-table tool because that is the most useful path for agent-guided tuning.
- The HTTP example file in `src/BlitzBridge.McpServer/BlitzBridge.McpServer.http` includes a capability probe request you can start from.

## Hosted deployment

This repo now includes:

- `src/BlitzBridge.AppHost`
  - Local Aspire orchestrator for running the MCP server with parameterized SQL target settings.
- `src/BlitzBridge.ServiceDefaults`
  - Shared health, telemetry, service discovery, and OpenTelemetry defaults.

The AppHost treats the SQL target as an external dependency. It does not provision or mutate the database; it injects the MCP server's profile configuration through Aspire parameters so you can:

- run the server locally from the AppHost dashboard
- keep the connection string outside the repo as a secret parameter
- standardize local config before deploying to Azure Container Apps

Default AppHost parameters live in [src/BlitzBridge.AppHost/appsettings.json](/D:/Github/blitz-bridge/src/BlitzBridge.AppHost/appsettings.json:1). The connection string is modeled as a secret parameter and should be supplied through the Aspire run experience or user secrets.

