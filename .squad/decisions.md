# Squad Decisions

## Active Decisions

### Decision 001: v1 PRD Bootstrap (2026-04-23)

**Author:** Verbal  
**Status:** Active  

Five core MCP tools chosen for MVP: azure_sql_target_capabilities, azure_sql_blitz_cache, azure_sql_blitz_index, azure_sql_health_check, azure_sql_current_incident. Read-only enforcement enforced at connection validation. Procedures and databases controlled via allowlists per profile. AiMode (0/1/2) configuration lever for AI participation cost control. 5-week milestone plan with parallelizable work: Week 1–2 core MCP + sp_BlitzCache, Week 2–3 remaining FRK tools + auth, Week 3–4 observability + health, Week 4 Aspire, Week 5 testing + release. Open questions assigned to owners. Full spec in `docs/PRD.md`.

**Related:** `docs/PRD.md`, `.squad/decisions/inbox/verbal-prd-bootstrap.md` (merged)

### Decision 002: MCP Tool Request Binding Compatibility (2026-04-23)

**Author:** Fenster  
**Status:** Active

The MCP tool binder treats complex request DTO parameters as a single top-level argument named after the parameter (for example, `request`). This caused `tools/call` payloads that passed flattened fields (for example, `{"target":"..."}`) to fail with a missing required `request` argument.

**Resolution:** Updated Azure SQL tool signatures to accept both flattened scalar arguments and an optional DTO argument, then normalize into the existing request models before calling service logic. This keeps backward compatibility with existing wrapper payloads while making flattened MCP invocations work reliably across clients and `.http` test flows.

**Related:** `src/BlitzBridge.McpServer/Tools/AzureSqlDiagnosticTools.cs`, `.squad/orchestration-log/fenster-request-binding-fix.md`

### Decision 003: Request Binding Regression Coverage (2026-04-23)

**Author:** McManus  
**Status:** Active

Prior exception in `azure_sql_target_capabilities` was tied to request-argument binding shape differences between top-level MCP arguments and nested request objects.

**Decision:** Add regression tests at the tool boundary (`AzureSqlDiagnosticTools`) that assert:
- top-level `target` is accepted and forwarded when request object is absent,
- nested request target wins when both are supplied,
- top-level scalar arguments marshal correctly for representative tools (`azure_sql_blitz_cache`), and
- the HTTP sample payload keeps `params.arguments.target` shape for `azure_sql_target_capabilities`.

**Why:** Keeps the guardrail close to where marshalling occurs, catches future signature regressions quickly, and verifies the sample payload remains aligned with expected binding behavior.

**Related:** `tests/BlitzBridge.McpServer.Tests/`, `.squad/orchestration-log/mcmanus-request-binding-test.md`

### Decision 004: PRD Decomposition & Execution Plan (2026-04-23)

**Author:** Keaton (Lead)  
**Status:** Active

1. **Multi-region targets:** NOT in v1. Single-region only. Multi-region adds config complexity with no MVP value. Defer to v2.

2. **Database enumeration in `target_capabilities`:** Config-level only. Returns allowlisted databases and installed FRK procedures — not schema counts, table sizes, or runtime enumeration. This keeps the tool fast and avoids permission creep.

3. **Result pagination:** MaxRows cap (existing approach). No cursor-based pagination for v1.

4. **AiMode on profile config:** `AiMode` must be a property on `SqlTargetProfile` (default: 2). Tool requests can override per-call, but the profile is the source of truth.

5. **Test fixture critical path:** The FRK stub fixture gates all integration tests. Must prioritize in Week 1 alongside FRK parameter validation.

6. **Batch execution model:** Three batches over 3 weeks. Batch 1 is almost fully parallelizable. Reviewer gates at each batch boundary.

**Impact:** All team members should read `docs/implementation-work-items.md` for assigned tasks, dependencies, and acceptance criteria.

**Related:** `docs/PRD.md`, `docs/implementation-work-items.md`

### Decision 005: Stdio Transport Architecture & Guardrails (2026-04-24)

**Author:** Keaton (Lead)  
**Status:** Active  
**Scope:** `--transport stdio|http` flag, dotnet global tool packaging, `--config` path, Aspire HTTP zero-regression

BlitzBridge needs a standalone MCP server distribution path so it can be consumed by any MCP client (Claude Desktop, VS Code Copilot Chat, Cursor, etc.) without requiring Aspire. The existing HTTP/Aspire path must remain byte-for-byte identical in behavior.

**Key Decisions:**

1. **Two mutually exclusive hosting paths** via `--transport {stdio|http}` flag (default: `http`):
   - HTTP path: Uses `WebApplication.CreateBuilder`, `WithHttpTransport`, CORS, `/health`, `/mcp`, Aspire service defaults — **unchanged from today**
   - Stdio path: Uses `Host.CreateDefaultBuilder` with `WithStdioServerTransport()`, no HTTP listener, no CORS, no `/health`, no `/mcp`, no Aspire defaults

2. **Configuration for stdio mode:**
   - If `--config <path>` supplied: load that JSON file
   - If omitted: resolve OS-appropriate default (Windows: `%APPDATA%\blitz-bridge\profiles.json`, macOS/Linux: `~/.config/blitz-bridge/profiles.json`)
   - If neither exists: fail fast with clear stderr error, exit non-zero (`2`)

3. **Global tool packaging:** Add `<PackAsTool>true</PackAsTool>` and `<ToolCommandName>blitz-bridge</ToolCommandName>` to enable `dotnet tool install -g BlitzBridge.McpServer`

4. **Shared services:** Both paths share tool registration, DI services, and `SqlTargetOptions` config binding

5. **HTTP path immutability:** AppHost.cs untouched; `--config` ignored in HTTP mode; `builder.AddServiceDefaults()` only in HTTP path

