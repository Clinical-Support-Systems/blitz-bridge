# Project Context

- **Owner:** Kori Francis
- **Project:** Read-only .NET MCP server for Brent Ozar First Responder Kit diagnostics against preconfigured Azure SQL targets
- **Stack:** .NET, MCP server patterns, Azure SQL, First Responder Kit diagnostics
- **Created:** 2026-04-23T17:59:01Z

## Learnings

Tester initialized with day-1 project context.

### Batch 0: Request Binding Regression Tests (2026-04-23)

- Added binding-regression tests for `AzureSqlDiagnosticTools` covering top-level MCP argument marshalling and nested request fallback/preference behavior in `tests/BlitzBridge.McpServer.Tests`.
- Added HTTP sample contract test validating `tools/call` payload shape with `params.arguments.target` for `azure_sql_target_capabilities`.
- Tests verified by building solution and executing the TUnit binary directly. Direct test host execution is the reliable path when .NET 10 SDK MTP opt-in is not available.
- Regression guardrails at tool boundary catch future signature regressions immediately and verify sample payload alignment is contractual.

### Batch 0: Transport Selection and Stdio Smoke Coverage (2026-04-23)

- Added startup parser tests confirming `--transport stdio`, `--transport http`, and no transport flag defaults to HTTP, with invalid transport values still rejected.
- Added process-level stdio smoke test that launches `BlitzBridge.McpServer` with sample config, sends a minimal `initialize` request over stdin, and asserts a protocol version response for request id `1`.
- Validation remains dependent on direct TUnit executable runs under .NET 10/MTP; `dotnet test` is still unsupported in this SDK path.
- Key harness lesson: current stdio transport expects newline-delimited JSON-RPC input (not `Content-Length` framed LSP payloads), so smoke tests must send a single-line JSON request.
- **Decision merged** → `mcmanus-stdio-tests.md` consolidated to `decisions.md` as Decision 007 (Active)
### Batch 0: HTTP Auth Mode Integration Coverage (2026-04-24)

- Added process-level HTTP integration tests that launch `BlitzBridge.McpServer.exe` and verify `/mcp` auth outcomes by mode: BearerToken returns 401 for missing/wrong headers, returns 200 for correct bearer token, and None mode returns 200 regardless of header.
- Added stdio bypass coverage proving `--transport stdio` initialize handshake still succeeds even when HTTP auth env vars are configured, confirming middleware scope is HTTP `/mcp` only.
- Harness nuance: MCP HTTP endpoint requires `Accept: application/json, text/event-stream`; missing this can produce 406 NotAcceptable and false negatives in auth assertions.
- Validation: rebuilt `BlitzBridge.McpServer.Tests` and executed TUnit binary directly; full suite passed (22/22).
- **Decision created** → `mcmanus-auth-tests.md` written to `.squad/decisions/inbox/` (pending merge)
- **Decision merged** → `mcmanus-auth-tests.md` consolidated to `decisions.md` as Decision 012 (Active)
- **Orchestration logged** → Entry recorded in `.squad/orchestration-log/mcmanus-auth-integration-tests.md`
- **Next:** Implement auth + CORS test matrix expansions as Fenster's middleware evolves; validate process-level endpoint behavior remains consistent.

### Batch 0: WebApplicationFactory Auth Matrix (2026-04-24)

- Reworked HTTP auth integration coverage to use a `WebApplicationFactory<Program>` harness instead of external process bootstrapping, reducing startup flake and keeping auth assertions at app-pipeline boundary.
- Confirmed required matrix behavior on `/mcp`: BearerToken mode returns 401 for missing header and wrong token, returns 200 for correct token, and None mode returns 200 regardless of Authorization header value.
- Kept stdio transport bypass coverage intact (`StdioTransport_BypassesHttpAuthMiddleware_AndStillInitializes`) to ensure HTTP auth configuration does not affect stdio initialize flow.
- Validation: built tests and executed TUnit host directly; full suite passed (22/22, failed 0, skipped 0).

