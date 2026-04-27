# Progressive Disclosure Design — Phase 1

**Author:** Keaton (Lead)  
**Date:** 2026-04-25  
**Status:** Draft — awaiting team review  
**Scope:** Design only. No `src/` changes.  
**Inputs:** Hockney's handle audit, Fenster's response-shape prototype, PRD, implementation work items

---

## 1. Problem Statement

Blitz Bridge wraps four FRK procedures that return large, multi-section result sets. Today, every tool call serializes the full compacted payload into the MCP response. The primary consumer is an LLM agent whose context window is measured in tokens.

**The cost model is simple:** every character we return costs tokens. At ~4 characters per token (the standard GPT/Claude heuristic), a 20 KB response burns ~5,000 tokens on a single tool call. Agents that chain diagnostics — health check → cache analysis → index review — can consume 15–25K tokens on raw diagnostic data before the model has room to reason about it.

### Token-economics rationale

| Tool | Typical compacted payload (chars) | Estimated tokens (chars/4) | With verbose | Estimated tokens (verbose) |
|------|-----------------------------------|---------------------------|--------------|---------------------------|
| `azure_sql_health_check` | ~8,000 | ~2,000 | ~32,000 | ~8,000 |
| `azure_sql_blitz_cache` | ~12,000 | ~3,000 | ~48,000 | ~12,000 |
| `azure_sql_blitz_index` | ~10,000 | ~2,500 | ~40,000 | ~10,000 |
| `azure_sql_current_incident` | ~6,000 | ~1,500 | ~24,000 | ~6,000 |
| **3-tool chain (typical)** | **~30,000** | **~7,500** | **~120,000** | **~30,000** |

The progressive disclosure pattern splits each response into a compact summary (targeting <2,000 chars / ~500 tokens) plus opaque handles that the agent can expand on demand. An agent that only needs the BlitzCache warning glossary pays for ~500 summary tokens + ~800 glossary tokens instead of ~3,000 for the full payload. Over a multi-step diagnostic session, this can save 60–80% of diagnostic token spend.

**This matters because:**
1. Token budgets are finite — agents that exhaust context on diagnostics lose capacity for reasoning and recommendation.
2. Cost scales linearly — every unnecessary token costs money on hosted LLM APIs.
3. Latency compounds — larger payloads increase serialization time, network transfer, and model processing time.

---

## 2. Proposed Tool Surface Changes

### 2.1 What stays (unchanged)

| Tool | Change | Notes |
|------|--------|-------|
| `azure_sql_target_capabilities` | **None** | Already compact. No handles needed. |
| All tool input signatures | **None** | Existing parameters (`target`, `databaseName`, `sortOrder`, `top`, `maxRows`, etc.) remain identical. |

### 2.2 What changes (existing tools)

The four diagnostic tools gain a `handles` array and top-level summary scalars in their response. The existing `summary`, `notes`, and compacted row arrays (`queries`, `findings`, `waits`, etc.) remain present in Phase 1 for backward compatibility.

**Changed tools:**
- `azure_sql_health_check`
- `azure_sql_blitz_cache`
- `azure_sql_blitz_index`
- `azure_sql_current_incident`

Response changes per tool:
- **New fields:** `handles` array (section-level opaque tokens), top-level scalar summaries (counts, severity indicators)
- **Retained fields:** All existing compacted arrays and scalars stay. `IncludeVerboseResults` continues to work but is marked deprecated in descriptions.
- **No removed fields** in Phase 1.

### 2.3 What gets added (new tool)

One new tool: **`azure_sql_fetch_detail_by_handle`**

**Inputs:**
```json
{
  "target": "prod-east",
  "parentTool": "azure_sql_blitz_cache",
  "kind": "queries",
  "handle": "opaque-token",
  "maxRows": 100
}
```

**Required fields:** `target`, `parentTool`, `kind`, `handle`  
**Optional fields:** `maxRows` (default: 100)

**Why one generic tool, not five per-parent tools:**
- Lower tool-surface growth (1 new tool vs. 5)
- One interaction pattern for all diagnostics: "run summary, then fetch handle"
- `parentTool` + `kind` are required and strictly validated — this is explicit dispatch, not magic