**No-Regression Criteria (must-pass):**
- HTTP/Aspire path: Zero behavioral change (verify via existing `.http` tests, `/health` endpoint, CORS headers, env var binding)
- Stdio path: Validates config availability, no HTTP listener, clean stdout (logging to stderr only)
- Global tool: `dotnet pack` succeeds, `dotnet tool install -g` works without Aspire SDK

**Related:** Keaton's charter item, `.squad/decisions/inbox/keaton-stdio-transport-guardrails.md` (merged)

### Decision 006: SqlTargets Config Path & Validation Compatibility (2026-04-24)

**Author:** Hockney  
**Status:** Active

The existing `SqlTargets:Profiles:<name>` appsettings binding shape must work identically with standalone `--config` JSON files. No schema fork.

**Key Decisions:**

1. **Unified schema:** Standalone JSON and existing appsettings both bind to `SqlTargetOptions`/`SqlTargetProfile`; `AiMode` explicitly modeled on profile (default: `2`)

2. **Path + startup semantics:**
   - Stdio mode enforces explicit config availability with fail-fast semantics
   - Default OS-path resolution per-platform (Windows: `%APPDATA%\blitz-bridge\profiles.json`, macOS: `~/Library/Application Support/blitz-bridge/profiles.json`, Linux: `~/.config/blitz-bridge/profiles.json`)
   - Missing config in stdio mode: clear startup error to stderr, exit non-zero (`2`)

3. **Centralized validation** via `SqlTargetOptionsValidator` on enabled profiles:
   - Connection string required and parseable
   - `ApplicationIntent=ReadOnly` required
   - `CommandTimeoutSeconds` must be 1..600
   - `AiMode` must be 0/1/2
   - `AllowedDatabases` and `AllowedProcedures` cannot contain blanks
   - `AllowedProcedures` restricted to known FRK surface

4. **Runtime safety:** SQL connection construction explicitly forces `ApplicationIntent=ReadOnly` and `MultipleActiveResultSets=false` even if config text drifts

**Related:** `.squad/decisions/inbox/hockney-config-paths.md` (merged)

### Decision 007: Stdio Transport Test Posture (2026-04-24)

**Author:** McManus  
**Status:** Active

The stdio transport introduces a startup path difference that can fail before tool-level tests execute. Need deterministic coverage for transport selection and actual stdio wiring.

**Key Decisions:**

1. **Startup parser boundary tests:** Lock in transport selection parsing:
   - Explicit `--transport stdio`
   - Explicit `--transport http`
   - Default transport when omitted (`http`)
   - Invalid transport rejection

2. **Smoke-level stdio process test:** Launch built MCP server executable with `--transport stdio --config <samples/profiles.json>`, write minimal `initialize` JSON-RPC to stdin, validate `id: 1` response containing `result.protocolVersion`

3. **Test coverage combination:** Parser-level tests catch transport regression quickly; process-level smoke test proves host wiring + stdio integration end-to-end. Fast failure signals + real runtime validation.

4. **Format compatibility:** Current stdio handling successfully parses newline-delimited JSON requests for `initialize`; smoke test pinned to single-line JSON to match observed behavior

**Related:** `.squad/decisions/inbox/mcmanus-stdio-tests.md` (merged)

### Decision 008: Stdio Transport + Tool Packaging Implementation Notes (2026-04-24)

**Author:** Fenster  
**Status:** Active

Implemented dual transport startup for `BlitzBridge.McpServer` with default HTTP preserved and explicit stdio mode for MCP clients. Packaged as global .NET tool (`blitz-bridge`).

**Key Decisions:**

1. **Transport selection:** `--transport stdio|http` parsed at startup; default remains `http` for safety with existing Aspire/AppHost workflows

2. **Config sourcing:** `--config <path>` honored for stdio mode. If omitted, stdio resolves OS-default profile path (Windows: `%APPDATA%\blitz-bridge\profiles.json`, Linux/macOS: `~/.config/blitz-bridge/profiles.json`)

3. **Fail-fast guarantee:** Missing stdio config emits clear stderr error, exits non-zero (`2`)

4. **Stdout hygiene:** Stdio mode clears log providers and sets logging level to none to avoid JSON-RPC stream contamination

5. **Global tool packaging:** Enabled `PackAsTool`, `ToolCommandName` in `BlitzBridge.McpServer.csproj` plus README inclusion in package output

**Validation outcomes:**
- Existing backend tests pass via test executable
- `dotnet pack` succeeds for `BlitzBridge.McpServer`
- Local global-tool install/update from nupkg succeeds
- `blitz-bridge --transport stdio --config ./samples/profiles.json` initialize handshake verified
- Missing stdio config returns non-zero exit + clear stderr error

**Note:** HTTP path keeps existing defaults and treats `--config` as ignored to prevent hidden behavior shifts

**Related:** `.squad/decisions/inbox/fenster-stdio-tooling.md` (merged)

### Decision 009: CLI Install & Configuration Documentation (2026-04-24)

**Author:** Verbal  
**Status:** Active

README updated to lead with `dotnet tool install -g BlitzBridge` as primary installation path, with configuration examples for Claude Desktop, Claude Code, and Cursor using stdio transport and `--config` / `BLITZ_CONFIG` environment variable patterns.

**Key Decisions:**

1. **User-friendly entry point:** `dotnet tool install` as primary installation for .NET-familiar developers

2. **Standardized MCP patterns:** Stdio transport is de facto MCP standard for lightweight clients; documented examples for Claude Desktop, Claude Code, Cursor

3. **Configuration flexibility:** Dual support for `--config` CLI flag and `BLITZ_CONFIG` environment variable for explicit and implicit workflows (Docker, CI/CD, wrapped scripts)

4. **Aspire remains available:** Moved to "Hosted deployment" section — still available for teams wanting parameterized secrets and local dev dashboards, but signals it's not the default path

