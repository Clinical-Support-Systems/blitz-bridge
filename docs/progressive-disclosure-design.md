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

### Token-economics rationale: Progressive disclosure is a high-variance tradeoff, not an always-win

The benefit of progressive disclosure is **highly dependent on agent workflow.** It is not a universal improvement.

| Tool | Typical compacted payload (chars) | Estimated tokens (chars/4) | With verbose | Estimated tokens (verbose) |
|------|-----------------------------------|---------------------------|--------------|---------------------------|
| `azure_sql_health_check` | ~8,000 | ~2,000 | ~32,000 | ~8,000 |
| `azure_sql_blitz_cache` | ~12,000 | ~3,000 | ~48,000 | ~12,000 |
| `azure_sql_blitz_index` | ~10,000 | ~2,500 | ~40,000 | ~10,000 |
| `azure_sql_current_incident` | ~6,000 | ~1,500 | ~24,000 | ~6,000 |
| **3-tool chain (typical)** | **~30,000** | **~7,500** | **~120,000** | **~30,000** |

**Best case (agent uses selective drill-down):** The progressive disclosure pattern splits each response into a compact summary (targeting <2,000 chars / ~500 tokens) plus opaque handles that the agent can expand on demand. An agent that only needs the BlitzCache warning glossary pays for ~500 summary tokens + ~800 glossary tokens = **~1,300 tokens instead of ~3,000 for the full payload.** Over a multi-step diagnostic session, this can save 60–80% of diagnostic token spend.

**Worst case (agent needs most sections, so requests multiple drill-downs):**  
Suppose an agent running azure_sql_health_check summary (~500 tokens) decides to drill down into findings, then switches to azure_sql_blitz_cache and requests queries (~1,200 tokens), warning glossary (~500 tokens), and ai_prompt (~1,000 tokens). Then azure_sql_blitz_index, requesting existing_indexes (~800 tokens) and column_data_types (~400 tokens). 

Total: summary + 5 drill-downs = 500 + 1200 + 500 + 1000 + 800 + 400 = **~4,400 tokens**.

Compare this to the "typical 3-tool" baseline of **~7,500 tokens** — still a modest win (41% savings). However, if the agent had simply called each tool with `IncludeVerboseResults=false` (our current default), it would have been ~3,000 tokens anyway. **In this worst case, progressive disclosure adds ~1,400 tokens over a minimally-verbose run** if the agent makes multiple requests to expand sections.

**Key insight:** Progressive disclosure saves tokens only when the agent is selective about what it expands. If an agent expands most or all sections, it's paying transaction costs (multiple MCP calls, repeated request/response framing) for marginal savings. Agents that rarely expand save the most; agents that expand everything save the least or may even regress.

**When progressive disclosure wins:**
1. Agents with constrained context windows (e.g., small model, long conversation history, large system prompt).
2. Agents that naturally make diagnostic decisions based on summaries before digging into details.
3. Multi-tool chains where filtering is possible after reading summaries (e.g., "I don't need index analysis for this database, skip it").

**When it's neutral or loses:**
1. Agents that always expand all sections (many details-first diagnostic agents behave this way).
2. Single-tool calls where the summary-only response is not meaningfully smaller than the compacted version.
3. Single-round diagnostic calls where the agent sees one response and reasons in a single shot.

**The Phase 1 tradeoff:** We are accepting the complexity of two response shapes (with-handles and without, for backward compatibility) and a new detail-fetching tool in exchange for *optional* token savings that **some workflows will realize and others will not.** This is a justified tradeoff because:
- The complexity is localized to the detail tool; existing tools' input contracts don't change.
- Backward compatibility means agents that ignore handles work identically to today.
- The telemetry (estimated_payload_tokens) gives us data to measure the actual tradeoff in production.

**This matters because:**
1. Token budgets are finite — agents that *do* benefit from selective disclosure regain capacity for reasoning and recommendation.
2. Cost scales linearly — every unnecessary token costs money on hosted LLM APIs.
3. Latency compounds — larger payloads increase serialization time, network transfer, and model processing time.
4. It's not always an improvement — we must be clear about the conditions under which it helps.

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
- `parentTool` + `kind` are required and strictly validated — **this is explicit dispatch, not opaque magic**. The server validates that each `kind` is a legal value for the given `parentTool`.

### 2.4 Explicit Dispatch vs. Opaque Handles — Design Choice

**Decision:** Use **explicit dispatch** with strict `parentTool` + `kind` validation, not opaque handles that blur the contract.

