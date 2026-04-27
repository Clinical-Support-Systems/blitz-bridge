# Project Context

- **Owner:** Kori Francis
- **Project:** Read-only .NET MCP server for Brent Ozar First Responder Kit diagnostics against preconfigured Azure SQL targets
- **Stack:** .NET, MCP server patterns, Azure SQL, First Responder Kit diagnostics
- **Created:** 2026-04-23T17:59:01Z

## Learnings

Azure SQL specialist initialized with day-1 project context.

### Config path + SqlTargets safety review (2026-04-24)

- Verified `--config` behavior is schema-compatible with current `SqlTargets:Profiles` binding; no alternate config contract was introduced.
- Confirmed stdio startup now has deterministic fail-fast semantics for missing config (stderr message + non-zero exit), reducing silent misconfiguration risk in MCP clients.
- Added profile-level `AiMode` to config model and wired default tool behavior to profile AiMode when request override is absent, matching PRD intent.
- Added startup validation guardrails for profile safety (`ApplicationIntent=ReadOnly`, timeout bounds, known FRK procedures, non-blank allowlists).
- Reinforced runtime connection safety by forcing `ApplicationIntent=ReadOnly` and `MARS=false` when opening SQL connections.
- **Decision merged** → `hockney-config-paths.md` consolidated to `decisions.md` as Decision 006 (Active)

### Docker Compose zero-dependency sandbox (2026-04-24)

- Replaced the demo stack with a true local sandbox using SQL Server 2022 + one-shot init + Blitz Bridge HTTP service, all health-gated in compose.
- Pinned and vendored FRK installer from First Responder Kit tag `20240222` (`Install-Core-Blitz-No-Query-Store.sql`) into `samples/docker-compose-demo/sql/frk-install.sql`.
- Added split seed strategy: `seed-workload.sql` for rich demo query-cache output and `seed-test.sql` for reusable deterministic integration setup.
- Added a repeatable verifier script (`scripts/verify-demo.ps1`) that validates compose startup and calls `azure_sql_health_check` on `demo-sql-target`.
- Wrote decision note: `.squad/decisions/inbox/hockney-docker-demo.md`.

### Progressive Disclosure Handle Audit — Phase 1 FRK Procedures (2026-04-27)

- **Scope:** Audited sp_Blitz, sp_BlitzCache, sp_BlitzIndex, sp_BlitzFirst for row identity, server-side narrowing, and result shape.
- **Key Findings:**
  - All procedures have stable natural handles (CheckID for Blitz, QueryHash for Cache, IndexName for Index, WaitType/Finding for First).
  - Server-side narrowing varies: sp_BlitzIndex has full table-scope narrowing (required params); sp_BlitzCache has partial (Top + SortOrder); sp_Blitz and sp_BlitzFirst have none.
  - Current response models already implement correct summary/detail splits with MaxRows compaction; no breaking changes needed.
- **Uncertainties Called Out:** CheckID stability across FRK versions (assume v8.19 is stable; validate in D-1), sp_BlitzFirst lacks stable FindingID (drill-down via time-series only), QueryHash not exposed in current response.
- **Artifact:** `docs/progressive-disclosure-handle-audit.md` + decision note `.squad/decisions/inbox/hockney-progressive-disclosure.md`.
- **Phase 2+ Opportunities:** Expose QueryHash + handle fields, add compaction metadata, audit sp_BlitzLock.
- **Verdict:** No Phase 1 code changes required; response model design is sound.
- **Decision merged** → `hockney-progressive-disclosure.md` consolidated to `decisions.md` as Decision 014 (Active)
- **Orchestration logged** → Entry recorded in `.squad/orchestration-log/2026-04-27T15-04-23Z-hockney.md`