5. **Design clarity first:** Documenting flags (`--transport`, `--config`) before implementation sets clear expectations and ensures architectural alignment

**README changes:**
- New "Install" section with subsections: CLI tool, Claude Desktop, Claude Code, Cursor
- New "Configuration" section: config file structure, CLI flags, env vars
- New "Local development without Aspire" subsection
- "Hosted deployment" heading (renamed from "Aspire orchestration")
- Removed duplicate config examples

**Open questions:** When will `--transport` and `--config` be implemented? Config validation error logging strategy? Security review for config file handling?

**Related:** `.squad/decisions/inbox/verbal-stdio-docs.md` (merged)

### Decision 010: Hosting with Auth Documentation (2026-04-24)

**Author:** Verbal  
**Status:** Active

README updated with "Hosting with auth" section documenting Bearer token authentication for HTTP deployments. Configuration shape: `BlitzBridge:Auth` with `Mode` and `BearerToken` properties. Token precedence: `BLITZ_AUTH_BEARER_TOKEN` environment variable > config file (backward compatible when auth is disabled).

**Key Points:**

1. Bearer tokens are the MCP HTTP standard for client auth; config is extensible for future modes (API Key, JWT)
2. Token precedence allows secure injection in orchestrated deployments (Docker, Kubernetes, Aspire)
3. MCP clients handle Authorization headers via their own config; server validates tokens
4. Stdio mode remains auth-free (no network, no credentials needed for local usage)
5. CORS allowlist documented: permissive for dev (`AllowAnyOrigin`), explicit restrictions for production

**Implementation notes (deferred):**
- Server-side validation of Bearer tokens on `/mcp` requests
- Config binding via `IOptions<BlitzBridgeAuthOptions>` with env var override
- Return 401 Unauthorized for invalid/missing tokens
- Backward compatibility: `Auth.Enabled = false` (default) keeps existing behavior

**Related:** `README.md` - "Hosting with auth" section, `.squad/orchestration-log/verbal-hosting-auth-docs.md`

### Decision 011: Auth Mode Architecture — Composability with Entra ID / Easy Auth (2026-04-24)

**Author:** Keaton (Lead)  
**Status:** Active

Confirmed auth architecture supports future providers (Entra ID, Easy Auth) without rework. **Blocking findings:** Two options classes (`HttpAuthOptions`, `BlitzBridgeAuthOptions`) create binding ambiguity. Must consolidate before implementation.

**Key Decisions:**

1. **Consolidate to one class:** Delete `HttpAuthOptions.cs`; keep and evolve `BlitzBridgeAuthOptions` with public visibility and string-based Mode
2. **String-based Mode with startup validation:** Mode values = `"None"`, `"Bearer"`, `"EntraId"` (future), `"EasyAuth"` (future). Validate at startup; fail-fast on unknown modes
3. **Mode controls ASP.NET scheme registration:** Each mode maps to standard ASP.NET Core authentication (Bearer = custom handler, Entra = `AddJwtBearer()`, Easy Auth = header-forwarding handler). No custom middleware needed
4. **Auth middleware insertion point fixed:** `UseCors()` → `UseAuthentication()` → `UseAuthorization()` → `MapMcp("/mcp")`. Stdio has no HTTP pipeline
5. **Drop `Enabled` from config:** Use `Mode = "None"` as sole "auth disabled" signal. Remove `Auth.Enabled` from README
6. **Align environment variable naming:** Standardize on `BLITZBRIDGE_AUTH_TOKENS` (matches code constant)
7. **Profile-level auth is v2 seam:** When client identity arrives (Entra tokens carry claims), extend `SqlExecutionService.GetTargetContext()` for profile filtering. No refactoring needed now

**Blockers:**
- **Fenster:** Delete `HttpAuthOptions.cs` before wiring auth
- **Verbal:** README must drop `Auth.Enabled`, update env var to `BLITZBRIDGE_AUTH_TOKENS`
- **McManus:** Auth tests use standard `WebApplicationFactory` pattern

**Related:** `src\BlitzBridge.McpServer\Configuration\HttpAuthOptions.cs` (to delete), `src\BlitzBridge.McpServer\Configuration\BlitzBridgeHttpOptions.cs`, `src\BlitzBridge.McpServer\Program.cs`

### Decision 012: Auth Mode Integration Test Posture (2026-04-24)

**Author:** McManus  
**Status:** Active

Auth behavior (transport selection, token validation) must be tested at process boundary, not unit tests alone. Stdio transport must remain unaffected by HTTP auth configuration.

**Test Coverage:**
- BearerToken mode + no Authorization header => 401 Unauthorized
- BearerToken mode + wrong bearer token => 401 Unauthorized
- BearerToken mode + correct bearer token => 200 OK
- None mode => 200 OK regardless of Authorization header
- Stdio `initialize` RPC successful even when HTTP auth env vars set

**Implementation notes:**
- Launch built MCP server executable; write JSON-RPC to stdin; validate response
- HTTP MCP requests must include `Accept: application/json, text/event-stream` header (omission can yield 406 masking auth behavior)
- Middleware/auth behavior best validated at host boundary (catches pipeline-order regressions unit tests miss)

**Related:** `.squad/orchestration-log/mcmanus-auth-integration-tests.md`

### Decision 013: HTTP Auth Hardening + CORS Allowlist Defaults (2026-04-24)

**Author:** Fenster  
**Status:** Active

Implemented HTTP auth middleware and CORS hardening for broader hosting scenarios. Bearer token auth validates against configured allowlist; CORS defaults restrictive to prevent accidental exposure.

**Key Implementation Details:**

