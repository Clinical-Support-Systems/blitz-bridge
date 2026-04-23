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

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
