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