### 2.4 `IncludeVerboseResults` deprecation

- The flag continues to work in Phase 1. Setting it to `true` still returns `resultSets[]` with raw FRK data.
- Tool descriptions are updated to mark it deprecated with guidance to use `azure_sql_fetch_detail_by_handle` instead.
- The flag is **not repurposed** — same name, same meaning, just discouraged. Repurposing creates incident confusion.

---

## 3. Before/After: Typical Agent Flow

### Before (current behavior)

```
Agent                              BlitzBridge
  │                                     │
  ├─ azure_sql_blitz_cache ────────────►│
  │  {target:"prod", sortOrder:"cpu"}   │
  │                                     │
  │◄──── full response (~12,000 chars) ─┤
  │  {                                  │
  │    "target": "prod-east",           │
  │    "sortOrder": "cpu",              │
  │    "queries": [                     │
  │      {"DatabaseName":"AppDb",       │
  │       "QueryType":"Statement",      │
  │       "ExecutionCount":4502,        │
  │       "AvgCPU":1842,               │
  │       "TotalCPU":8293484,           │
  │       "AvgReads":3201,              │
  │       "TotalReads":14414402,        │
  │       "AvgDuration":12045,          │
  │       "TotalDuration":54250590,     │
  │       "Warnings":"implicit conv",   │
  │       "QueryText":"SELECT o.Ord..." │
  │      },                             │
  │      ... 9 more rows ...            │
  │    ],                               │
  │    "warningGlossary": [             │
  │      {"Warning":"implicit conv",    │
  │       "Description":"...",          │
  │       "URL":"..."}                  │
  │    ],                               │
  │    "aiPrompt": "... 2KB text ...",  │
  │    "summary": [...],                │
  │    "notes": [...]                   │
  │  }                                  │
  │                                     │
  │  (~3,000 tokens consumed)           │
  │                                     │
  ├─ Agent reasons about ALL data ──────┤
     (even if it only needed warnings)
```

### After (progressive disclosure)

```
Agent                              BlitzBridge
  │                                     │
  ├─ azure_sql_blitz_cache ────────────►│
  │  {target:"prod", sortOrder:"cpu"}   │
  │                                     │
  │◄── compact response (~2,000 chars) ─┤
  │  {                                  │
  │    "target": "prod-east",           │
  │    "toolName": "azure_sql_blitz_cache",
  │    "sortOrder": "cpu",              │
  │    "databaseName": "AppDb",         │
  │    "aiMode": 2,                     │
  │    "queryCount": 10,                │
  │    "warningGlossaryCount": 4,       │
  │    "hasAiPrompt": true,             │
  │    "hasAiAdvice": false,            │
  │    "summary": [                     │
  │      {"label":"Top CPU query",      │
  │       "value":"SELECT o.Ord..."}    │
  │    ],                               │
  │    "handles": [                     │
  │      {"handle":"bc:prod:cpu:q",     │
  │       "parentTool":"azure_sql_blitz_cache",
  │       "kind":"queries",             │
  │       "title":"Top cached queries", │
  │       "preview":"10 rows by CPU",   │
  │       "severity":"warning",         │
  │       "itemCount":10},              │
  │      {"handle":"bc:prod:cpu:wg",    │
  │       "parentTool":"azure_sql_blitz_cache",
  │       "kind":"warning_glossary",    │
  │       "title":"Warning glossary",   │
  │       "preview":"4 glossary rows",  │
  │       "severity":"info",            │
  │       "itemCount":4},               │
  │      {"handle":"bc:prod:cpu:ai",    │
  │       "parentTool":"azure_sql_blitz_cache",
  │       "kind":"ai_prompt",           │
  │       "title":"FRK AI prompt",      │
  │       "preview":"Prompt available", │
  │       "severity":"info"}            │
  │    ],                               │
  │    "notes": []                       │
  │  }                                  │
  │                                     │
  │  (~500 tokens consumed)             │
  │                                     │
  ├─ Agent decides it needs warnings ───┤
  │                                     │
  ├─ azure_sql_fetch_detail_by_handle ─►│
  │  {target:"prod",                    │
  │   parentTool:"azure_sql_blitz_cache",
  │   kind:"warning_glossary",          │
  │   handle:"bc:prod:cpu:wg"}          │
  │                                     │
  │◄──── detail response (~800 chars) ──┤
  │  {                                  │
  │    "target": "prod-east",           │
  │    "parentTool": "azure_sql_blitz_cache",
  │    "kind": "warning_glossary",      │
  │    "handle": "bc:prod:cpu:wg",      │
  │    "scope": {"sortOrder":"cpu"},    │
  │    "items": [                       │
  │      {"Warning":"implicit conv",    │
  │       "Description":"...",          │
  │       "URL":"..."},                 │
  │      ... 3 more ...                 │
  │    ],                               │
  │    "notes": []                      │
  │  }                                  │
  │                                     │
  │  (~200 tokens for glossary only)    │
  │                                     │
  │  Total: ~700 tokens vs ~3,000       │
  │  Savings: ~77%                      │
```

