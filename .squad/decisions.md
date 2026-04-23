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

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
