# Progressive Disclosure Handle Audit — FRK Procedure Surface

**Audit Date:** 2026-04-24  
**Auditor:** Hockney (Data & Azure SQL Specialist)  
**Scope:** Phase 1 FRK procedures currently wrapped in Blitz Bridge  
**Procedures Audited:** `sp_Blitz`, `sp_BlitzCache`, `sp_BlitzIndex`, `sp_BlitzFirst`  
**Procedures Not Yet Wrapped:** `sp_BlitzLock` (acknowledged; deferred to Phase 2)

---

## Executive Summary

Each FRK procedure returns result sets that require different strategies for row identification and drill-down realism. This audit documents:

1. **Natural handles** – columns that uniquely identify a row across repeated procedure executions
2. **Server-side narrowing** – which FRK parameters allow limiting results to a single handle vs. forcing full re-run + client-side filtering
3. **Result set shape** – summary rows vs. detail rows, with proposed response splits per procedure

**Key Findings:**
- All four procedures have stable natural handles in their primary result sets (Priority/CheckID for Blitz, QueryHash for BlitzCache, DatabaseName/SchemaName/TableName for BlitzIndex, WaitType for BlitzFirst waits).
- Server-side narrowing is **impossible** for Blitz and BlitzFirst (procedures don't accept row-level filters).
- BlitzCache and BlitzIndex support partial narrowing via `@Top`/`@SortOrder` and `@DatabaseName`/`@SchemaName`/`@TableName` respectively, but **cannot filter to a single row** without client-side post-processing.
- Our current response model already implements appropriate summary/detail splits with `MaxRows` compaction.

---

## Procedure Audits

### 1. sp_Blitz (Health Check)

**Repo Evidence:**
- Tool request: `AzureSqlHealthCheckRequest` (target, databaseName, minimumPriority, expertMode, maxRows, includeVerboseResults)
- Tool response: `AzureSqlHealthCheckResponse` (target, totalFindings, summary, findings[], resultSets[])
- Result mapper: `MapHealthCheck()` projects columns: `[Priority, FindingsGroup, Finding, DatabaseName, Details, CheckID, URL]`
- Service invocation: `sp_Blitz` with optional `@DatabaseName`, `@IgnorePrioritiesAbove`

**Natural Row Identifiers (Handles):**
- **Primary composite handle:** `(Priority, FindingsGroup, Finding, CheckID)`
  - CheckID is a numeric identifier for each distinct finding rule.
  - Priority and FindingsGroup cluster related findings.
  - **Stability:** ✅ **Stable** – CheckID is FRK's internal finding identifier and remains constant across repeated executions targeting the same issue.
- **Secondary handle:** `(DatabaseName, Finding, CheckID)` when database-scoped findings are needed.
- **Drill-down realism:** 🟡 **Partial.** A user can drill down into a specific finding via CheckID, but `sp_Blitz` returns **all findings** in each execution; there is no `@FindingID` parameter to narrow results server-side.

**Server-Side Narrowing Capability:**
- **`@IgnorePrioritiesAbove`:** Filters result set to exclude findings above a priority threshold. However, the filtered-out findings are **still detected** and counted; they're only suppressed from output.
- **`@DatabaseName`:** Accepted but **does not narrow** findings to a single database. sp_Blitz scans all user databases by default.
- **Narrowing verdict:** ❌ **No server-side single-handle narrowing.** To drill down to a specific finding across repeated runs, the client must:
  1. Re-run sp_Blitz with the same parameters.
  2. Filter the response in memory to CheckID of interest.
  3. Compare rows to detect state changes or new instances of the same finding.

**Result Set Shape & Proposed Summary/Detail Split:**

| Result Set | Shape | Current Handling | Recommendation |
|-----------|-------|------------------|-----------------|
| Findings (Primary) | **Detail** – one row per finding instance per database. May duplicate findings across databases. | Compacted to `maxRows` (default 25); projected to ~7 key columns | **Keep current split.** Summary layer provides high-level counts; detail layer shows each finding. When `IncludeVerboseResults=true`, raw FRK table is included for full audit trail. |
| Server Info (Secondary, if `@CheckServerInfo=1`) | **Summary** – aggregate server-level stats (CPUs, memory, version, etc.) | Not currently exposed | **Future feature.** Could be added as optional metadata block if needed for inventory scans. |

**Uncertainty Notes:**
- 🟡 FRK documentation does not explicitly define CheckID stability across versions. We assume version 8.19 (vendored in frk-install.sql) maintains CheckID consistency; this should be validated in integration tests.
- 🟡 Azure SQL may suppress certain findings. Current code does not distinguish Azure-specific behavior from standard behavior.

---

### 2. sp_BlitzCache (Plan Cache Diagnostics)

**Repo Evidence:**
- Tool request: `AzureSqlBlitzCacheRequest` (target, databaseName, sortOrder, top, expertMode, aiMode, maxRows, includeVerboseResults)
- Tool response: `AzureSqlBlitzCacheResponse` (target, sortOrder, databaseName, aiMode, summary, queries[], warningGlossary[], aiPrompt, aiAdvice, resultSets[])
- Result mapper: `MapBlitzCache()` projects columns: `[DatabaseName, QueryType, ExecutionCount, ExecutionsPerMinute, AvgCPU, TotalCPU, AvgReads, TotalReads, AvgDuration, TotalDuration, Warnings, QueryText]`
- Service invocation: `sp_BlitzCache` with `@Top`, `@SortOrder`, optional `@DatabaseName`, `@ExpertMode`, `@AI`

**Natural Row Identifiers (Handles):**
- **Primary composite handle:** `(DatabaseName, QueryHash, StatementStartOffset, StatementEndOffset)`
  - QueryHash is a 128-bit hash of the query text; FRK uses this internally to identify unique queries.
  - However, **repo code does not expose QueryHash** in the projected columns; only QueryText (truncated).
  - **Stability:** ✅ **Stable for the same query text** – but FRK exposes this at plan cache level, not query level. Multiple plans with the same query text hash may exist.
- **Practical handle within our response:** `(DatabaseName, QueryType, truncated(QueryText))`
  - This is what our mapper projects.
  - **Stability:** 🟡 **Moderately stable** – truncated query text may collide (especially for long queries), but within a single result set, rows are distinguishable by execution count and timing metrics.
- **Secondary handle for drill-down:** Clients observing high CPU or duration can track `(AvgCPU, TotalCPU, ExecutionCount)` tuple across repeated runs to detect trend changes or escalation.

**Server-Side Narrowing Capability:**
- **`@Top`:** Narrows result to the top N rows (default 10). Combined with `@SortOrder` (cpu|duration|executions|reads), this limits output to the N most resource-intensive queries **by that metric only**.
- **`@SortOrder`:** Defaults to 'cpu'; can be 'duration', 'executions', 'reads'. FRK returns the top queries ranked by the chosen metric.
- **`@DatabaseName`:** FRK **does not** support single-database filtering in its parameters (not seen in public docs or our code). BlitzCache scans all databases' plan caches by default.
- **Narrowing verdict:** 🟡 **Partial server-side narrowing.** 
  - ✅ Client can narrow to "top N by CPU" or "top N by reads" using `@Top` + `@SortOrder`.
  - ❌ Cannot narrow to a single query hash without re-running and client-side post-filtering.
  - ❌ Cannot narrow to a specific database's plan cache.

**Result Set Shape & Proposed Summary/Detail Split:**

| Result Set | Shape | Current Handling | Recommendation |
|-----------|-------|------------------|-----------------|
| Queries (Primary) | **Detail** – one row per cached query plan, ranked by sort order. May include plans from multiple databases. | Compacted to `maxRows` (default 50); key metrics and truncated QueryText included | **Keep current split.** Summary layer counts/ranks by metric; detail layer shows each query. When running repeated narrowed queries (e.g., "top 5 by CPU"), trends become visible across executions. |
| Warning Glossary (Secondary) | **Summary** – warning codes and their meanings. One row per distinct warning. | Compacted to min(maxRows, 10); columns: [Warning, Description, URL] | **Keep current.** This is reference data, not detail. No drill-down needed. |
| AI Prompt / AI Advice | **Metadata** – text generated by FRK (if @AI > 0). Not a result set row. | Extracted as `aiPrompt` and `aiAdvice` string fields | **Keep current.** These are scalar outputs, not tabular rows. |

**Uncertainty Notes:**
- 🟡 QueryHash is not exposed in our projected columns. If clients need to track a specific query across runs, they must match on truncated QueryText + metrics, which is fragile.
- 🟡 FRK documentation does not clarify whether QueryHash is stable across SQL Server versions or if plan recompilation changes the hash.
- 🟡 `@DatabaseName` support is not documented; may be undocumented or unsupported. Recommend explicit test in D-1 (FRK parameter validation).

---

### 3. sp_BlitzIndex (Table Index Analysis)

**Repo Evidence:**
- Tool request: `AzureSqlBlitzIndexRequest` (target, databaseName, schemaName, tableName, mode, thresholdMb, expertMode, aiMode, maxRows, includeVerboseResults)
- Tool response: `AzureSqlBlitzIndexResponse` (target, databaseName, schemaName, tableName, aiMode, summary, existingIndexes[], missingIndexes[], columnDataTypes[], foreignKeys[], aiPrompt, aiAdvice, resultSets[])
- Result mapper: `MapBlitzIndex()` projects across four result sets:
  - RS0 (Existing Indexes): `[DatabaseName, SchemaName, TableName, IndexName, IndexType, KeyColumnNames, IncludeColumnNames, IndexUsageSummary, Impact]`
  - RS1 (Missing Indexes): `[DatabaseName, SchemaName, TableName, MissingIndexDetails, MagicBenefitNumber, CreateTsql]`
  - RS2 (Column Data Types): `[ColumnName, SystemTypeName, MaxLength, IsNullable, IsIdentity]`
  - RS3 (Foreign Keys): `[ForeignKeyName, ParentTableName, ReferencedTableName]`
- Service invocation: `sp_BlitzIndex` with required `@DatabaseName`, `@SchemaName`, `@TableName`, `@Mode`, `@ThresholdMB`

**Natural Row Identifiers (Handles):**

| Result Set | Primary Handle | Stability | Notes |
|-----------|---|---|---|
| **Existing Indexes** | `(DatabaseName, SchemaName, TableName, IndexName)` | ✅ **Stable** – Schema metadata is immutable within the index object's lifetime. | Index identity is guaranteed by object identity in the catalog. Rename the index, and the handle changes; drop/recreate, handle is lost. |
| **Missing Indexes** | `(DatabaseName, SchemaName, TableName, MissingIndexDetails)` | 🟡 **Moderately stable** – MissingIndexDetails is a text description (e.g., "CREATE NONCLUSTERED INDEX..."). Text changes if column set changes, but SQL Server's missing index DMV is deterministic for a given query workload. | If the same query pattern re-emerges, FRK regenerates the same MissingIndexDetails. However, if workload shifts, recommendations change. |
| **Column Data Types** | `(ColumnName)` – within scope of DatabaseName.SchemaName.TableName | ✅ **Stable** – Column metadata is immutable within a single object. | Column rename changes the handle; retyping a column creates a new column, effectively changing the handle. |
| **Foreign Keys** | `(ForeignKeyName)` – within scope of the table | ✅ **Stable** – FK is a schema object with an identity. | As with indexes, rename/drop/recreate changes the handle. |

**Server-Side Narrowing Capability:**
- **`@DatabaseName`, `@SchemaName`, `@TableName`:** These are **required parameters** and act as a full row-level filter.
  - ✅ **Uniquely scopes the analysis to a single table.** sp_BlitzIndex cannot return results for multiple tables in one execution.
  - This is the **tightest possible server-side narrowing** among the four procedures.
- **`@Mode`:** Controls analysis depth (0=diagnose, 1=summarize, 2=usage detail, 3=missing index detail, 4=diagnose+details). Does NOT filter rows; only controls which result sets are populated.
- **Narrowing verdict:** ✅ **Full server-side narrowing to a single table.** 
  - Client specifies the exact table, and sp_BlitzIndex returns all indexes, missing index recommendations, columns, and foreign keys for that table only.
  - No need for client-side post-filtering beyond the result-set-level compaction we already do.

**Result Set Shape & Proposed Summary/Detail Split:**

| Result Set | Shape | Current Handling | Recommendation |
|-----------|-------|------------------|-----------------|
| Existing Indexes | **Detail** – one row per index on the target table. All indexes are included. | Compacted to `maxRows` (default 100); key columns + IndexUsageSummary metric | **Keep current.** Each index is a distinct row; users may want to see all indexes or just heavily-used ones. Summary notes the count; detail shows each. |
| Missing Indexes | **Detail** – one row per FRK recommendation. Ordered by "magic benefit number" (priority ranking). | Compacted to `maxRows` (default 100) | **Keep current.** Summary mentions count of recommendations; detail shows each with generated CREATE TSQL. |
| Column Data Types | **Summary** – metadata reference. One row per column. Compacted to min(maxRows, 25). | Currently included when IncludeVerboseResults is true; columns: [ColumnName, SystemTypeName, MaxLength, IsNullable, IsIdentity] | **Keep current.** This supports drill-down: user sees an index on columns X, Y, Z; can look at column types to understand data alignment. |
| Foreign Keys | **Summary** – metadata reference. One row per FK. Compacted to min(maxRows, 25). | Currently included. Columns: [ForeignKeyName, ParentTableName, ReferencedTableName] | **Keep current.** Helps users understand table relationships when considering index changes. |

**Uncertainty Notes:**
- 🟡 MissingIndexDetails is a text string; if FRK generates it dynamically, format changes across versions could break handle matching. Recommend storing the hash or ID of the recommendation instead (if FRK exposes it).
- 🟡 `@Mode` parameter is not currently used in our request model (we don't expose it to MCP clients). This may be intentional (we run in diagnose mode by default), but limits flexibility.

---

### 4. sp_BlitzFirst (Current Incident Snapshot)

**Repo Evidence:**
- Tool request: `AzureSqlCurrentIncidentRequest` (target, databaseName, expertMode, maxRows, includeVerboseResults)
- Tool response: `AzureSqlCurrentIncidentResponse` (target, totalWaitRows, totalFindingRows, summary, waits[], findings[], resultSets[])
- Result mapper: `MapCurrentIncident()` projects across two result sets:
  - RS0 (Waits): `[CheckDate, WaitType, WaitTimeSeconds, WaitTimeMsPerMinute, SignalWaitTimeMs, ResourceWaitTimeMs]`
  - RS1 (Findings): `[Priority, FindingsGroup, Finding, Details, URL]`
- Service invocation: `sp_BlitzFirst` with optional `@ExpertMode`; rejects `@DatabaseName` (runs in current database context only)

**Natural Row Identifiers (Handles):**

| Result Set | Primary Handle | Stability | Notes |
|-----------|---|---|---|
| **Waits** | `(CheckDate, WaitType)` | 🟡 **Snapshot-scoped.** CheckDate is the execution timestamp; WaitType is the SQL Server wait category (e.g., 'PAGEIOLATCH_SH'). Same WaitType may appear in multiple runs, but CheckDate anchors each snapshot to a point-in-time. | WaitType is stable across SQL Server versions; it's part of the public DMV surface. However, **CheckDate is generated at execution time**, so repeated runs produce different CheckDates. Handle stability is **low for tracking across time** unless you strip the CheckDate and assume "same WaitType at different times is the same issue". |
| **Findings** | `(Priority, FindingsGroup, Finding)` – implicitly tied to CheckDate | 🟡 **Snapshot-scoped.** Same as Waits: findings are point-in-time. Repeated runs produce different CheckDates. The Finding text (like "CPU pressure detected") may repeat across snapshots, but without a CheckID (unlike sp_Blitz), there's no stable finding identifier. | sp_BlitzFirst does not expose a CheckID or FindingID; it only gives us Priority, FindingsGroup, Finding text. This is less stable than sp_Blitz for drill-down. |

**Server-Side Narrowing Capability:**
- **No narrowing parameters accepted.** sp_BlitzFirst runs in the context of the target database's current session and returns a **point-in-time snapshot** of waits and findings.
- **`@ExpertMode`:** If enabled, may expand certain diagnostic details, but does not filter rows.
- **`@DatabaseName`:** Explicitly rejected by our service layer (throws ArgumentException). sp_BlitzFirst always runs in the current database context.
- **Narrowing verdict:** ❌ **No server-side row-level narrowing.**
  - To drill down into a specific wait type or finding across time, the client must:
    1. Run sp_BlitzFirst multiple times (e.g., every 5 seconds during an incident).
    2. Compare CheckDate timestamps and WaitType/Finding names.
    3. Track trends (e.g., "PAGEIOLATCH_SH waits increased from 120 to 450 WaitTimeMsPerMinute in 10 seconds").
  - This is the intended use case for sp_BlitzFirst: repeated sampling during active incidents.

**Result Set Shape & Proposed Summary/Detail Split:**

| Result Set | Shape | Current Handling | Recommendation |
|-----------|-------|------------------|-----------------|
| Waits | **Detail** – one row per observed wait type in this snapshot. May show 5-20+ distinct wait types depending on system load. | Compacted to `maxRows` (default 25); all six key metrics included | **Keep current, but clarify sampling semantics.** Summary notes total wait rows before compaction; detail shows top N waits. **Caution:** If user relies on a specific wait appearing in results, compaction may drop it. Consider making MaxRows adaptive to wait count or documenting the limitation. |
| Findings | **Detail** – one row per finding in this snapshot. May include 0-10+ findings. | Compacted to `maxRows` (default 25); columns: [Priority, FindingsGroup, Finding, Details, URL] | **Keep current.** Similar to Waits: summary counts; detail shows findings. No stable handle (no FindingID), so drill-down is time-series only (compare snapshots). |

**Uncertainty Notes:**
- 🟡 **Critical:** CheckDate is generated by sp_BlitzFirst at execution time, not by our service. If CheckDate is NULL or poorly formatted, snapshot correlation breaks. Recommend validating CheckDate non-null in integration tests.
- 🟡 sp_BlitzFirst findings lack a CheckID equivalent (unlike sp_Blitz). If a finding appears in multiple snapshots, we have no way to correlate it to a specific finding rule. FRK's public docs do not clarify this limitation. Recommend requesting CheckID in findings output (would require FRK enhancement).
- 🟡 **Snapshot frequency risk:** Users may expect repeated calls to sp_BlitzFirst to reveal incident escalation, but if the wait type or finding name changes slightly (e.g., capitalization, whitespace), matching fails. Recommend documenting that drill-down comparisons should use case-insensitive matching or fuzzy scoring.

---

## Not Yet Wrapped: sp_BlitzLock (Deadlock Analysis)

**Status:** Not currently implemented in our codebase (no request/response models, no service method, no MCP tool).

**FRK Documentation Summary:**
- sp_BlitzLock analyzes deadlock graph XML from the SQL Server error log.
- Returns deadlock details: participating objects, query text, lock types, victim selection.
- Primary result set: one row per deadlock event (identified by EventTime + Victim ID).

**Preliminary Handle Assessment (for Phase 2 planning):**
- **Natural handle:** `(EventTime, DeadlockGraph)` – EventTime is when the deadlock occurred; DeadlockGraph is the XML payload.
- **Stability:** ✅ **Stable** – deadlock XML is immutable after the event.
- **Server-side narrowing:** ❌ No parameters to filter deadlocks; returns recent deadlocks from the error log. Narrowing requires log rotation or client-side time-range filtering.

**Deferred to Phase 2:** Will require dedicated audit and integration testing.

---

## Recommendations for Progressive Disclosure Implementation

### 1. **Implement Handle Tracking in Client Code**

For each procedure, document the recommended "composite key" tuple:

| Procedure | Recommended Composite Key | Use Case |
|-----------|--|--|
| sp_Blitz | (CheckID, Priority, FindingsGroup, Finding) | Track finding state across repeated health checks |
| sp_BlitzCache | (DatabaseName, truncated(QueryText), ExecutionCount, AvgCPU) | Observe query performance trend over time |
| sp_BlitzIndex | (DatabaseName, SchemaName, TableName, IndexName) | Monitor index health after changes |
| sp_BlitzFirst | (CheckDate, WaitType) or (CheckDate, Priority, Finding) | Compare consecutive snapshots during incidents |

### 2. **Extend Response Models to Include Natural Handles (Phase 2+)**

For better drill-down support, consider:
- Adding optional `handle` field to each row in the compacted response (e.g., `{ ..., "handle": "PAGEIOLATCH_SH" }`).
- Allows clients to build a lightweight lookup table without parsing multiple fields.
- **Not required for MVP** but improves UX for multi-call drill-down workflows.

### 3. **Document Compaction Semantics**

Current code compacts results using `MaxRows` before returning. For users expecting 100% of rows:
- Clearly note in Notes field when results are compacted: "Results are compacted by default to reduce agent token usage. Set IncludeVerboseResults to true when you need all FRK result sets."
- Consider a `truncated` boolean flag in response to signal compaction.

### 4. **Add Server-Side Narrowing Parameters Where Possible (Phase 2+)**

- **sp_BlitzCache:** Investigate `@DatabaseName` support; if not available in FRK, document this limitation.
- **sp_BlitzFirst:** Consider adding an optional `@WaitTypeFilter` (comma-delimited) to FRK wrapper if FRK adds support.

### 5. **Validate FRK Parameter Stability (D-1 Task)**

- Confirm CheckID, QueryHash, IndexName, WaitType are stable across FRK versions and SQL Server versions.
- Add integration tests that compare handles across repeated executions on stable workloads.

---

## Summary Table: Handle Audit Results

| Procedure | Primary Handle | Server-Side Narrowing | Detail Shape | Drill-Down Strategy |
|-----------|---|---|---|---|
| **sp_Blitz** | (CheckID, Priority, Finding) | ❌ None | Findings detail | Compare CheckID across snapshots; track state changes |
| **sp_BlitzCache** | (DatabaseName, QueryHash, QueryText) | 🟡 Partial (@Top, @SortOrder) | Queries detail | Track (AvgCPU, ExecutionCount) tuples across runs; compare trends |
| **sp_BlitzIndex** | (DatabaseName, SchemaName, TableName, IndexName/MissingIndexDetails) | ✅ Full (@DatabaseName, @SchemaName, @TableName scopes to single table) | Indexes detail, recommendations detail | Compare IndexUsageSummary and MissingIndexDetails across runs |
| **sp_BlitzFirst** | (CheckDate, WaitType) or (CheckDate, Finding) | ❌ None | Waits/Findings detail | Time-series comparison of consecutive snapshots; no stable identifier |

---

## Open Questions for Team Review

1. **CheckID Stability (sp_Blitz):** Can we rely on CheckID remaining constant across FRK versions? Or should we maintain a mapping table in BlitzBridge?
2. **QueryHash Exposure (sp_BlitzCache):** Should we expose QueryHash in addition to truncated QueryText? Would help clients implement stable query-level drill-down.
3. **FindingID for sp_BlitzFirst:** Should we request that FRK expose a stable FindingID (like sp_Blitz's CheckID) for sp_BlitzFirst findings?
4. **Compaction Signal (All):** Should we add a `isTruncated` boolean to each response to clarify when MaxRows compaction is applied?
5. **sp_BlitzLock Timeline:** When should we audit sp_BlitzLock? Can we defer to Phase 2, or is it critical path for MVP?

---

## References

- **Repo Evidence:** FrkProcedureService.cs, FrkResultMapper.cs, request/response models in src/BlitzBridge.McpServer/Models/
- **FRK Install:** samples/docker-compose-demo/sql/frk-install.sql (v8.19, 2024-02-22)
- **FRK Public Docs:** https://www.brentozar.com/blitz/documentation/ (general; procedure-specific docs vary)
- **FRK GitHub:** https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit (source code reference)
- **Blitz Bridge PRD:** docs/PRD.md, docs/implementation-work-items.md

---

## Approval & Sign-Off

**Audit Completion:** Ready for Keaton (Lead) review.  
**Next Steps:** Incorporate findings into D-1 (FRK parameter validation) and D-2 (AiMode FRK version compat) work items.
