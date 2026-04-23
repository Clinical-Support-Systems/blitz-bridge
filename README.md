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

## Try it in 5 minutes

Want to test Blitz Bridge without installing? Use Docker Compose:

```bash
cd samples/docker-compose-demo
cp .env.example .env
# Edit .env with your local demo SA password + token
docker compose up --build
```

Then test the MCP endpoint:

```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer your-demo-token-here" \
  -d '{"jsonrpc": "2.0", "id": 1, "method": "tools/list", "params": {}}'
```

See [samples/docker-compose-demo/README.md](samples/docker-compose-demo/README.md) for details.

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
- `Enabled` — gates this profile's validation at startup; profiles with `Enabled=false` are skipped and not exposed to MCP tools
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

## Hosting with auth

When deploying Blitz Bridge to a remote or shared environment, you can layer authentication on top of the MCP HTTP transport. This section explains the `BlitzBridge:Auth` configuration shape, token precedence, and integration patterns for Claude Desktop, Claude Code, and other MCP clients.

### BlitzBridge:Auth configuration

Add an `Auth` section to your `appsettings.json` (HTTP mode only; stdio mode ignores it):

```json
{
  "BlitzBridge": {
    "Auth": {
      "Mode": "BearerToken",
      "Tokens": [ "your-secret-token-here" ]
    }
  }
}
```

**Configuration shape:**

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `Auth.Mode` | string | `"None"` | `"None"` disables HTTP auth; `"BearerToken"` requires an `Authorization: Bearer <token>` header on `/mcp` HTTP requests. |
| `Auth.Tokens` | string[] | `[]` | Bearer-token allowlist. At least one token is required when mode is `BearerToken`. |

**Mode semantics:**

- **None**: HTTP `/mcp` requests are not token-gated.
- **BearerToken**: `/mcp` requires a bearer token and returns `401 Unauthorized` when missing or invalid.

### Token source and precedence

Tokens can come from two sources; the precedence order is:

1. **Environment variable** (highest priority): `BLITZBRIDGE_AUTH_TOKENS` (semicolon-separated)
2. **Config file**: `BlitzBridge:Auth:Tokens` in `appsettings.json`

If `BLITZBRIDGE_AUTH_TOKENS` is set with at least one non-empty token, it overrides config tokens. This allows you to:

- Keep sensitive tokens out of config files
- Override tokens at deployment time without recompiling
- Use orchestration platforms (Aspire, Docker, Kubernetes) to inject secrets

**Example: Aspire parameter binding**

In your `AppHost.cs`, define a secret parameter:

```csharp
var authTokens = builder.AddParameter("auth-tokens", secret: true);

var mcp = builder
    .AddProject<Projects.BlitzBridge_McpServer>("blitz-bridge")
    .WithEnvironment("BLITZBRIDGE_AUTH_TOKENS", authTokens);
```

Then run:

```bash
dotnet run -- --parameters auth-tokens="token-a;token-b"
```

### Sample hosted MCP client configuration

#### Claude Desktop with Authorization header

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "blitz-bridge": {
      "command": "curl",
      "args": [
        "-X", "POST",
        "-H", "Authorization: Bearer YOUR_TOKEN_HERE",
        "-H", "Content-Type: application/json",
        "-d", "@-",
        "http://your-host:5000/mcp"
      ]
    }
  }
}
```

Alternatively, if your Blitz Bridge is exposed on a system with environment variables, you can use:

```json
{
  "mcpServers": {
    "blitz-bridge": {
      "command": "sh",
      "args": [
        "-c",
        "curl -X POST -H \"Authorization: Bearer $BLITZ_TOKEN\" -H \"Content-Type: application/json\" -d @- http://your-host:5000/mcp"
      ]
    }
  }
}
```

Set `BLITZ_TOKEN` in your shell environment:

```bash
export BLITZ_TOKEN="your-secret-token-here"
```

#### Claude Code and Cursor

Both Claude Code and Cursor support similar patterns. Use the environment variable approach if your MCP client supports shell execution, or hard-code the token if the connection is local and trusted.

Example for Claude Code workspace settings:

```json
{
  "mcpServers": {
    "blitz-bridge": {
      "command": "sh",
      "args": [
        "-c",
        "curl -X POST -H \"Authorization: Bearer $BLITZ_TOKEN\" -H \"Content-Type: application/json\" -d @- http://your-host:5000/mcp"
      ]
    }
  }
}
```

### Stdio mode and local auth

**Stdio mode (used with `blitz-bridge --transport stdio`) does not support HTTP auth.** This is by design:

- Stdio transport communicates via JSON-RPC over standard input/output, not HTTP.
- All config is loaded from the local filesystem or environment at startup.
- If you run `blitz-bridge --transport stdio --config profiles.json`, there is no network listener; auth is not required.

This means local MCP clients (Claude Desktop on your workstation) can access Blitz Bridge without credentials when using stdio mode.

### CORS and public endpoints

HTTP CORS is now configuration-driven under `BlitzBridge:Cors`:

```json
{
  "BlitzBridge": {
    "Cors": {
      "AllowAnyOrigin": false,
      "AllowedOrigins": [
        "https://claude.ai",
        "https://your-internal-app.example.com"
      ]
    }
  }
}
```

**Behavior:**

- Default is restrictive (`AllowAnyOrigin=false`, `AllowedOrigins=[]`), so no CORS origin is granted unless you opt in.
- Set `AllowedOrigins` for explicit production allowlists.
- `AllowAnyOrigin=true` is supported as an explicit opt-in for local development only.

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

