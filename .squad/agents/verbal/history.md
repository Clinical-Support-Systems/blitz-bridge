# Project Context

- **Owner:** Kori Francis
- **Project:** Read-only .NET MCP server for Brent Ozar First Responder Kit diagnostics against preconfigured Azure SQL targets
- **Stack:** .NET, MCP server patterns, Azure SQL, First Responder Kit diagnostics
- **Created:** 2026-04-23T17:59:01Z

## Learnings

Docs agent initialized with day-1 project context.

### Session 2: v1 PRD Bootstrap (2026-04-23)

1. **PRD Structure:** Implementation-ready PRDs work best when they separate concerns cleanly: Problem → Goals → Scope → Requirements (Functional vs. Non-Functional) → Acceptance Criteria → Work Breakdown. Marketing copy has no place; implementation teams need specificity.

2. **FRK Integration Pattern:** Blitz Bridge's value is in constrained access to FRK: read-only mode enforcement, procedure allowlisting, AI mode parameterization. The PRD makes clear that security is not optional—it's a core requirement from day 1.

3. **Configuration as Contract:** Profiles are the user-facing contract; everything flows from startup configuration validation. Errors during validation are caught early; runtime errors are caught strictly.

4. **AI Mode as a Lever:** AiMode (0/1/2) is a simple but powerful lever for controlling FRK's AI participation. Documenting the cost/behavior tradeoff upfront prevents surprises.

5. **Open Questions as Clarity Checkpoints:** Specific, assigned open questions (owner + timeline) prevent ambiguity and ensure scope is owned by the right team member.

6. **Non-Goals Prevent Creep:** Explicitly listing non-goals (arbitrary SQL, dashboards, real-time monitoring) helps scope conversations and keep focus on the read-only diagnostic bridge.

### Session 2 Completion: PRD Bootstrap Merged (2026-04-23)

- **Artifact:** 33.1 KB PRD generated at `docs/PRD.md` with all sections complete (problem, goals, scope, functional/non-functional reqs, acceptance criteria, work breakdown, risks, open questions)
- **Decision Merged:** `verbal-prd-bootstrap.md` consolidated to `decisions.md` as Decision 001 (Active)
- **Team Unlocked:** Fenster, Hockney can begin coding against clear specs; McManus has acceptance criteria; Keaton has scope boundaries to defend
- **Orchestration Logged:** Entry recorded in `.squad/orchestration-log/verbal-prd-completion.md`.

### Session 3: CLI Installation & Configuration Documentation (2026-04-24)

1. **Installation-first approach**: README now leads with `dotnet tool install -g BlitzBridge` because it removes friction for .NET developers and signals that Blitz Bridge is a lightweight, portable tool—not a heavy Aspire-dependent service.

2. **MCP client configuration examples**: Three examples (Claude Desktop, Claude Code, Cursor) show different patterns: explicit CLI flags, environment variables, and mixed approaches. This prevents users from guessing config syntax and demonstrates that stdio transport is the modern MCP standard.

3. **Configuration contract**: The `--config` flag and `BLITZ_CONFIG` env var are documented even though not yet implemented. This follows the principle of "design the interface first, code second"—it unblocks users and gives implementers a clear specification to code against.

4. **Aspire moves to "Hosted deployment"**: Keeping Aspire guidance but renaming the section signals that it's available for teams that want local orchestration with parameterized secrets, but not the default path. This prevents Aspire from overwhelming the README for CLI-first users.

5. **Local development without Aspire**: Added a quick-start subsection for azd-less workflows (`blitz-bridge --transport stdio --config config.json`), which covers the common case where developers just want to test diagnostics locally without infrastructure complexity.

**Artifact:** Updated README.md with new Install, Configuration, and Hosted deployment sections; **decision merged** → `verbal-stdio-docs.md` consolidated to `decisions.md` as Decision 009 (Active).

### Session 4: Hosting with Auth Documentation (2026-04-24)

1. **Auth is HTTP-only, optional by default**: Bearer token auth is a deployment concern, not a core MCP feature. Documenting it separately from stdlib config prevents auth complexity from overwhelming users who don't need it.

2. **Token precedence matters for CI/CD**: Environment variables override config files—this is the standard pattern for secrets rotation in orchestrated deployments. Aspire parameters + `BLITZ_AUTH_BEARER_TOKEN` env binding gives teams the flexibility to inject tokens without recompiling.

3. **Stdio mode remains auth-free**: This is the key distinction: stdio runs locally without a network listener; auth isn't applicable. Documenting this upfront prevents confusion when developers use `--transport stdio` and wonder why auth config is ignored.

