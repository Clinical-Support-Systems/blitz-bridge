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
