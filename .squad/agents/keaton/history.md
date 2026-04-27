# Project Context

- **Owner:** Kori Francis
- **Project:** Read-only .NET MCP server for Brent Ozar First Responder Kit diagnostics against preconfigured Azure SQL targets
- **Stack:** .NET, MCP server patterns, Azure SQL, First Responder Kit diagnostics
- **Created:** 2026-04-23T17:59:01Z

## Learnings

Team lead agent initialized with day-1 project context.

### Session 2: v1 PRD Bootstrap (2026-04-23)

- **PRD finalized and merged to decisions.md** → Ready for scope review and open-question assignment
- **Five core MCP tools locked** → Fenster/Hockney can begin coding Week 1
- **Read-only + allowlist model approved** → Security model first-class from day 1
- **Open questions ownership:** Keaton shepherds scope consensus; specific owners assigned (Kori, Keaton, Hockney, Fenster, McManus)
- **Team roster complete** → 7-agent team cast; all histories initialized
- **Next:** 48-hour team review window; finalize open question timeline

### Session 3: PRD Decomposition & Work Items (2026-04-23)

- **Full codebase audit completed** → Scaffold is ~70% built (MCP tools, services, models, Aspire wiring all present)
- **Key gap identified:** `AiMode` missing from `SqlTargetProfile` config — blocks AiMode wiring chain
- **Test project empty** → McManus needs to stand up xUnit infrastructure immediately (T-1)
- **Produced `docs/implementation-work-items.md`** → 30 tasks across 5 workstreams, 3 execution batches, full dependency graph
- **Six architecture decisions made** → Written to `.squad/decisions/inbox/keaton-prd-decomposition.md`
- **Critical path:** D-3 (FRK test fixture) gates all integration tests; L-1 (AiMode config) unblocks half the dependency chain
- **Batch model:** 3 batches, reviewer gates at each boundary, Batch 1 fully parallelizable
- **Next:** Team reads work items, begins Batch 1 execution across all roles simultaneously

### Session 4: Stdio Transport Architecture Guardrails (2026-04-24)

- **Dual-path architecture approved** → `--transport stdio|http` (default `http`) branches early in `Program.cs`; HTTP path must remain byte-for-byte identical in behavior
- **18 explicit no-regression checks defined** → 7 HTTP (H-1–H-7), 8 stdio (S-1–S-8), 3 global tool (G-1–G-3); Fenster implements, McManus tests
- **Key risk: stdout log pollution** → In stdio mode, all logging must go to stderr; stdout is exclusively MCP JSON-RPC. This is the highest-likelihood defect.
- **Shared service registration pattern** → Recommend extracting `ConfigureSharedServices(IServiceCollection, IConfiguration)` to avoid duplication between HTTP and stdio builders
- **AppHost frozen** → Zero changes allowed to `BlitzBridge.AppHost/` for this work item; Aspire orchestrator uses default `http` transport
- **Package additions** → Need `ModelContextProtocol` (base) alongside existing `ModelContextProtocol.AspNetCore`; both at `1.2.0`
- **Global tool packaging** → `PackAsTool=true`, `ToolCommandName=blitz-bridge`; must verify no Aspire SDK transitive dependency in tool scenario
- **Config source divergence** → HTTP uses standard ASP.NET config chain; stdio uses `--config <path>` or OS-default `config.json` location
- **Decision merged** → `keaton-stdio-transport-guardrails.md` consolidated to `decisions.md` as Decision 005 (Active)
- **Next:** Fenster begins implementation; McManus prepares test harness for stdio path; all H-* checks are merge-blocking

### Session 5: Auth Mode Architecture Review (2026-04-24)

- **Dual options class conflict found** → `HttpAuthOptions.cs` and `BlitzBridgeAuthOptions` both bind to `BlitzBridge:Auth`; neither is wired. Must consolidate before auth implementation begins.
- **Extension-friendly pattern chosen** → String-based `Mode` property with startup validation, not an internal enum. Compose with ASP.NET Core `AddAuthentication()` pipeline, not custom middleware.
- **Entra ID / Easy Auth composability confirmed** → Adding a provider = new switch case + NuGet package + mode-specific config properties. No refactoring of tools, services, or stdio path required.
- **README–code divergence flagged** → `Auth.Enabled` doesn't exist in code; env var name mismatch (`BLITZ_AUTH_BEARER_TOKEN` vs `BLITZBRIDGE_AUTH_TOKENS`). Verbal must align.
- **Profile authorization seam identified for v2** → `SqlExecutionService.GetTargetContext()` is the natural point to add identity-based profile filtering when client identity becomes available.
- **Seven decisions produced** → D1–D7 in `keaton-auth-mode-architecture.md`; two marked as blockers for Fenster and Verbal.
- **Decision merged** → `keaton-auth-mode-architecture.md` consolidated to `decisions.md` as Decision 011 (Active)
- **Orchestration logged** → Entry recorded in `.squad/orchestration-log/keaton-auth-architecture-review.md`
- **Next:** Fenster deletes `HttpAuthOptions.cs` and implements auth via `ConfigureAuth` method. Verbal updates README to remove `Auth.Enabled` and fix env var naming. McManus implements integration test suite.

### Session 7: Progressive Disclosure Phase 1 Design Assembly (2026-04-27)

- **Design doc completed** → `docs/progressive-disclosure-design.md` covers problem statement, token economics, tool surface changes, before/after agent flow, backward compat analysis, caching recommendation, telemetry, and open questions
- **Backward compatibility confirmed** → Adding `handles` array + scalar summaries to existing responses + one new detail tool is fully additive. No fields removed, no inputs changed, no breaking changes in Phase 1.
- **Server-side caching explicitly rejected** → Memory pressure, cache invalidation on point-in-time diagnostics, stdio restarts, and deployment simplicity all argue against it. All four FRK procs are ≤8 sec — re-run is acceptable.
- **Telemetry recommendation: ship independently** → `estimated_payload_tokens` (chars/4) histogram on every tool call, using existing `BlitzBridge.Diagnostics` meter. Independent of progressive disclosure feature flag.
- **One generic detail tool chosen** → `azure_sql_fetch_detail_by_handle` with required `parentTool` + `kind` discriminators. Lower surface growth than 5 per-parent tools.
- **`IncludeVerboseResults` deprecated, not removed or repurposed** → Same name, same meaning, just discouraged. Phase 2 may remove compacted arrays from default response (breaking change, needs versioning).
- **Key input artifacts:** Hockney's handle audit (FRK narrowing capabilities), Fenster's response-shape prototype (contracts and IncludeVerboseResults posture)
- **Eight design decisions recorded** → D1–D8 in design doc; team decision written to inbox
- **Open questions for Phase 2:** QueryHash exposure, row-level handles, cursor pagination, sp_BlitzLock, handle versioning, agent caching guidance
- **Decision merged** → `keaton-progressive-disclosure.md` consolidated to `decisions.md` as Decision 016 (Active)
- **Orchestration logged** → Entry recorded in `.squad/orchestration-log/2026-04-27T15-04-23Z-keaton.md`
- **Next:** Team reviews design doc. Fenster begins Phase 1 implementation after review gate. McManus plans test coverage for detail tool.