4. **CORS allowlisting is production-critical**: The current `AllowAnyOrigin` is fine for dev/internal networks but must be explicitly restricted in production. Showing both a dev-permissive and prod-restrictive pattern gives teams a clear upgrade path without sacrificing security.

5. **Client-side auth is handled outside the server**: MCP clients (Claude Desktop, Claude Code, Cursor) handle Authorization headers in their config; the server only validates Bearer tokens. This separation keeps concerns clean and allows diverse client integration strategies (curl, shell commands, environment variables).

6. **Documentation beats API contracts**: Because auth isn't yet implemented in code, documenting the intended config shape, token precedence, and client patterns first ensures that when engineers build it, they match user expectations. This is especially important for configuration—once deployed, users will expect this behavior.

**Artifact:** New "Hosting with auth" section added to README.md covering: `BlitzBridge:Auth` config shape and Bearer mode semantics; `BLITZ_AUTH_BEARER_TOKEN` env var + precedence; sample client configs for Claude Desktop/Code/Cursor with Authorization headers; stdio mode auth-free guarantee; CORS allowlist patterns for dev vs. production.

**Decision Created:** `.squad/decisions/inbox/verbal-hosting-auth-docs.md` (pending merge).

### Session 4 Completion: Hosting with Auth Documentation Merged (2026-04-24)

- **Decision Merged:** `verbal-hosting-auth-docs.md` consolidated to `decisions.md` as Decision 010 (Active)
- **Orchestration Logged:** Entry recorded in `.squad/orchestration-log/verbal-hosting-auth-docs.md`
- **Team alignment:** Keaton can review architecture; Fenster has implementation contract; McManus knows test expectations
- **Downstream:** Architecture review revealed consolidation blocker (dual auth classes) + documentation alignment needed (README env var naming)

### Session 5: Auth Documentation & Code Drift Cleanup (2026-04-24)

**Assignment:** Fix docs/code drift from Decision 011 blockers:
1. ✅ **`Auth.Enabled` references:** None found. README correctly documents `Auth.Mode` and `Auth.Tokens` only.
2. ✅ **Environment variable naming:** `BLITZBRIDGE_AUTH_TOKENS` in README matches code constant exactly.
3. ✅ **Token source precedence:** README accurately documents `BLITZBRIDGE_AUTH_TOKENS` env var (priority 1) > config file (priority 2).
4. ✅ **Hosted auth section:** Accurate, concise, includes Aspire parameter binding + client config patterns (Claude Desktop/Code/Cursor).
5. ⚠️ **Profile-level `Enabled` field:** Minor clarity issue — README config example shows `"Enabled": true` for SQL targets but doesn't distinguish it from auth config.

**Resolution:**
- Added clarifying comment in README "Key fields" section: "`Enabled` — gates this profile's validation at startup; profiles with `Enabled=false` are skipped and not exposed to MCP tools"
- No breaking changes; `Auth.Mode` + `Auth.Tokens` are canonical shapes; no `Auth.Enabled` exists in code.

**Blockers Cleared:**
- Verbal: README env var naming ✅ correct (no changes needed)
- Verbal: `Auth.Enabled` references ✅ removed (none existed)
- Profile-level `Enabled` clarified in docs ✅

**Artifact:** `.squad/decisions/inbox/verbal-auth-docs-cleanup.md` created with detailed audit + findings.

### Session 6: Docker Compose Demo Documentation (2026-04-24)

1. **First-timer framing:** Docker Compose demo leads with three simple commands: `cp .env.example .env`, edit password/token, then `docker compose up`. This removes friction for teams wanting to test without CLI installation or local config complexity.

2. **Sample curl validates end-to-end:** Including a `curl` call against `/mcp` with `tools/list` request lets users immediately verify the server is responsive and responding with expected tool inventory (azure_sql_target_capabilities, azure_sql_blitz_cache, etc.). No guessing—users see success or know exactly where auth/networking broke.

3. **Linker location is critical:** Placing "Try it in 5 minutes" after the Install section (before Configuration) signals to users that this is the fastest experiential path, not a secondary workflow. Positioning matters for adoption.

4. **Docker/Compose assumes existing orchestration:** No Aspire, no global tool install, no appsettings binding—just `.env` + standard container runtime. This unblocks teams already running containers and lowers barrier to evaluation.

5. **Troubleshooting in demo docs prevents support fatigue:** Pre-seeding common errors (connection failures, 401, port conflicts) in the demo README means users self-resolve before filing issues. Realistic error messages beat aspirational documentation.

