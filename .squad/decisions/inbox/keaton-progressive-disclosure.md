# Decision: Progressive Disclosure Phase 1 Design

**Author:** Keaton (Lead)  
**Date:** 2026-04-25  
**Status:** Proposed — pending team review  
**Scope:** Response shape changes, new detail tool, caching posture, telemetry

## Key Decisions

1. **One new tool: `azure_sql_fetch_detail_by_handle`** — required `parentTool` + `kind` discriminators; stateless re-run, no cache.

2. **Phase 1 is additive only** — existing compacted arrays stay in responses alongside new `handles` array and scalar summaries. No fields removed. Adding new tools preserves backward compatibility (confirmed).

3. **`IncludeVerboseResults` deprecated, not removed or repurposed** — flag keeps working in Phase 1. Documentation marks it deprecated.

4. **No server-side caching of result sets** — memory pressure, point-in-time staleness, stdio restarts, and lightweight deployment model all prohibit it. All FRK procs are ≤8 sec; re-run is the correct strategy.

5. **Telemetry: `estimated_payload_tokens` on every tool call** — instrument immediately using `BlitzBridge.Diagnostics` meter, independent of progressive disclosure shipping. chars/4 heuristic.

6. **Handles are opaque, encode original parameters** — server-validated, stateless, survive restarts. Clients must not parse them.

## Blocking For

- **Fenster:** Awaiting review gate before implementation
- **Hockney:** QueryHash exposure decision deferred to Phase 2
- **McManus:** Test plan for `azure_sql_fetch_detail_by_handle` follows review

## Related

- `docs/progressive-disclosure-design.md` (full design)
- `docs/progressive-disclosure-handle-audit.md` (Hockney's input)
- `docs/progressive-disclosure-response-shapes.md` (Fenster's input)
