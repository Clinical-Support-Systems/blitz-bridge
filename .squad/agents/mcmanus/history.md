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

### Batch 0: Docker Compose Demo Verification (2026-04-24)

- Validated fresh-machine compose behavior for `samples/docker-compose-demo` by repeatedly running `docker compose down --volumes --remove-orphans` then `docker compose up --build -d`, confirming stack starts from clean state with no pre-existing SQL data.
- Confirmed startup ordering guards: `sql-init` now waits for SQL readiness, installs FRK, applies seed, and writes `dbo.BlitzBridgeInitComplete`; SQL healthcheck requires `DBAtools`, `sp_Blitz`, and `BlitzBridgeInitComplete`; `blitzbridge` depends on SQL health + successful `sql-init`.
- Added reusable verification artifact `samples/docker-compose-demo/scripts/verify-demo.ps1` to validate compose config, clean startup, MCP `tools/list` tool inventory, and `azure_sql_health_check` call path against `demo-sql-target`.
- Identified and mitigated race and portability issues: compose command argument handling in PowerShell, CRLF line ending breakage in `init-sql.sh`, and over-heavy default seed script; default seed now uses `seed-test.sql` for deterministic readiness checks.
- Found runtime coupling issue where FRK execution could fail with SQL `QUOTED_IDENTIFIER` session mismatch in containerized path; fixed by setting session options before SQL command execution in `SqlExecutionService`.
- Validation after changes: `dotnet build BlitzBridge.slnx` passed, test project build + direct TUnit host run passed (`22/22`), compose stack reaches healthy SQL + running bridge state with init completion marker.

### Batch 0: Progressive Disclosure Testability Review (2026-04-27)

- Reviewed `docs/progressive-disclosure-design.md` through a testability lens only; no `src/` changes made.
- Verdict: approve Phase 1 summary-plus-handles design for testing, provided handles remain section-level, deterministic, versioned, and derived from normalized request parameters rather than cache state or runtime row values.
- Key fixture conclusion for D-3: deterministic FRK stub handles are feasible for section-level drill-downs because parent and detail contracts can share a canonical request-tuple-derived handle.
- Phase 2 warning recorded: row-level handles are not test-approved yet for `sp_BlitzCache` (needs stable `QueryHash`) or `sp_BlitzFirst` (snapshot `CheckDate`, no stable finding ID).
- Key paths: `docs/progressive-disclosure-design.md`, `docs/progressive-disclosure-handle-audit.md`, `docs/implementation-work-items.md`, `samples/docker-compose-demo/sql/frk-install.sql`.
- **Decision merged** → `mcmanus-progressive-disclosure-review.md` consolidated to `decisions.md` as Decision 017 (Active)
- **Orchestration logged** → Entry recorded in `.squad/orchestration-log/2026-04-27T15-04-23Z-mcmanus.md`



### Batch 0: Progressive Disclosure Phase 2 Coverage (2026-04-27)

- Added progressive-disclosure contract coverage in `tests/BlitzBridge.McpServer.Tests/AzureSqlDiagnosticToolsBindingTests.cs` for emitted handles, detail fetch success, and explicit-dispatch failures (`unknown_parent_tool`, `unknown_kind`, malformed handle, mismatched dispatch metadata).
- Verified the FRK fixture path end-to-end: a summary `azure_sql_blitz_cache` response returns section handles, and drilling into a returned `warning_glossary` handle yields a non-empty detail payload.
- Pinned the McManus guardrail in tests: `sp_BlitzCache` and `sp_BlitzFirst` stay section-scoped for progressive disclosure; no row-level identity assumptions are asserted.
- Response/detail contracts now rely on `AzureSqlDetailHandle`, `AzureSqlFetchDetailByHandleRequest`, `AzureSqlFetchDetailByHandleResponse`, and `ProgressiveDisclosureHandleCodec` under `src/BlitzBridge.McpServer`.
- Validation: `dotnet build BlitzBridge.slnx` passed; test project build plus direct TUnit host run passed (`37/37`).

### Batch 3.3: Phase 2 Session Completion & Orchestration (2026-04-28)

- All phase 2 test contracts passing: handle emission from parent tools, summary-to-detail flow validation, explicit dispatch error handling, WebApplicationFactory auth matrix (4 mode/header combinations), docker-compose-demo verification
- Test suite final state: 37/37 passed (handle dispatch, auth matrix, docker demo smoke tests, backward compatibility)
- **Decisions merged** → `mcmanus-progressive-disclosure-phase2.md` + `mcmanus-compose-verification.md` + `mcmanus-waf-auth-tests.md` consolidated to decisions.md as Decisions 027, 026, 028 (Active)
- **Orchestration logged** → `2026-04-27T15-45-41-mcmanus.md` recorded