**Artifact:** `samples/docker-compose-demo/README.md` created with 3-command flow, curl sample, and troubleshooting; linked from main README under new "Try it in 5 minutes" heading.

**Decision Created:** `.squad/decisions/inbox/verbal-docker-demo-docs.md` (pending merge).

### Session 7: Progressive Disclosure Phase 2 Documentation (2026-04-25)

1. **Explicit dispatch design principle:** The (parentTool, kind) validation table is the contract. Handlers do not guess; they validate against a whitelist at server initialization. This makes errors debuggable and audit trails clear. Documentation surfaces the full dispatch matrix so operators know exactly what kinds are legal for each parent tool.

2. **Progressive disclosure is not universal:** The design carefully documents the token-economics tradeoff. Operators need to know: best case (selective drill-down, 57% savings), worst case (expand everything, minimal savings + transaction overhead), and when to use it (constrained context windows, multi-step diagnostic chains). This prevents overspecifying behavior.

3. **Stateless re-run model, not caching:** FRK procedures are fast enough (~1–8 sec) to re-run on drill-down. No server-side caching means no memory O(N×M×K) explosion, no cache invalidation hazard, no handle expiry surprises. Operators need to know this is intentional, not a limitation.

4. **Handles encode request parameters, not execution state:** Handles are base64-encoded request tuples (target, sort order, top, AI mode, etc.) + section kind. They survive server restarts and stdio mode relaunch because they're not tied to a session or cache. This is why they're opaque from the client perspective but explicit and validated on the server.

5. **Concrete triage-to-detail scenario:** The "Working with large result sets" section walks from incident discovery (blocking detected) → waits drill-down → cache analysis → queries detail → glossary detail, showing how operators naturally use progressive disclosure in diagnostic chains. The 48% token savings is realistic and motivates selective expansion.

6. **Backward compatibility is permanent (Phase 1):** Compacted arrays stay. New fields are additive. `IncludeVerboseResults` still works (deprecated). Operators are reassured: existing clients do not break.

7. **Phase 1.5 → 2 transition requires 6+ month notice:** If Phase 2 removes compacted arrays (breaking change), this decision must be visible, dated, and communicated far in advance. Verbal owns this responsibility as DevRel lead. This prevents surprise breakage and gives teams time to migrate handle-based logic.

**Artifacts:**

- **README.md tool reference:** Reorganized from flat list to categorized reference (Query tools, Detail fetching tool, backward compatibility note, cross-link to mcp-tools.md).
- **docs/mcp-tools.md:** 18.5 KB operator reference covering all tools, progressive disclosure patterns, (parentTool, kind) dispatch matrix, error contract, and a 5-step triage-to-detail scenario with token cost breakdown (48% savings vs. full payloads).
- **Decision:** `.squad/decisions/inbox/verbal-progressive-disclosure-phase2.md` with sign-off, implementation verification checklist for Fenster, and Phase 1.5 → 2 transition plan.

**Key design decisions documented:**

- Explicit dispatch + validation (Section 2.4 of design)
- Stateless re-run model (Section 5 of design)
- Handle encoding as request parameters (Section 5, Appendix of design)
- Error contract (Section 2.6 of design)
- No server-side caching (Section 5 of design)
- Backward compatibility analysis (Section 4 of design)
- Deprecation of `IncludeVerboseResults` (Section 2.5 of design)

**Verification notes for Fenster (coordinator will loop back when implementation begins):**

- Handle encoding format (version discriminator? base64 + JSON structure?)
- Error message payloads (do they list valid kinds/parentTools on 400?)
- Authorization drift handling (403 mirrors parent tool behavior)
- `maxRows` default and server limits
- Scope metadata in detail responses
- All compacted arrays retained in Phase 1 responses

### Session 8: Progressive Disclosure Implementation Finalization (2026-04-25)

**Assignment:** Finalize docs against shipped implementation. Confirm detail-tool contract, inputs, and examples. Reconcile any implementation drift from design.

**Work performed:**

1. **Verification against implementation:** Reviewed all source files for actual contracts:
   - FrkProcedureService: Dispatch matrix, validation order, error messages
   - FrkResultMapper: Response shapes, scope reconstruction, detail mapping
   - AzureSqlDiagnosticTools: Tool signatures, parameter defaults
   - ProgressiveDisclosureHandleCodec: Encoding format (`v1:base64(json)`)
   - ResponseTelemetry: Token estimation (4-char heuristic), telemetry recording