**Rationale:**
- **Explicit is debuggable:** When a detail fetch fails (e.g., unknown `kind` for that parent), the error message can say exactly what went wrong instead of "handle not found."
- **Server-side validation:** The service maintains a whitelist of legal `(parentTool, kind)` pairs. Requests with invalid combinations are rejected early with clear error messages.
- **Audit trail:** Logs and traces can record which `parentTool` and `kind` were requested, making it easier to spot misconfiguration or client bugs.
- **Tradeoff:** Handles are still opaque from the client's perspective (clients should not parse them), but the dispatch parameters are transparent. This is a deliberate choice to favor debuggability over purity.

**Legal `kind` values per `parentTool`:**

| `parentTool` | Legal `kind` values | Response type | Notes |
|---|---|---|---|
| `azure_sql_health_check` | `findings` | Items array | Drill-down to individual findings with full details |
| `azure_sql_blitz_cache` | `queries`, `warning_glossary`, `ai_prompt`, `ai_advice` | Items array for queries/glossary; text for ai_* | Queries = top cached plans; glossary = warning reference; ai_* = generated text |
| `azure_sql_blitz_index` | `existing_indexes`, `missing_indexes`, `column_data_types`, `foreign_keys`, `ai_prompt`, `ai_advice` | Items array for data; text for ai_* | Separate detail response per index-related section |
| `azure_sql_current_incident` | `waits`, `findings` | Items array | Drill-down to wait types and active findings |

An unknown `parentTool` (e.g., `azure_sql_unknown_tool`) returns a 400-level error with message "Unknown parentTool: azure_sql_unknown_tool."  
An unknown `kind` for a known `parentTool` (e.g., `parentTool=azure_sql_blitz_cache, kind=unknown_section`) returns a 400-level error with message "Unknown kind 'unknown_section' for parentTool 'azure_sql_blitz_cache'. Valid kinds: queries, warning_glossary, ai_prompt, ai_advice."

### 2.5 `IncludeVerboseResults` deprecation

- The flag continues to work in Phase 1. Setting it to `true` still returns `resultSets[]` with raw FRK data.
- Tool descriptions are updated to mark it deprecated with guidance to use `azure_sql_fetch_detail_by_handle` instead.
- The flag is **not repurposed** — same name, same meaning, just discouraged. Repurposing creates incident confusion.

### 2.6 Detail Tool Error Contract

The `azure_sql_fetch_detail_by_handle` tool must specify error handling for the following failure modes:

| Failure Mode | HTTP Status | Error Response | Root Cause | Client Action |
|---|---|---|---|---|
| Malformed handle (base64 decode fails, missing fields) | 400 | `{"error": "malformed_handle", "message": "Handle must be a valid base64-encoded JSON object with fields: version, parentTool, kind, target, ..."}` | Client sent a corrupted or manually-crafted handle | Re-fetch the summary from the parent tool to get a valid handle |
| Malformed explicit dispatch payload (unknown type for maxRows, invalid JSON) | 400 | `{"error": "invalid_request", "message": "maxRows must be a positive integer, got: abc"}` | Client sent invalid JSON or type mismatch in request body | Fix the request and retry |
| Unknown `parentTool` (e.g., `parentTool=azure_sql_unknown_tool`) | 400 | `{"error": "unknown_parent_tool", "message": "Unknown parentTool: 'azure_sql_unknown_tool'. Valid tools: azure_sql_health_check, azure_sql_blitz_cache, azure_sql_blitz_index, azure_sql_current_incident"}` | Client requested a non-existent parent tool | Check the MCP tool listing and use a valid tool name |
| Unknown `kind` for valid `parentTool` (e.g., `parentTool=azure_sql_blitz_cache, kind=nonexistent`) | 400 | `{"error": "unknown_kind", "message": "Unknown kind 'nonexistent' for parentTool 'azure_sql_blitz_cache'. Valid kinds: queries, warning_glossary, ai_prompt, ai_advice"}` | Client requested a section that doesn't exist for this tool | Consult the design doc or tool documentation for valid kinds |
| Authorization drift since parent call (e.g., profile was disabled, client lost access) | 403 | `{"error": "access_denied", "message": "Access to target 'prod-east' is not available. This may indicate the profile was disabled or your authorization has changed since the summary call."}` | The profile or client authorization state changed between the summary call and the detail call | Check that the profile still exists and is enabled; re-run the parent tool to verify access, or contact an administrator |
| Valid handle but detail section has since expired or been garbage-collected (should be extremely rare with stateless design) | 404 | `{"error": "section_not_found", "message": "The requested section is no longer available. This can happen if the handle references parameters that no longer apply (e.g., profile was deleted). Re-run the parent tool to fetch a fresh handle."}` | Handle points to stale request parameters; profile or configuration changed | Re-run the parent tool |
| SQL execution failure during detail fetch (connection timeout, query timeout, permission denied on SQL side) | 500 or 504 | `{"error": "sql_execution_error", "message": "Failed to execute procedure: Connection timeout after 30 seconds"}` | SQL Server is unreachable, unresponsive, or query exceeded timeout | Retry after a delay; check SQL Server connectivity and query load |