1. **Auth config contract:** Mode (None|BearerToken) + Tokens allowlist; constant-time comparison for token validation
2. **Token precedence:** `BLITZBRIDGE_AUTH_TOKENS` (semicolon-separated) env var wins over config file; backward compatible when disabled
3. **Auth failure logging:** Middleware logs source IP + truncated SHA-256 hash prefix of presented token (never raw token)
4. **CORS configuration:** `BlitzBridge:Cors:AllowedOrigins` allowlist section; `AllowAnyOrigin` retained as explicit dev-only opt-in
5. **Stdio isolation:** Auth middleware only in HTTP app pipeline; stdio path unaffected

**Validation outcomes:**
- Auth middleware integrated; CORS policy respects configuration
- Env var token override works for orchestrated deployments
- Accidental exposure risk reduced for shared environments
- Backward compatibility maintained

**Related:** `.squad/orchestration-log/fenster-auth-cors-hardening.md`

### Decision 014: Progressive Disclosure Handle Audit — FRK Procedures (2026-04-27)

**Author:** Hockney  
**Status:** Active

Comprehensive audit of row identifiers ("handles"), server-side narrowing capability, and response-set shape for all four FRK procedures wrapped in BlitzBridge.

**Key Findings:**

| Procedure | Primary Handle | Stability | Server Narrowing |
|-----------|---|---|---|
| sp_Blitz | CheckID (+ Priority, FindingsGroup) | ✅ Stable | ❌ None (client re-run + filter) |
| sp_BlitzCache | QueryHash (+ DatabaseName) | 🟡 Moderate* | 🟡 Partial (@Top/@SortOrder only) |
| sp_BlitzIndex | IndexName (scoped to table) | ✅ Stable | ✅ Full (required params) |
| sp_BlitzFirst | WaitType / Finding | 🟡 Snapshot-scoped | ❌ None (point-in-time only) |

*QueryHash not exposed in current response; tracked via QueryText

**Response Model Assessment:**

Current compacted arrays + optional `IncludeVerboseResults` already implement correct progressive disclosure patterns. No breaking changes required for Phase 1.

**Phase 1 Recommendation:** No code changes needed. Design is sound and ready for additive progressive disclosure.

**Phase 2+ Opportunities:**
- Expose QueryHash in BlitzCache responses
- Add compaction metadata (`isTruncated`, `totalRows`)
- Audit sp_BlitzLock for similar patterns

**Related:** `docs/progressive-disclosure-handle-audit.md`, `src/BlitzBridge.McpServer/Services/FrkResultMapper.cs`

### Decision 015: Progressive Disclosure Responses & Detail Tool Design (2026-04-27)

**Author:** Fenster  
**Status:** Active

Recommend generic drill-down tool pattern over per-parent detail tools for progressive disclosure responses.

**Key Decisions:**

1. **One generic detail tool:** `azure_sql_fetch_detail_by_handle` with required inputs: `target`, `parentTool`, `kind`, `handle`
2. **Section-level handles for Phase 1:** Handles represent `findings`, `queries`, `missing_indexes`, `ai_prompt`, etc. — not individual rows
3. **`kind` values strictly validated per `parentTool`** — prevents cross-tool handle confusion
4. **Detail responses echo `parentTool`, `kind`, `handle`** — maintains request-response traceability
5. **Deprecate `IncludeVerboseResults`** — do not repurpose; keep for backward compatibility only

**Why Generic Tool:**

Single tool avoids tool sprawl, maintains tight discriminator validation, and keeps paging/handle semantics unified across all parent tools.

**Related:** `docs/progressive-disclosure-response-shapes.md`, `src/BlitzBridge.McpServer/Tools/AzureSqlDiagnosticTools.cs`

### Decision 016: Progressive Disclosure Phase 1 Design Specification (2026-04-27)

**Author:** Keaton (Lead)  
**Status:** Active  
**Scope:** Response shape changes, new detail tool, caching posture, telemetry

**Key Decisions:**

1. **One new tool: `azure_sql_fetch_detail_by_handle`** — required `parentTool` + `kind` discriminators; stateless re-run, no cache
2. **Phase 1 is additive-only** — existing compacted arrays stay in responses alongside new `handles` array and scalar summaries; no fields removed
3. **`IncludeVerboseResults` deprecated, not removed or repurposed** — flag keeps working in Phase 1; documentation marks deprecated
4. **No server-side caching of result sets** — memory pressure, point-in-time staleness, stdio restarts, and lightweight deployment prohibit it; all FRK procs ≤8 sec; re-run is correct strategy
5. **Telemetry: `estimated_payload_tokens` on every tool call** — instrument immediately using `BlitzBridge.Diagnostics` meter, independent of progressive disclosure; chars/4 heuristic
6. **Handles are opaque, encode original parameters** — server-validated, stateless, survive restarts; clients must not parse them

**Backward Compatibility:** Fully additive; existing client code continues without changes.

**Related:** `docs/progressive-disclosure-design.md`, `docs/implementation-work-items.md`

### Decision 017: Progressive Disclosure Phase 1 Testability Approval (2026-04-27)

**Author:** McManus  
**Status:** Active

Testability review and approval gate for Phase 1 progressive disclosure design.

**Phase 1 Verdict:** ✅ **APPROVED**

Section-level handles are deterministic, derived from normalized request parameters (not runtime state), versioned, and server-validated. This preserves D-3 FRK fixture determinism.

**Phase 1 Test Coverage Basis:**

- Parent tool emits consistent section-level handles for canonical request
- Detail tool accepts those same handles across repeated runs and process restarts
- No runtime state dependency (caches, timestamps, GUIDs, row identities)
- D-3 FRK fixture remains deterministic and testable

**Phase 2 Gate:** Do **not** approve Phase 2 row-level handles until each tool has stable row identity and deterministic fixture story.

**Phase 2 Blockers:**
- `sp_BlitzCache`: Needs `QueryHash` exposure and stability confirmation
- `sp_BlitzFirst`: Lacks stable FindingID; CheckDate is execution-time data; requires time-series comparison only

