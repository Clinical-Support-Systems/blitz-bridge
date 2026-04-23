# Project Context

- **Owner:** Kori Francis
- **Project:** Read-only .NET MCP server for Brent Ozar First Responder Kit diagnostics against preconfigured Azure SQL targets
- **Stack:** .NET, MCP server patterns, Azure SQL, First Responder Kit diagnostics
- **Created:** 2026-04-23T17:59:01Z

## Learnings

Backend agent initialized with day-1 project context.

### Batch 0: Request Binding Fix (2026-04-23)

- MCP tool binding in ModelContextProtocol.AspNetCore treated complex DTO parameters as a required top-level `request` argument, so flattened `arguments` payloads were rejected before business logic ran.
- Dual-mode signatures (flattened scalars + optional request DTO) removed the brittle binding dependency while preserving compatibility with existing request-wrapper callers.
- After binding was fixed, tool invocations progressed to real target validation (`Unknown or disabled target ...`), confirming the unhandled binder exception path was eliminated.
- Pattern now extensible: other tools can follow same dual-mode approach for consistent MCP interop.

**Commit:** `a02d4dd` - Approved by Kori Francis
- Validation: 4/4 regression tests pass (TUnit)
- All 4 staging validations passed (solution build, test project build, test exe)
- No unintended files staged (only implementation + tests + csproj)
- Commit message includes dual-mode signature design and regression test coverage
- Pushed to `origin/main` as direct commit (no PR needed for direct main workflow)