**Authorization drift handling — clarification:**  
If authorization state has changed since the parent call (e.g., the profile was disabled, or the client's access was revoked), the detail tool should return the same 403-style authorization failure that the parent tool would return. The exact behavior mirrors the parent tool's error contract: if `azure_sql_health_check` would return 403 when called on the target, then `azure_sql_fetch_detail_by_handle` should also return 403. This keeps error modes consistent and prevents leaking information about which tools are available.

### 2.7 Handle Audit and Natural Row Identifiers

Hockney's handle audit (see [Progressive Disclosure Handle Audit](progressive-disclosure-handle-audit.md)) confirms that each FRK procedure has stable natural row identifiers suitable for drill-down:

| Procedure | Primary Handle | Drill-Down Realism | Notes |
|---|---|---|---|
| `sp_Blitz` | `(Priority, FindingsGroup, Finding, CheckID)` | 🟡 Partial | CheckID is stable; re-run returns all findings; no server-side filtering by CheckID |
| `sp_BlitzCache` | `(DatabaseName, QueryHash, StatementStartOffset, StatementEndOffset)` | 🟡 Partial | QueryHash not exposed in current projection; can track by truncated QueryText + metrics |
| `sp_BlitzIndex` | `(DatabaseName, SchemaName, TableName, IndexName)` | ✅ Full | Server-side narrowing is maximal; required parameters scope to a single table |
| `sp_BlitzFirst` | `(WaitType)` for waits, `(CheckID)` for findings | 🟡 Partial | No server-side filtering; re-run returns all waits/findings for the target |

**Server-side narrowing verdict:**
- `sp_BlitzIndex` supports full server-side narrowing to a single table via required parameters.
- `sp_BlitzCache` supports partial narrowing via `@Top` and `@SortOrder`, but not single-query filtering without client-side post-processing.
- `sp_Blitz` and `sp_BlitzFirst` support no server-side single-row filtering; re-run returns full result set, and detail tool extracts the requested section in memory.

This does not change Phase 1 implementation — all procedures are acceptably fast for stateless re-run. However, it informs Phase 2 decisions about row-level handles and whether exposing `QueryHash` or similar stable identifiers is needed.

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

**Risk: Response size increases slightly.** Adding `handles` and scalar summaries to existing responses makes them ~600–800 chars larger (revised from earlier ~200–400 estimate after factoring in handle verbosity and metadata). A handle object with metadata can be 100–150 chars each, and a response may have 3–5 handles, plus scalar summaries and scope metadata. This is negligible (~150–200 tokens) and is offset by the savings when agents actually use progressive disclosure.

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

## 9. Roadmap: From Phase 1 Additive to Single Canonical Shape (Phase 2+)

Phase 1 is intentionally additive: compacted arrays coexist with handles, and clients can ignore handles entirely. This preserves backward compatibility but creates a "double-emit" risk: responses contain both the old shape and the new shape, and teams must maintain both paths indefinitely.

**Transition plan to a single canonical shape:**

1. **Phase 1 (current): Additive dual-path.** Parent tools emit both compacted arrays (for backward compatibility) and section handles (for progressive disclosure). Agents that ignore handles see no behavior change.

2. **Phase 1.5 (6–12 months): Telemetry and decision.** Via `estimated_payload_tokens` histograms and agent behavior data, we measure:
   - What fraction of agent workflows actually use progressive disclosure?
   - Do agents expand most sections (suggesting drill-down isn't cost-effective) or selectively expand (suggesting draft-down is valuable)?
   - Is there a threshold agent type or model size where progressive disclosure becomes essential?

3. **Phase 2 proposal (decision gate): Break compacted arrays into handles only.** If telemetry shows meaningful adoption of progressive disclosure, Phase 2 will move compacted arrays (`queries`, `findings`, etc.) entirely behind handles. The default parent response becomes true summary-only: just metadata, counts, and handles. This is a **breaking change** and requires:
   - API versioning (e.g., `Accept: application/vnd.blitzbridge.v2+json` header, or a new set of `v2` tools)
   - OR: an opt-in flag like `compact=false` on parent tool requests, defaulting to `compact=true` for backward compatibility
   - Migration period: at least 6 months with both shapes available
   - Clear changelog and deprecation notices to all documented clients

4. **Phase 3+ (Year 2+): Single canonical shape.** Once adoption is high and backward-compatibility burden is justifiable, the old compacted-array shape is removed entirely. Every diagnostic tool response is summary-only with handles; progressive disclosure is the mandatory interaction pattern, not optional.

**Why this phasing matters:**
- Phase 1 adds complexity (dual paths), but it's temporary and necessary to preserve existing workflows.
- Phase 1.5 data informs Phase 2's decision — we don't commit to breaking changes without evidence that progressive disclosure is actually used.
- The transition is gradual: clients have time to migrate, and we don't force a rewrite if agent workflows don't benefit from the new pattern.

**Key constraint:** The decision to break backward compatibility must be made visible, dated, and communicated 6+ months in advance. This is Verbal's responsibility as DevRel lead.

---

## Appendix A. Testability Review (McManus)

**Verdict:** **APPROVE for Phase 1** from a testability standpoint. The summary-plus-handles split does create a few new contract tests, but it does **not** make the suite materially harder to write than the current shape **as long as Phase 1 handles stay section-level and deterministic**. I would **block Phase 2** if handle generation drifts toward cache keys, timestamps, random IDs, or row-level identifiers that are not stable across repeated runs.

### Why Phase 1 remains testable

1. **Existing assertions survive.** Phase 1 keeps the current compacted arrays in place, so today's response-shape tests still work with additive assertions for `handles` and top-level summary scalars.
2. **Section-level handles are fixture-friendly.** A deterministic FRK stub can return predictable drill-down handles when each handle is derived only from:
   - handle version,
   - parent tool,
   - handle kind,
   - normalized request parameters already visible on the parent call (`target`, `sortOrder`, `databaseName`, `schemaName`, `tableName`, `top`, `aiMode`, etc.).
3. **The stateless design helps testing.** Because the doc already rejects server-side caching, tests do not need process affinity, expiry management, or hidden setup between the summary call and the drill-down call.

### D-3 fixture answer: can we make predictable handles?

**Yes — for Phase 1, deterministically.** The fixture can hard-code expected handles for every canned response **if** the implementation treats the handle as a canonical encoding of the request tuple plus section kind, not as a per-execution token.

Concrete examples of what stays deterministic in a fixture:

- `azure_sql_health_check` → one `findings` handle derived from target + normalized priority/database inputs
- `azure_sql_blitz_cache` → section handles like `queries`, `warning_glossary`, `ai_prompt` derived from target + sort order + top + AI mode
- `azure_sql_blitz_index` → section handles derived from target + database/schema/table scope
- `azure_sql_current_incident` → section handles for `waits` and `findings` derived from target + request options only

That means D-3 does **not** need live FRK row identities to make the drill-down contract reproducible.

### What would make tests harder than today

These are the failure modes that would turn this design into a testing tax:

- **Opaque-but-random handles** (GUIDs, nonces, encrypted blobs with per-call entropy)
- **Cache-backed handles** that expire or depend on server memory
- **Handles derived from runtime row values** for Phase 1, especially `sp_BlitzFirst` `CheckDate`
- **Non-canonical input encoding** where omitted/default parameters produce different handles for semantically identical requests

If any of those appear in implementation, the fixture stops being predictable and review should fail.

### Required testability guardrails before implementation

Phase 1 should explicitly require:

1. **Deterministic handle derivation:** same normalized request => same handle across repeated calls and process restarts.
2. **Canonical defaults:** omitted values and defaulted values serialize identically in the handle payload.
3. **Versioned handles:** keep the `v1:`-style discriminator so tests can pin the contract and detect breaking changes cleanly.
4. **Stable handle ordering:** parent responses should emit handles in a fixed order per tool so fixture assertions are simple and repeatable.

### Phase 2 warning I want recorded now

Phase 1 section handles are testable. **Phase 2 row-level handles are not yet approved.** The handle audit already shows why:

- `sp_BlitzCache` row-level drill-down is shaky until `QueryHash` is exposed and proven stable.
- `sp_BlitzFirst` row-level drill-down is inherently unstable because `CheckDate` is execution-time data and findings lack a stable ID.

So the testability gate for Phase 2 is: **do not proceed with row-level handles until each parent tool has a stable row identity and a deterministic fixture story.**

---

## References

- [Hockney's Handle Audit](progressive-disclosure-handle-audit.md) — FRK procedure surface, natural handles, server-side narrowing capability
- [Fenster's Response Shape Prototype](progressive-disclosure-response-shapes.md) — response contracts, generic vs. per-parent tools, IncludeVerboseResults posture
- [PRD](PRD.md) — problem statement, goals, tool surface
- [Implementation Work Items](implementation-work-items.md) — task decomposition, dependencies
- [Team Decisions](../.squad/decisions.md) — active architecture decisions