**Related:** `docs/progressive-disclosure-design.md` (Appendix A), `docs/progressive-disclosure-handle-audit.md`

### Decision 018: Coordinator Directive — Targeted Keaton Revision Only (2026-04-27)

**Author:** Coordinator (via user directive)  
**Status:** Active  
**Directive:** Skip full-team review loop; route only Keaton for revision pass on progressive-disclosure-design.md before Phase 2 implementation.

**Rationale:** User (Kori Francis) requested efficient routing: Keaton has deep context from Phase 1 design assembly; five specific review items are targeted and scoped to documentation only; full team review loop can be compressed into one agent pass.

**Related:** `.squad/decisions/inbox/copilot-directive-20260427T111041-0400.md`

### Decision 019: Progressive Disclosure Design Doc Revision — Five Items Addressed (2026-04-27)

**Author:** Keaton (Lead)  
**Status:** Active  
**Scope:** Design doc revision; documentation only; no code changes

Keaton completed targeted revision pass on `docs/progressive-disclosure-design.md` addressing five specific user review items before Phase 2 implementation kickoff.

**Five Revisions:**

1. **Token-Economics Reframed** — Added best-case, worst-case, and neutral-case analysis. Progressive disclosure is a **high-variance tradeoff**, not guaranteed win. Worst case: agent that expands all sections realizes ~41% savings but pays transaction overhead; may not outperform minimal-verbosity baseline.

2. **Explicit Dispatch Chosen Over Opaque Handles** — New Section 2.4 documents design tradeoff: explicit dispatch is debuggable (precise error messages, audit trail, server validation) while opaque is pure (client-opaque). Kori's Phase 1 preference for explicit recorded.

3. **Legal `kind` Values Enumeration** — New Section 2.4 table enumerates all valid (parentTool, kind) pairs per tool (4 tools × 1–4 kinds each). Code cannot drift from design; server-side validation must reject unknown combinations with clear error messages.

4. **Hockney's Handle Audit Integrated Into Main Doc** — Section 2.7 now summarizes audit findings (natural row identifiers, server-side narrowing capability per procedure) directly in design doc rather than as separate reference. Elevates audit to first-class design input.

5. **Error Contract Comprehensively Specified** — New Section 2.6 documents all failure modes: malformed handle, malformed payload, unknown parentTool, unknown kind, authorization drift, section expired, SQL failure. Authorization drift mirrors parent tool (403) for consistency and to prevent information leakage.

**Additional Changes:**

