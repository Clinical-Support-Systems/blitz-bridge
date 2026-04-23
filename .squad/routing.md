# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture and scope | Keaton | Service boundaries, diagnostics flow, hard constraints |
| .NET MCP backend implementation | Fenster | MCP handlers, read-only execution logic, server wiring |
| Azure SQL and diagnostic target modeling | Hockney | Target configuration, SQL connectivity strategy, FRK integration constraints |
| Testing and quality gates | McManus | Unit/integration tests, edge cases, reviewer checks |
| Documentation and operator guidance | Verbal | README updates, setup docs, usage examples |
| Session logging | Scribe | Automatic — never needs routing |
| Work monitoring | Ralph | Backlog scanning, issue/PR progress loop |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Keaton |
| `squad:keaton` | Pick up issue and complete the work | Keaton |
| `squad:fenster` | Pick up issue and complete the work | Fenster |
| `squad:hockney` | Pick up issue and complete the work | Hockney |
| `squad:mcmanus` | Pick up issue and complete the work | McManus |
| `squad:verbal` | Pick up issue and complete the work | Verbal |

## Rules

1. Eager by default — spawn all agents who can safely start now, including anticipatory test/doc work.
2. Scribe always runs after substantial work in background mode.
3. Quick factual checks can be answered directly by coordinator when context is already known.
4. Reviewer rejection lockout is enforced strictly per artifact revision cycle.
5. Shared decisions are written via inbox drop files and merged by Scribe.