---

## 4. Backward Compatibility Analysis

### Hypothesis: Adding new tools preserves backward compatibility. **Confirmed.**

**Reasoning:**

1. **Existing tool inputs are unchanged.** No parameters are added, removed, or reinterpreted on any existing tool. A client sending today's payloads gets a valid response tomorrow.

2. **Existing response fields are retained.** The compacted arrays (`queries`, `findings`, `waits`, `existingIndexes`, etc.) remain in Phase 1 responses. New fields (`handles`, top-level scalars like `queryCount`) are **additive**. JSON consumers that ignore unknown fields — which is the standard MCP client behavior — see no difference.

3. **`IncludeVerboseResults` keeps working.** The flag is deprecated in documentation, not removed. Clients relying on `resultSets[]` continue to get them.

4. **The new tool is opt-in.** `azure_sql_fetch_detail_by_handle` is a new tool in the MCP listing. No existing client calls it unless it explicitly chooses to. An agent that never reads `handles` from the response operates identically to today.

5. **No response fields are removed or retyped.** This is the critical constraint. If we removed `queries` from `AzureSqlBlitzCacheResponse` in Phase 1, that would break clients. We don't.

**Risk: Response size increases slightly.** Adding `handles` and scalar summaries to existing responses makes them ~200–400 chars larger. This is negligible (<100 tokens) and is offset by the savings when agents actually use progressive disclosure.

**Phase 2 risk (flagged for future).** If Phase 2 removes compacted arrays from the default response (moving them behind handles only), that is a breaking change requiring a version bump or opt-in flag. Phase 1 does not do this.

---

## 5. Recommendation AGAINST Server-Side Caching of Full Result Sets

### The tempting optimization

When a parent tool returns handles, the obvious implementation is: run the FRK procedure once, cache the full result set in memory (keyed by handle), and serve detail requests from cache. This avoids re-running the procedure on drill-down.

### Why we must not do this

**1. Memory pressure is unbounded.** Each FRK result set can be 50–500 KB depending on the procedure and parameter surface. With N concurrent agents × M profiles × K sort orders, server-side caching creates an O(N×M×K) memory footprint with no natural eviction signal. BlitzBridge is designed to run as a lightweight sidecar or global tool — not a stateful cache.

**2. Cache invalidation is impossible to get right.** FRK procedures return point-in-time snapshots. A cached BlitzCache result from 30 seconds ago may show a query plan that has since been evicted from the plan cache. Serving stale data from a diagnostic tool is worse than re-running — it's actively misleading during incident response.

**3. Handle lifetime creates hidden coupling.** If handles point to a server-side cache, clients must use them within an expiry window. This creates a temporal coupling that doesn't exist in the current stateless model. When the cache entry expires, the handle becomes a dead reference. The error mode is confusing: "handle not found" looks like a bug, not a timeout.

**4. Stdio transport has no session affinity.** In stdio mode, the MCP server may be restarted between calls (e.g., Claude Desktop spawns a new process per conversation). Any in-process cache is lost. Externalizing to Redis or disk would contradict the "zero infrastructure" deployment model.

