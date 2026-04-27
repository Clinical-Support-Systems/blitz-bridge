# Blitz Bridge — Claude Project Instructions

Blitz Bridge is a read-only MCP server that exposes a tightly allowlisted Brent Ozar First Responder Kit (FRK) surface over Azure SQL targets. Agents call diagnostic tools; the server enforces profile-scoped access, allowlisted procedures, and read-only intent.

## Where to look first

- Architecture: `docs/architecture.md`
- Product requirements: `docs/PRD.md`
- Tool catalog and progressive-disclosure contract: `docs/mcp-tools.md`, `docs/progressive-disclosure-design.md`, `docs/progressive-disclosure-response-shapes.md`
- Implementation tracking: `docs/implementation-work-items.md`
- Azure deployment: `docs/deployment-azure.md`
- Least-privilege SQL grants: `docs/sql/blitz-bridge-role.sql`
- Security boundaries: `SECURITY.md`

Read the relevant doc before editing — do not infer architecture from code alone.

## Stack and framework decisions

- .NET 10. Solution file is `BlitzBridge.slnx`.
- Aspire AppHost orchestrates the server (`src/BlitzBridge.AppHost`). Service defaults live in `src/BlitzBridge.ServiceDefaults`.
- MCP transport via `ModelContextProtocol.AspNetCore` (HTTP) and `WithStdioServerTransport()` (stdio). See `src/BlitzBridge.McpServer/Program.cs`.
- Tools are declared with `[McpServerTool, Description(...)]` in `src/BlitzBridge.McpServer/Tools/AzureSqlDiagnosticTools.cs`. Validation and orchestration live in `Services/FrkProcedureService.cs`.
- Tests use TUnit (run with `dotnet run -c Release` from the test project, or `dotnet test` with the new MTP test runner — VSTest is not supported on .NET 10).
- JSON: `System.Text.Json` only.
- Mapping: hand-written or Mapperly. No AutoMapper.
- Logging: minimum level `None` for stdio transport (stdout is reserved for JSON-RPC).

## Behavior contract

- All query tools return summary-plus-handles; expanded sections fetched via `azure_sql_fetch_detail_by_handle`. Preserve this shape — agents depend on it.
- Target resolution lives in `FrkProcedureService.ResolveTarget`. When a single enabled profile exists, blank target auto-resolves; ambiguous/missing targets raise `ArgumentException` listing available profiles.
- `--transport stdio` requires `--config <path>`. Default config path: `%APPDATA%\blitz-bridge\profiles.json` on Windows, `~/.config/blitz-bridge/profiles.json` elsewhere.
- HTTP transport uses bearer auth via `McpHttpAuthMiddleware`. CORS denies by omission unless explicitly allowed.

## Workflow expectations

- Commit after each meaningful change with a focused message describing the why.
- For non-trivial features, write a spec to `docs/` first and implement from the spec.
- Do not introduce dependencies without a clear need; this server runs in restricted environments.
- Do not add tools, parameters, or response fields without updating `docs/mcp-tools.md` and `docs/progressive-disclosure-response-shapes.md`.
- Do not weaken the allowlist (procedures, databases, transports). Read `SECURITY.md` before changing anything in `Middleware/`, `Configuration/SqlTargetOptions*`, or `Services/SqlExecutionService.cs`.

## Client-config examples

`examples/client-configs/` — Claude Desktop, Claude Code, Cursor, VS Code, hosted/HTTP, Python SDK sample. Update all relevant entries together when transport semantics change.
