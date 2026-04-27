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

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