### Latency tradeoff: re-running FRK procedures

The alternative is stateless: the detail tool re-runs the FRK procedure with the same parameters encoded in the handle, then extracts only the requested section.

**Are any FRK procs too expensive to re-run?**

| Procedure | Typical duration | Re-run acceptable? | Notes |
|-----------|-----------------|-------------------|-------|
| `sp_Blitz` | 2–8 sec | ✅ Yes | Scans all databases; duration is consistent. No server-side narrowing, but the procedure is designed for repeated invocation. |
| `sp_BlitzCache` | 1–5 sec | ✅ Yes | Reads plan cache DMVs. `@Top` + `@SortOrder` keep it bounded. Plan cache reads are lightweight. |
| `sp_BlitzIndex` | 1–3 sec | ✅ Yes | **Best case for re-run.** Already scoped to a single table via required parameters. Server-side narrowing is maximal. Duration is consistently low. |
| `sp_BlitzFirst` | 1–3 sec | ✅ Yes | Point-in-time snapshot by design. Re-running is actually desirable during incident response — you want fresh data, not stale cache. |

**Verdict:** No FRK procedure is expensive enough to justify server-side caching. The slowest case (`sp_Blitz` at ~8 seconds on large instances) is acceptable for a drill-down interaction where the agent has already reviewed the summary and made a deliberate decision to expand a section. The latency is a feature: the agent gets fresh data.

### What the handle encodes instead

Handles are opaque tokens that encode the original procedure parameters (target, sort order, database, etc.) plus the requested section kind. The detail tool decodes the handle, re-runs the procedure with those parameters, and extracts just the requested section. This is stateless, restartable, and cache-free.

**Handle encoding recommendation:** Base64-encoded JSON or a structured string (e.g., `bc:prod:cpu:queries`). The exact format is an implementation detail — clients must treat handles as opaque. The server validates handle structure and rejects malformed tokens.

---

## 6. Telemetry Recommendation: Estimated Payload Size

### Instrument immediately — independent of progressive disclosure shipping

Every MCP tool response should emit a metric recording the estimated token cost of the serialized payload:

```
estimated_payload_tokens = serialized_json_chars / 4
```

**Why chars/4?** This is the standard heuristic for GPT-4 / Claude tokenization. It's wrong for any specific string (real tokenizers produce variable-length tokens), but it's consistently useful as an order-of-magnitude signal for capacity planning.

### What to instrument

| Metric name | Type | Tags | Description |
|-------------|------|------|-------------|
| `blitzbridge.tool.payload_chars` | Histogram | `tool`, `target`, `profile` | Raw character count of serialized JSON response |
| `blitzbridge.tool.estimated_tokens` | Histogram | `tool`, `target`, `profile` | `payload_chars / 4` |

### Implementation guidance

- Emit on **every tool response**, not just progressive disclosure responses. This gives us baseline data before and after the feature ships.
- Use the existing `BlitzBridge.Diagnostics` meter (per Decision 004, work item B-8).
- Record after JSON serialization, before MCP framing. The serialized `string.Length` is the measurement point.
- Include whether `IncludeVerboseResults` was true as a tag to quantify verbose mode's token cost.

### Why this matters before progressive disclosure ships

1. **Baseline.** Without before-data, we can't prove progressive disclosure improved token efficiency. Ship telemetry first, measure, then ship the feature.
2. **Anomaly detection.** A sudden spike in `estimated_tokens` for a specific tool or target signals a configuration issue (e.g., MaxRows set too high) or an FRK regression (new columns in result sets).
3. **Cost attribution.** Teams can see which profiles and procedures are the most expensive callers and adjust their agent workflows accordingly.
4. **Independent value.** Even if progressive disclosure is deferred or descoped, payload size telemetry is permanently useful for capacity planning.

---

## 7. Open Questions for Phase 2

### 7.1 Remove compacted arrays from default response?