- **Phase 2 Roadmap** (Section 9): Additive Phase 1 → Phase 1.5 telemetry → Phase 2 breaking change (versioning). Key constraint: backward-compatibility break must be visible 6+ months in advance (Verbal's responsibility).
- **Response-size estimate updated** from ~200–400 chars to ~600–800 chars (more realistic for handle objects + metadata). Still negligible (~150–200 tokens).

**Status:** Design doc now ready for team review and implementation gate before Phase 1 coding begins.

**Related:** `docs/progressive-disclosure-design.md`, `.squad/decisions/inbox/keaton-progressive-disclosure-revision.md`

### Decision 020: Auth Cleanup — Canonical Auth Options & Bind Consolidation (2026-04-24)

**Author:** Fenster  
**Status:** Active  
**Scope:** Resolve duplicate auth configuration types, standardize to single canonical model

**Resolution:**

Removed duplicate auth options types and standardized MCP server on one canonical model bound to `BlitzBridge:Auth`.

1. Deleted `src/BlitzBridge.McpServer/Configuration/HttpAuthOptions.cs`
2. Created canonical `src/BlitzBridge.McpServer/Configuration/BlitzBridgeAuthOptions.cs` with:
   - `SectionName = "BlitzBridge:Auth"`
   - `EnvironmentTokenListVariable = "BLITZBRIDGE_AUTH_TOKENS"`
   - `Mode` (default `None`)
   - `Tokens` allowlist
3. Updated wiring and middleware to use canonical types
4. Updated tests to consume canonical auth options type

**Behavior Verification:**
- Auth mode behavior preserved (`None`, `BearerToken`)
- Token precedence preserved (`BLITZBRIDGE_AUTH_TOKENS` > config)
- Constant-time validation preserved (SHA-256 + FixedTimeEquals)
- Stdio behavior unaffected

**Related:** `src/BlitzBridge.McpServer/Configuration/`, `src/BlitzBridge.McpServer/Middleware/`, Decision 011

### Decision 021: Progressive Disclosure Phase 2 — Implementation (Fenster) (2026-04-27)

**Author:** Fenster  
**Status:** Completed  
**Scope:** Implement detail tool, stateless replay, mapper metadata, token telemetry

**Implementation Completed:**

1. **Versioned opaque handle codec** — `v1:` + base64 JSON; require explicit `parentTool` + `kind` dispatch on fetch tool
2. **Response-size telemetry** — Record at tool boundary for every response (legacy inline + detail-fetch)
3. **Detail orchestration** — `FrkProcedureService` with explicit dispatch validation
4. **Response mapping** — `FrkResultMapper` shapes handles with section metadata (title, severity, preview, itemCount, totalCount)
5. **Handle codec** — `ProgressiveDisclosureHandleCodec` with version prefix for forward-compatibility

**Artifacts:**
- `src/BlitzBridge.McpServer/Tools/AzureSqlDiagnosticTools.cs`
- `src/BlitzBridge.McpServer/Services/FrkProcedureService.cs`
- `src/BlitzBridge.McpServer/Services/FrkResultMapper.cs`
- `src/BlitzBridge.McpServer/Services/ProgressiveDisclosureHandleCodec.cs`
- `src/BlitzBridge.McpServer/Services/ResponseTelemetry.cs`

**Validation:**
- `dotnet build BlitzBridge.slnx` — 0 warnings, 0 errors
- Direct test executable — 34/34 passed

**Related:** Decision 016, `docs/progressive-disclosure-design.md`

### Decision 022: Repo-Root Dockerfile for BlitzBridge.McpServer (2026-04-24)

**Author:** Fenster  
**Status:** Active  

Added minimal multi-stage container definition at repo root for `BlitzBridge.McpServer` with explicit HTTP transport defaults.

**Key Decisions:**

1. Single repo-root `Dockerfile` targeting `src/BlitzBridge.McpServer` publish output
2. Use .NET 10 SDK for build/publish, .NET 10 ASP.NET runtime for final image
3. Run final image as non-root `app` user
4. Default container startup: `dotnet BlitzBridge.McpServer.dll --transport http`
5. Container HTTP defaults: port `5000` (`ASPNETCORE_URLS=http://+:5000`, `EXPOSE 5000`)

**Rationale:**
- Multi-stage build keeps runtime image small
- Non-root execution aligns with hardening baseline
- Explicit HTTP startup prevents accidental stdio mode in containers
- Port 5000 default matches existing hosted examples

**Validation:**
- `docker build -t blitzbridge-mcpserver:local -f Dockerfile .` succeeded locally

**Related:** Decision 005 (Stdio Transport Architecture)

### Decision 023: Docker Compose Zero-Dependency Demo Sandbox (2026-04-24)

**Author:** Hockney  
**Status:** Active  

Added three-service compose stack for first-time evaluators with zero pre-existing SQL asset dependencies.

**Key Decisions:**

1. Three-service compose stack (`samples/docker-compose-demo/docker-compose.yml`):
   - `sqlserver` (SQL Server 2022 Developer) with persistent volume + healthcheck
   - `sql-init` one-shot initializer (waits for readiness, creates DBAtools, installs FRK, runs seed workload)
   - `blitzbridge` built from repo-root Dockerfile, HTTP mode, gated on SQL health + init success

2. Vendor FRK installer as `samples/docker-compose-demo/sql/frk-install.sql` pinned to tag `20240222`

3. Split seeding for reuse:
   - `seed-workload.sql` for demo cache-rich `sp_BlitzCache` output
   - `seed-test.sql` for deterministic integration-test style setup

4. Standardize demo secrets: `SA_PASSWORD` and `BLITZ_BRIDGE_TOKEN` via `.env.example`

5. Scripted validation (`scripts/verify-demo.ps1`) proves startup path and MCP health behavior

**Why:**
Removes hidden prerequisites; makes 5-minute promise credible on clean machines. Split seed strategy keeps FRK install reusable across demo and test scenarios.

**Artifacts:**
- `samples/docker-compose-demo/docker-compose.yml`
- `samples/docker-compose-demo/.env.example`
- `samples/docker-compose-demo/profiles.json`
- `samples/docker-compose-demo/sql/` (FRK + seeds)
- `samples/docker-compose-demo/scripts/`

**Related:** Decision 022 (Dockerfile)

### Decision 024: Phase 2 Validation Complete — Implementation Matches Spec (2026-04-28)

**Author:** Hockney  
**Status:** Production Ready  
**Scope:** Full validation of Phase 2 implementation against design spec

**Finding:**

Phase 2 implementation (FRK handles, stateless replay, explicit dispatch) **exactly matches** `docs/progressive-disclosure-design.md` specification and is **production-ready**.

**Validated:**

1. Explicit Dispatch — ValidKindsByParentTool[] whitelist correctly enforces no blind dispatch
2. Handle Codec (v1 scheme) — Base64(JSON) encoding, versioned prefix, deterministic, tamper-detectable
3. Request Model — All 4 required fields (target, parentTool, kind, handle) + 1 optional (maxRows) present
4. Error Contracts — All 6 failure modes implemented with correct HTTP codes (400/403/404/500/504)
5. Stateless Replay — Handles encode only request parameters; replay reconstructs identical request; deterministic re-runs
6. Backward Compatibility — IncludeVerboseResults still works, marked deprecated in tool descriptions
7. Read-Only Safety — Multi-layered defense (connection, allowlist, dispatch validation)
8. Section Metadata — ItemCount (visible) + TotalCount (actual) tracking in handles
9. Handle Tamper Detection — ValidateHandleMatchesRequest ensures decoded handle matches request
10. Empty Section Handling — EnsureTableExists throws 404 if section absent

**Could Not Be Proven (Mitigated):**
- Real FRK section stability → FRK deterministic by design; standard functional testing before production
- Token savings → Theoretical until real payloads; not merge-blocking
- Process-restart determinism → Handles stateless by construction

**Recommendation:** ✓ **Approve Phase 2 for production deployment. No code changes required.**

**Related:** Decision 021, `docs/progressive-disclosure-design.md`, `.squad/agents/hockney/phase2-validation-report.md`

### Decision 025: Progressive Disclosure Phase 2 — Final Review Verdict (2026-04-28)

**Author:** Keaton (Lead)  
**Status:** Approved for Merge  
**Scope:** Full architectural review of Phase 2 implementation

**Verdict: APPROVED FOR MERGE**

**Five Review Dimensions:**

1. **Read-Only / Allowlist Guarantees: PRESERVED**
   - Detail fetch re-executes parent FRK procedures through same SqlExecutionService path
   - ApplicationIntent=ReadOnly, ValidateProcedureAccess, ValidateDatabaseAccess enforced on every call
   - No new execution path bypasses these checks

2. **Dispatch/Handle Contract Durability: SOUND**
   - Against FRK version bumps: handle encodes request parameters only (not output schema)
   - FrkResultMapper adapts via positional table access; EnsureTableExists → 404 for missing sections
   - Against handle tampering: ValidateHandleMatchesRequest cross-checks decoded handle vs. request parameters

3. **Hockney's Live-Target Gap: ACCEPTED**
   - Four unproven items (section stability, empty sections, token savings, restart replay) are mitigated by FRK design contract and D-3 fixture strategy
   - No merge-blocking risk

4. **Test Coverage: SUFFICIENT**
   - 37/37 tests pass
   - Covers handle emission, summary-to-detail flow, unknown kind/parentTool rejection, malformed handle, telemetry, backward compatibility

5. **Build Status: CLEAN**
   - `dotnet build BlitzBridge.slnx` — 0 warnings, 0 errors
   - Test executable — 37 passed, 0 failed, 0 skipped

**Decision:** Phase 2 is approved for merge. No reviewer lockout triggered.

**Related:** Decision 021, Decision 024, `.squad/decisions/inbox/keaton-progressive-disclosure-phase2-review.md`

### Decision 026: Docker Compose Demo Verification Guardrails (2026-04-24)

**Author:** McManus  
**Status:** Active  

Demo bundle must behave like true fresh-machine path: reproducible startup, deterministic SQL init ordering, clear MCP readiness checks.

**Key Decisions:**

1. Treat `samples/docker-compose-demo` as self-contained verification bundle with explicit startup gates
2. Require SQL readiness to include FRK + seed completion (not just SQL process availability)
3. Minimal, deterministic verification script (`scripts/verify-demo.ps1`) checks:
   - Compose config validity
   - Clean startup from `down --volumes`
   - Init completion
   - MCP `tools/list` availability
   - `azure_sql_health_check` invocation path

4. Default lightweight seed (`seed-test.sql`) for readiness; heavier seed (`seed-workload.sql`) as opt-in
5. SQL execution sessions set required options (`QUOTED_IDENTIFIER`, `ANSI_NULLS`) before procedure invocation

**Why:**
- Health/ordering must guard functional readiness, not container liveness
- Fast deterministic seed keeps verification reliable
- Explicit script-based checks make day-0 validation repeatable
- Session-level SQL option normalization reduces environment-specific drift

**Validation Evidence:**
- `dotnet build BlitzBridge.slnx` succeeded
- `dotnet build tests/BlitzBridge.McpServer.Tests` succeeded
- TUnit host run: 22/22 passed
- Compose stack reaches healthy state after clean restart

**Minimal Fixes Applied:**
- `verify-demo.ps1` argument-safe compose invocation
- `init-sql.sh` deterministic flow
- `docker-compose.yml` ordering + healthcheck gating
- `SqlExecutionService.cs` sets required SQL session options

**Related:** Decision 023, Decision 022

### Decision 027: Progressive Disclosure Phase 2 — Test Gate (McManus) (2026-04-27)

**Author:** McManus  
**Status:** Completed  
**Scope:** Unit/integration coverage for summary-to-detail flow, explicit-dispatch error contract

**Test Coverage Locked:**

1. `azure_sql_fetch_detail_by_handle` must succeed when given a handle from parent response
2. Explicit dispatch stays contractual: reject unknown `parentTool`, reject illegal `kind` values, reject malformed/mismatched handles with clear failures
3. Section-level handles for `sp_BlitzCache` and `sp_BlitzFirst`; tests assume section-level identity only (not row-level)

**Why:**
- Keeps new flow debuggable and fixture-friendly
- Preserves guardrail against brittle row-level assumptions
- Gives reviewers clean regression net around exact operator path (summary → drill-down)

**Test Coverage:**
- Handle emission from all four parent tools
- Summary-to-detail flow (explicit dispatch)
- Unknown kind rejection
- Unknown parentTool rejection
- Malformed handle rejection
- Mismatched dispatch metadata rejection
- Response telemetry recording
- Backward compatibility (IncludeVerboseResults)

**Artifacts:**
- `tests/BlitzBridge.McpServer.Tests/` (37/37 passed)

**Related:** Decision 021, `docs/progressive-disclosure-design.md`

### Decision 028: WebApplicationFactory Auth Integration Matrix (2026-04-24)

**Author:** McManus  
**Status:** Active  

HTTP auth behavior verified at ASP.NET pipeline boundary while keeping tests deterministic and fast.

**Coverage Matrix:**

1. `Mode=BearerToken` + no `Authorization` header => `401 Unauthorized`
2. `Mode=BearerToken` + wrong bearer token => `401 Unauthorized`
3. `Mode=BearerToken` + correct bearer token => `200 OK`
4. `Mode=None` => `200 OK` regardless of header

Additionally: Stdio bypass test proves stdio initialize RPC unaffected by HTTP auth settings.

**Why:**
- Validates real middleware behavior without external process dependency
- Improves determinism while preserving integration-level confidence
- Maintains guardrail that HTTP auth is transport-scoped; must not leak to stdio

**Validation Evidence:**
- Test project built successfully
- TUnit executable: 22/22 passed

**Related:** Decision 011 (Auth Mode Architecture), Decision 012 (Auth Test Posture)

### Decision 029: Auth Documentation & Code Drift Cleanup (2026-04-24)

**Author:** Verbal  
**Status:** Active  

README audit vs. implementation confirmed alignment on auth configuration shape and precedence.

**Findings:**

1. ✅ `Auth.Enabled` references — None found; README correctly documents `Auth.Mode` only
2. ✅ Environment variable naming — Correct (`BLITZBRIDGE_AUTH_TOKENS`)
3. ✅ Token precedence — Accurate in README
4. ✅ Profile-level `Enabled` confusion — Clarified (controls profile activation, not auth)
5. ✅ Hosted auth section — Accurate (CORS, Bearer, Stdio guarantee)

**Resolution:**

Added clarifying comment in README JSON example to distinguish profile-level `Enabled` (gates profile validation) from auth configuration.

**Takeaways:**
1. Configuration shape + precedence aligned; README matches implementation
2. No breaking changes needed
3. Stdio isolation holds; auth only in HTTP path
4. Future extensibility ready (Mode enum supports EntraId, EasyAuth)

**Related:** Decision 011 (Auth Mode Architecture), Decision 010 (Hosting with Auth Docs)

### Decision 030: Docker Compose Demo Documentation (2026-04-24)

**Author:** Verbal  
**Status:** Active  

Added fast on-ramp documentation for teams evaluating without .NET CLI or Aspire knowledge.

**Solution:**

Docker Compose quick-start guide at `samples/docker-compose-demo/README.md` with three-command, five-minute path:

1. `cp .env.example .env`
2. Edit password/token
3. `docker compose up`

Includes `curl` sample against `/mcp` endpoint showing `tools/list` request and response.

**Key Decisions:**

1. Position matters — Demo link early (after Install) signals this is fastest eval path
2. Single curl sample proves end-to-end — Shows successful `/mcp` call with token + JSON-RPC
3. Troubleshooting baked in — Common errors + solutions prevent support volume
4. Realistic Docker workflow — No Aspire, no appsettings; just `.env` + compose
5. Self-service resolution — Pre-seeded error patterns let users debug independently

**Artifacts:**
- `samples/docker-compose-demo/README.md` — Three-command flow, curl, troubleshooting
- `README.md` — New "Try it in 5 minutes" section

**Impact:**
- **Adoption:** Removes friction for quick evaluation
- **Support:** Troubleshooting reduces "why doesn't it work" questions
- **Clarity:** Prominent positioning signals Docker is first-class eval path
- **Alignment:** Demo docs stay in sync with main README

**Related:** Decision 023 (Docker Compose Demo)

### Decision 031: Progressive Disclosure Phase 1 — Implementation vs. Design Reconciliation (2026-04-25)

**Author:** Verbal  
**Status:** Active  

Fenster's implementation **complete and matches design spec with one intentional divergence.**

**Intentional Divergence Reconciled:**

- **Design proposed:** `azure_sql_blitz_index` detail kind `unused_indexes`
- **Implementation shipped:** `azure_sql_blitz_index` detail kind `foreign_keys`
- **Reason:** Foreign keys more actionable for table-scoped analysis than unused index tracking
- **Action:** Updated README.md and docs/mcp-tools.md to document shipped `foreign_keys` kind

**Implementation Verification:**

✅ Explicit dispatch + validation — (parentTool, kind) pairs strictly validated at server init  
✅ Stateless handle decoding/replay — Base64-encoded JSON with request parameters; no caching  
✅ Parent-response metadata — All query tools return `handles[]` with per-section metadata  
✅ Estimated-token telemetry — ResponseTelemetry captures payload chars; 4-char/token heuristic  
✅ Backward compatibility — All compacted arrays retained; `IncludeVerboseResults` functional  
✅ Error contract — 400 for invalid dispatch; 403 for authorization drift; 500 for SQL failures  
✅ Response shape alignment — All query tools return `Summary` (not legacy `label`/`value`)

**Documentation Updates:**

1. `docs/mcp-tools.md`:
   - Replaced `unused_indexes` with `foreign_keys` in blitz_index section
   - Added explicit required parameters (databaseName, schemaName, tableName)
   - Updated all response examples with actual field names (title/severity/message, not label/value)
   - Corrected dispatch matrix table

2. `README.md` — No changes needed (already links to mcp-tools.md)

**Quality Assurance:**

✅ Operators can trust README.md and docs/mcp-tools.md as source of truth  
✅ Clients can implement against documented shapes with confidence  
✅ No breaking changes; documentation alignment only  

**Related:** Decision 021, `docs/progressive-disclosure-design.md`

### Decision 032: Progressive Disclosure Phase 2 Documentation (2026-04-25)

**Author:** Verbal  
**Status:** Approved for Phase 2 Implementation  
**Scope:** Operator-focused documentation for progressive disclosure design + implementation

**Work Completed:**

1. **README Tool Reference Update** — Categorized query tools vs. detail-fetching tool; cross-reference to comprehensive `docs/mcp-tools.md`

2. **New Document: `docs/mcp-tools.md` (36 KB structured reference)**
   - Overview: default behavior (summary + handles), backward compatibility, interaction pattern
   - Query Tools: full reference for each of 5 existing tools
   - Detail Fetching Tool: complete `azure_sql_fetch_detail_by_handle` reference with error contract
   - Dispatch Matrix: visual (parentTool, kind) contract
   - Working with Large Result Sets: concrete end-to-end scenario (wait → cache → index) showing token savings
   - Deprecated Features: `IncludeVerboseResults` guidance
   - Backward Compatibility: operator reassurance

**Design Alignment:**

✅ Explicit dispatch via `parentTool` + `kind`  
✅ (parentTool, kind) validation table  
✅ Error contract for malformed handles, unknown kinds, authorization drift  
✅ Stateless re-run model  
✅ Handles encode original parameters  
✅ Section-level handles (Phase 1 scope)  
✅ `IncludeVerboseResults` deprecation with backward compatibility  

**Backward Compatibility Guarantee:**

✅ Existing tool inputs unchanged  
✅ Existing response fields retained  
✅ New fields additive (`handles`, scalar counts)  
✅ `IncludeVerboseResults` continues to work  
✅ New tool opt-in (agents ignoring handles work identically)  

**Phase 1.5 → Phase 2 Transition:**

If Phase 2 removes compacted arrays (breaking change), communication must be:
- Visible and dated 6+ months in advance
- Paired with migration guidance
- Reflected in versioning strategy

**Verbal's Future Action Items:**

1. When Phase 1.5 telemetry arrives (6–12 months), draft Phase 2 deprecation notice if adoption high
2. Deprecation notice will include: rationale, timeline, migration guide
3. Communication will precede implementation by 6+ months per design constraint

**Related:** `docs/progressive-disclosure-design.md`, `docs/implementation-work-items.md`

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
