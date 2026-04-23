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