Phase 1 keeps compacted arrays (`queries`, `findings`, etc.) alongside `handles` for backward compatibility. Phase 2 could move these behind handles only, making the default response truly summary-only. This is a **breaking change** and needs a versioning strategy (opt-in flag, API version header, or new tool names).

**Owner:** Keaton (Lead)  
**Decision needed by:** Phase 2 planning

### 7.2 Expose `QueryHash` in BlitzCache responses?

Hockney flagged that `QueryHash` is not in our projected columns. Without it, clients cannot build stable per-query handles for row-level drill-down. Phase 1 uses section-level handles (all queries vs. one query), but Phase 2 row-level handles need `QueryHash`.

**Owner:** Hockney  
**Dependency:** D-1 (FRK parameter validation) to confirm QueryHash stability across FRK versions

### 7.3 Row-level handles vs. section-level handles?

Phase 1 uses section-level handles only (e.g., "all queries" or "all findings"). Phase 2 could add row-level handles (e.g., "this specific query by QueryHash"). This increases handle count per response and adds complexity to the detail tool's dispatch logic.

**Owner:** Keaton + Fenster  
**Gating question:** Do agent workflows actually need single-row drill-down, or is section-level with `maxRows` sufficient?

### 7.4 Cursor-based pagination for large sections?

Phase 1's detail tool accepts `maxRows` but not `cursor`. If a section has 500 rows and `maxRows` is 100, the agent gets the first 100 with no way to page. Phase 2 could add cursor-based pagination to the detail response.

**Owner:** Fenster  
**Complexity:** Low if we use offset-based cursors. High if we need keyset pagination for stable ordering across re-runs.

### 7.5 sp_BlitzLock integration?

Not yet wrapped. Phase 2 should include a handle audit (Hockney) and tool implementation (Fenster) for `sp_BlitzLock`. The deadlock analysis procedure returns XML-heavy payloads that are prime candidates for progressive disclosure.

**Owner:** Hockney (audit), Fenster (implementation)

### 7.6 Handle format versioning?

If handle encoding changes between versions, clients holding old handles get confusing errors. Should handles include a version byte or prefix?

**Owner:** Fenster  
**Recommendation:** Yes, prefix handles with a version discriminator (e.g., `v1:bc:prod:cpu:queries`). Cheap insurance.

### 7.7 Agent-side caching guidance?

Should we publish guidance for MCP clients on whether they may cache parent responses and handles within a conversation? This is outside our server's control, but documentation can set expectations about handle freshness.

**Owner:** Verbal

---

## 8. Summary of Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | Add one new tool: `azure_sql_fetch_detail_by_handle` | Lower surface growth than per-parent detail tools; explicit dispatch via required `parentTool` + `kind` |
| D2 | Keep compacted arrays in Phase 1 responses | Backward compatibility; agents that ignore `handles` work identically to today |
| D3 | Deprecate (not remove, not repurpose) `IncludeVerboseResults` | Clear migration path; same name = same meaning = no confusion |
| D4 | No server-side caching of result sets | Memory, staleness, stdio restarts, deployment simplicity — all argue against it |
| D5 | Re-run FRK procedures on detail fetch (stateless) | All four procs are ≤8 sec; fresh data is better than stale cache during incidents |
| D6 | Instrument `estimated_payload_tokens` on every tool call now | Independent of progressive disclosure; provides baseline, anomaly detection, and cost attribution |
| D7 | Handles are opaque, encode original parameters, server-validated | Stateless design; no cache dependency; handles survive server restarts |
| D8 | Adding new tools preserves backward compatibility — confirmed | No existing inputs changed, no fields removed, new fields are additive, new tool is opt-in |

---

## References

- [Hockney's Handle Audit](progressive-disclosure-handle-audit.md) — FRK procedure surface, natural handles, server-side narrowing capability
- [Fenster's Response Shape Prototype](progressive-disclosure-response-shapes.md) — response contracts, generic vs. per-parent tools, IncludeVerboseResults posture
- [PRD](PRD.md) — problem statement, goals, tool surface
- [Implementation Work Items](implementation-work-items.md) — task decomposition, dependencies
- [Team Decisions](../.squad/decisions.md) — active architecture decisions
