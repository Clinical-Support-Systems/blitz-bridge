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