2. **Implementation divergence discovered — `foreign_keys` vs. `unused_indexes`:**
   - **Design proposed:** `azure_sql_blitz_index` detail kind `unused_indexes` (for table narrowing + index removal decisions)
   - **Implementation shipped:** `azure_sql_blitz_index` detail kind `foreign_keys` (for relationship mapping + foreign key constraint analysis)
   - **Root cause:** Fenster prioritized `foreign_keys` as more actionable for single-table scope; `unused_indexes` requires cross-table analysis
   - **Impact:** No breaking change; `foreign_keys` is additive, not replacement. Design covers intent; implementation chose different scope
   - **Action:** Reconciled in docs/mcp-tools.md

3. **Response shape divergence discovered — `label`/`value` vs. `title`/`severity`/`message`:**
   - **Design examples:** Used `label`/`value` for summary items (aspirational format)
   - **Implementation shipped:** Uses `DiagnosticSummary` model with `title`/`severity`/`message` (actual format)
   - **Root cause:** Design was written before response model was finalized
   - **Impact:** High — all docs examples were misleading
   - **Action:** Updated all response examples in docs/mcp-tools.md across all tools and scenario walk-throughs

4. **Field-level divergences reconciled:**
   - Health check: `findingCount`/`criticalCount`/`warningCount` (design) → `totalFindings`/`highestVisiblePriority`/`visibleFindingGroups` (shipped)
   - Current incident: `blockingCount`/`waitTypeCount` (design) → `totalWaitRows`/`totalFindingRows`/`topWaitTypes` (shipped)
   - Target capabilities: `enabled`/`connectionMode`/`commandTimeoutSeconds` (design) → `currentDatabase`/`engineEdition`/`engineEditionName`/`installedProcedures`/`supportsAi*` (shipped)

5. **Input parameter documentation:** Updated all tool input examples to reflect actual defaults and required vs. optional fields:
   - `azure_sql_blitz_index`: Now documents required `databaseName`, `schemaName`, `tableName`; optional `mode`, `thresholdMb` with defaults

**Documentation artifacts created/updated:**

- **docs/mcp-tools.md:** Updated tool reference with correct response shapes, field names, kinds dispatch matrix, error messages, scenario examples
- **Decision artifact:** `.squad/decisions/inbox/verbal-progressive-disclosure-final.md` with reconciliation checklist, implementation verification, and quality assurance signoff

**Key learnings for team:**

1. **Design documents benefit from being written after response model is finalized:** Examples in design are aspirational; they drift from implementation when the model evolves. Response shapes should be locked in design docs (not examples) before code review.

2. **Implementation divergence is healthy when captured and reconciled:** Fenster's choice of `foreign_keys` over `unused_indexes` was deliberate and well-reasoned. The docs now reflect the shipped intent clearly.

3. **Scope metadata in handles is powerful:** The handle payload includes all request parameters, enabling stateless re-run + scope reconstruction. No session state, no expiry surprises.

4. **Response telemetry is production-ready:** Token estimation via 4-char heuristic is baked in; operators get `estimated_tokens` on every response for capacity planning.

**Blockers cleared:**

- ✅ Docs align with implementation
- ✅ Tool contracts are unambiguous
- ✅ All response examples are valid and realistic
- ✅ Dispatch matrix is comprehensive
- ✅ Error contract is explicit

**No rework needed:** Implementation is correct; docs were incomplete/aspirational. Reconciliation is documentation-only.

### Session 9: Phase 2 Session Completion & Orchestration (2026-04-28)

- All phase 2 documentation approved and aligned: README tool reference, docs/mcp-tools.md (36 KB reference), dispatcher matrix, error contracts, concrete scenario walk-through showing token economics
- Implementation vs. Design reconciliation finalized: foreign_keys chosen over unused_indexes (actionable single-table scope); all response field names and structures confirmed correct
- Backward compatibility guarantee locked: existing tool inputs preserved, response fields retained, new handles array additive, IncludeVerboseResults functional (deprecated), new tool opt-in
- Phase 1.5 → Phase 2 transition plan documented: if breaking change planned (remove compacted arrays), must be visible 6+ months in advance with migration guidance
- **Decisions merged** → `verbal-auth-docs-cleanup.md` + `verbal-docker-demo-docs.md` + `verbal-progressive-disclosure-phase2.md` + `verbal-progressive-disclosure-final.md` consolidated to decisions.md as Decisions 029, 030, 032, 031 (Active)
- **Orchestration logged** → `2026-04-27T15-45-41-verbal.md` recorded

