# MCP Tools Reference

Blitz Bridge exposes diagnostic tools via the Model Context Protocol (MCP). This guide explains what each tool does, how progressive disclosure works, and when to use handle-based detail fetching.

## Overview

All diagnostic tools return a **summary** by default: counts, severity indicators, top results, and handles to expanded sections. This design keeps token usage low for constrained agent contexts while preserving the ability to drill into details on demand.

**Default interaction pattern:**

1. Call a query tool (e.g., `azure_sql_blitz_cache`) → get a summary (~500 tokens)
2. Review the summary and handles
3. If you need details, call `azure_sql_fetch_detail_by_handle` on one or more handles (~200–1,000 tokens per detail)
4. Repeat step 3 for other sections, or move to a different diagnostic tool

**Backward compatibility:** If you ignore handles and read only the summary fields, the tools work identically to earlier versions. All existing compacted result arrays (`queries`, `findings`, `waits`, etc.) remain in responses.

---

## Query Tools

### `azure_sql_target_capabilities`

**Lists configured profiles and their allowed procedures.**

**Inputs:**

```json
{
  "target": "prod-east"  // (optional) If omitted, lists all profiles
}
```

**Output:**

```json
{
  "target": "prod-east",
  "toolName": "azure_sql_target_capabilities",
  "currentDatabase": "master",
  "engineEdition": 5,
  "engineEditionName": "Azure SQL Database",
  "allowedDatabases": ["AppDb", "DBAtools"],
  "allowedProcedures": ["sp_Blitz", "sp_BlitzCache", "sp_BlitzIndex", "sp_BlitzFirst", "sp_BlitzLock", "sp_BlitzWho"],
  "installedProcedures": ["sp_Blitz", "sp_BlitzCache", "sp_BlitzIndex", "sp_BlitzFirst"],
  "supportsAiPromptGeneration": true,
  "supportsDirectAiCalls": true,
  "hasPromptConfigTable": true,
  "hasProviderConfigTable": false,
  "summary": [
    {
      "title": "Target profile",
      "severity": "info",
      "message": "Target points at Azure SQL Database in database 'master'."
    },
    {
      "title": "Installed FRK procedures",
      "severity": "info",
      "message": "Detected 4 supported FRK procedure(s)."
    },
    {
      "title": "AI prompt generation available",
      "severity": "info",
      "message": "This target can surface FRK-generated AI prompts when the underlying proc supports it."
    }
  ],
  "notes": []
}
```

**Use case:** Before calling other tools, verify which procedures are available on your target and whether AI features are enabled.

---

### `azure_sql_health_check`

**Runs sp_Blitz to scan database health.**

**Inputs:**

```json
{
  "target": "prod-east",
  "databaseName": "AppDb",           // (optional) Override; scans all if omitted
  "minimumPriority": 50,              // (optional) Filter to high-priority findings
  "expertMode": false,                // (optional) Include expert-mode checks
  "maxRows": 25                       // (optional) Limit compacted result arrays
}
```

**Output (summary view):**

```json
{
  "target": "prod-east",
  "toolName": "azure_sql_health_check",
  "databaseName": "AppDb",
  "totalFindings": 8,
  "highestVisiblePriority": 50,
  "visibleFindingGroups": ["Urgent", "Warning", "Informational"],
  "summary": [
    {
      "title": "Database health overview",
      "severity": "warning",
      "message": "Returned 8 findings at priority 50 (highest visible)"
    }
  ],
  "handles": [
    {
      "kind": "findings",
      "title": "All findings",
      "preview": "8 items grouped by priority",
      "severity": "critical",
      "itemCount": 8
    }
  ],
  "notes": [
    "Results are compacted by default to reduce agent token usage.",
    "Use azure_sql_fetch_detail_by_handle with returned handles for expanded sections."
  ]
}
```

**Progressive disclosure:** If you need the full detail on each finding (CheckID, description, recommendation), call:

```json
{
  "target": "prod-east",
  "parentTool": "azure_sql_health_check",
  "kind": "findings",
  "handle": "(opaque token from response)"
}
```

---

### `azure_sql_blitz_cache`

**Runs sp_BlitzCache to analyze query plan cache and identify high-CPU or high-IO queries.**

**Inputs:**

```json
{
  "target": "prod-east",
  "databaseName": "AppDb",            // (optional) Override
  "sortOrder": "cpu",                 // (optional) "cpu", "reads", "writes", "duration", "executions", "memory"
  "top": 10,                          // (optional) Number of top queries
  "expertMode": false,                // (optional)
  "aiMode": 2,                        // (optional) 0=off, 1=prompts, 2=prompts+advice
  "maxRows": 25                       // (optional)
}
```

**Output (summary view):**

```json
{
  "target": "prod-east",
  "toolName": "azure_sql_blitz_cache",
  "databaseName": "AppDb",
  "sortOrder": "cpu",
  "aiMode": 2,
  "queryCount": 10,
  "warningGlossaryCount": 4,
  "hasAiPrompt": true,
  "hasAiAdvice": false,
  "summary": [
    {
      "title": "Top queries by cpu",
      "severity": "info",
      "message": "Returned 10 compact query row(s) from 10 total query row(s)."
    },
    {
      "title": "Warning glossary",
      "severity": "info",
      "message": "Returned 4 glossary row(s) from 4 total glossary row(s)."
    },
    {
      "title": "AI prompt available",
      "severity": "info",
      "message": "The FRK result set included a generated AI prompt."
    }
  ],
  "handles": [
    {
      "kind": "queries",
      "title": "Top cached queries",
      "preview": "10 rows by CPU usage",
      "severity": "info",
      "itemCount": 10
    },
    {
      "kind": "warning_glossary",
      "title": "Warning glossary",
      "preview": "4 glossary entries",
      "severity": "info",
      "itemCount": 4
    },
    {
      "kind": "ai_prompt",
      "title": "FRK AI prompt",
      "preview": "Prompt available for your query pattern",
      "severity": "info"
    }
  ],
  "notes": [
    "Results are compacted by default to reduce agent token usage.",
    "Use azure_sql_fetch_detail_by_handle with returned handles for expanded sections."
  ]
}
```

**Progressive disclosure:**

- **`kind: "queries"`** → Full query details: execution count, CPU, reads, query text, warnings
- **`kind: "warning_glossary"`** → Definitions of warnings (implicit conversions, missing indexes, etc.)
- **`kind: "ai_prompt"`** → sp_BlitzCache's AI mode prompt (read-only text)
- **`kind: "ai_advice"`** → If available, FRK AI advice for this query set

**Token savings example:**

- Summary: ~500 tokens
- Full payload if all sections requested: ~3,000 tokens
- Selective drill-down (queries + glossary only): ~1,300 tokens
- Savings: 57% vs. full, 160% over summary-only

---

### `azure_sql_blitz_index`

**Runs sp_BlitzIndex to surface index recommendations, missing indexes, and table structure analysis.**

**Inputs:**

```json
{
  "target": "prod-east",
  "databaseName": "AppDb",          // (required) Database to analyze
  "schemaName": "dbo",               // (optional, default: "dbo") Schema containing table
  "tableName": "Orders",             // (required) Table to analyze
  "mode": 0,                         // (optional, default: 0) FRK mode
  "thresholdMb": 250,                // (optional, default: 250) Size threshold in MB
  "expertMode": false,               // (optional) Include expert mode checks
  "aiMode": 2,                       // (optional) 0=off, 1=prompts, 2=prompts+advice
  "maxRows": 25                      // (optional) Limit compacted result arrays
}
```

**Output (summary view):**

```json
{
  "target": "prod-east",
  "toolName": "azure_sql_blitz_index",
  "databaseName": "AppDb",
  "schemaName": "dbo",
  "tableName": "Orders",
  "aiMode": 2,
  "existingIndexCount": 42,
  "missingIndexCount": 3,
  "columnDataTypeCount": 8,
  "foreignKeyCount": 2,
  "hasAiPrompt": true,
  "hasAiAdvice": false,
  "summary": [
    {
      "label": "Missing indexes",
      "value": "3 recommendations on Orders table"
    },
    {
      "label": "Existing indexes",
      "value": "8 indexes on this table"
    }
  ],
  "handles": [
    {
      "kind": "existing_indexes",
      "title": "Existing indexes",
      "preview": "8 indexes listed",
      "severity": "info",
      "itemCount": 8
    },
    {
      "kind": "missing_indexes",
      "title": "Missing index recommendations",
      "preview": "3 recommendations",
      "severity": "warning",
      "itemCount": 3
    },
    {
      "kind": "column_data_types",
      "title": "Column data types",
      "preview": "8 columns listed",
      "severity": "info",
      "itemCount": 8
    },
    {
      "kind": "foreign_keys",
      "title": "Foreign key relationships",
      "preview": "2 foreign keys",
      "severity": "info",
      "itemCount": 2
    },
    {
      "kind": "ai_prompt",
      "title": "FRK AI prompt",
      "preview": "Prompt available for this table",
      "severity": "info"
    }
  ],
  "notes": []
}
```

**Progressive disclosure:**

- **`kind: "existing_indexes"`** → All indexes on the table with column lists and usage statistics
- **`kind: "missing_indexes"`** → Detailed index recommendations with estimated impact
- **`kind: "column_data_types"`** → Column definitions, data types, and nullability
- **`kind: "foreign_keys"`** → Foreign key relationships and referenced tables
- **`kind: "ai_prompt"`** → (if aiMode >= 1) FRK AI prompt for this table structure
- **`kind: "ai_advice"`** → (if aiMode >= 2) FRK AI recommendations

---

### `azure_sql_current_incident`

**Runs sp_BlitzFirst to surface immediate blocking, waits, and active problems.**

**Inputs:**

```json
{
  "target": "prod-east",
  "databaseName": "AppDb",
  "expertMode": false,
  "maxRows": 25
}
```

**Output (summary view):**

```json
{
  "target": "prod-east",
  "toolName": "azure_sql_current_incident",
  "databaseName": "AppDb",
  "totalWaitRows": 7,
  "totalFindingRows": 5,
  "topWaitTypes": ["PAGEIOLATCH_SH", "WRITELOG", "CXPACKET"],
  "summary": [
    {
      "title": "Blocking detected",
      "severity": "critical",
      "message": "Current blocking incident detected on this database."
    },
    {
      "title": "Wait types",
      "severity": "warning",
      "message": "Detected 7 distinct wait types; top 3: PAGEIOLATCH_SH, WRITELOG, CXPACKET"
    }
  ],
  "handles": [
    {
      "kind": "waits",
      "title": "Wait types",
      "preview": "7 wait types by frequency",
      "severity": "warning",
      "itemCount": 7
    },
    {
      "kind": "findings",
      "title": "Active findings",
      "preview": "Issues detected in current incident",
      "severity": "critical",
      "itemCount": 5
    }
  ],
  "notes": [
    "Results are compacted by default to reduce agent token usage.",
    "Use azure_sql_fetch_detail_by_handle with returned handles for expanded sections."
  ]
}
```

**Progressive disclosure:**

- **`kind: "waits"`** → Detailed wait type statistics (count, percentage, duration)
- **`kind: "findings"`** → Incident-specific findings (locks, queries involved, recommendations)

---

## Detail Fetching Tool

### `azure_sql_fetch_detail_by_handle`

**Fetches expanded content for a section identified by a handle from a query tool.**

This tool enables progressive disclosure by fetching only the sections an agent needs, rather than returning everything upfront.

**Inputs:**

```json
{
  "target": "prod-east",
  "parentTool": "azure_sql_blitz_cache",
  "kind": "queries",
  "handle": "(opaque base64-encoded token from parent response)",
  "maxRows": 100  // (optional) Limit result count
}
```

**Output:**

```json
{
  "target": "prod-east",
  "parentTool": "azure_sql_blitz_cache",
  "kind": "queries",
  "handle": "(same as input)",
  "scope": {
    "sortOrder": "cpu",
    "top": 10
  },
  "items": [
    {
      "databaseName": "AppDb",
      "queryHash": "0x1A2B3C4D...",
      "executionCount": 4502,
      "avgCPU": 1842,
      "totalCPU": 8293484,
      "avgReads": 3201,
      "totalReads": 14414402,
      "avgDuration": 12045,
      "totalDuration": 54250590,
      "warnings": "implicit_conversion",
      "queryText": "SELECT o.OrderID, o.Status FROM Orders o WHERE o.Status = @status"
    }
    // ... 9 more rows
  ],
  "notes": []
}
```

**Valid `(parentTool, kind)` pairs:**

| Parent Tool | Kind | Content | Use Case |
|---|---|---|---|
| `azure_sql_health_check` | `findings` | Detailed findings with CheckID, priority, description | Review all database health issues |
| `azure_sql_blitz_cache` | `queries` | Query metrics, text, warnings | Analyze top cached plans |
| `azure_sql_blitz_cache` | `warning_glossary` | Warning type definitions | Understand warning categories |
| `azure_sql_blitz_cache` | `ai_prompt` | FRK AI prompt (text) | Review the prompt sent to the AI |
| `azure_sql_blitz_cache` | `ai_advice` | FRK AI advice (text) | Read generated recommendations |
| `azure_sql_blitz_index` | `existing_indexes` | Index list with column definitions | Audit all indexes |
| `azure_sql_blitz_index` | `missing_indexes` | Missing index recommendations | Plan index creation |
| `azure_sql_blitz_index` | `column_data_types` | Table/column schema | Review data types |
| `azure_sql_blitz_index` | `foreign_keys` | Foreign key relationships | Understand table relationships |
| `azure_sql_blitz_index` | `ai_prompt` | FRK AI prompt (text) | Review the prompt |
| `azure_sql_blitz_index` | `ai_advice` | FRK AI advice (text) | Read recommendations |
| `azure_sql_current_incident` | `waits` | Wait type statistics | Analyze blocking patterns |
| `azure_sql_current_incident` | `findings` | Active incident findings | Understand immediate issues |

**Error responses:**

- **Unknown parentTool** → 400 error with valid tool list
- **Unknown kind for parentTool** → 400 error with valid kinds for that parent
- **Malformed handle** → 400 error with expected handle format
- **Authorization lost since parent call** → 403 error (profile disabled or access revoked)
- **SQL execution failure** → 500 error with diagnostic message

---

## Working with Large Result Sets: A Triage-to-Detail Example

Real diagnostic sessions often start broad and narrow based on findings. Here's a concrete example using progressive disclosure.

### Scenario

You suspect a performance incident on a production SQL Server. You want to:
1. Check current blocking (narrow incident scope)
2. If blocking is high, drill into wait types
3. Then switch to cache analysis to see if index changes helped recently

### Step 1: Run incident triage

Call `azure_sql_current_incident` with minimal parameters to get a summary:

```json
{
  "target": "prod-east",
  "databaseName": "AppDb"
}
```

Response (~400 tokens):

```json
{
  "target": "prod-east",
  "toolName": "azure_sql_current_incident",
  "databaseName": "AppDb",
  "totalWaitRows": 7,
  "totalFindingRows": 5,
  "topWaitTypes": ["PAGEIOLATCH_SH", "WRITELOG"],
  "summary": [
    {
      "title": "Blocking detected",
      "severity": "critical",
      "message": "Current blocking incident detected on this database."
    }
  ],
  "handles": [
    {
      "kind": "waits",
      "title": "Wait types",
      "itemCount": 7
    },
    {
      "kind": "findings",
      "title": "Active findings",
      "itemCount": 5
    }
  ]
}
```

**Analysis:** Blocking is detected and marked critical. You decide to drill into wait types to understand the root cause.

### Step 2: Drill into waits

Call `azure_sql_fetch_detail_by_handle` for the `waits` section:

```json
{
  "target": "prod-east",
  "parentTool": "azure_sql_current_incident",
  "kind": "waits",
  "handle": "(opaque token from response)"
}
```

Response (~600 tokens):

```json
{
  "parentTool": "azure_sql_current_incident",
  "kind": "waits",
  "items": [
    {
      "waitType": "PAGEIOLATCH_SH",
      "waitCount": 2847,
      "percentage": 72.1,
      "avgWaitTimeMs": 125.3
    },
    {
      "waitType": "WRITELOG",
      "waitCount": 612,
      "percentage": 15.5,
      "avgWaitTimeMs": 45.2
    },
    {
      "waitType": "CXPACKET",
      "waitCount": 489,
      "percentage": 12.4,
      "avgWaitTimeMs": 8.1
    }
    // ... 4 more
  ]
}
```

**Analysis:** Dominant wait is `PAGEIOLATCH_SH` (page I/O latch), suggesting disk I/O contention. This is consistent with recent index changes or missing indexes. You decide to check the cache to see which queries are driving the I/O.

### Step 3: Switch tools and triage cache analysis

Call `azure_sql_blitz_cache` with `sortOrder: "reads"` to surface high-I/O queries:

```json
{
  "target": "prod-east",
  "databaseName": "AppDb",
  "sortOrder": "reads"
}
```

Response (~500 tokens):

```json
{
  "target": "prod-east",
  "toolName": "azure_sql_blitz_cache",
  "databaseName": "AppDb",
  "sortOrder": "reads",
  "aiMode": 2,
  "queryCount": 10,
  "warningGlossaryCount": 3,
  "hasAiPrompt": false,
  "hasAiAdvice": false,
  "summary": [
    {
      "title": "Top queries by reads",
      "severity": "warning",
      "message": "Returned 10 compact query row(s) from 10 total query row(s)."
    },
    {
      "title": "Warning glossary",
      "severity": "info",
      "message": "Returned 3 glossary row(s) from 3 total glossary row(s)."
    }
  ],
  "handles": [
    {
      "kind": "queries",
      "title": "Top cached queries",
      "itemCount": 10
    },
    {
      "kind": "warning_glossary",
      "title": "Warning glossary",
      "itemCount": 3
    }
  ]
}
```

**Analysis:** The high-I/O query is a wide table scan on Orders. You want to see if there are missing index recommendations and check for warnings.

### Step 4: Drill into queries to see details

```json
{
  "target": "prod-east",
  "parentTool": "azure_sql_blitz_cache",
  "kind": "queries",
  "handle": "(handle from step 3)"
}
```

Response (~1,200 tokens):

```json
{
  "parentTool": "azure_sql_blitz_cache",
  "kind": "queries",
  "items": [
    {
      "databaseName": "AppDb",
      "executionCount": 4502,
      "avgReads": 3201,
      "totalReads": 14414402,
      "avgCPU": 1842,
      "queryText": "SELECT * FROM Orders WHERE Status = @status",
      "warnings": "implicit_conversion, missing_index"
    }
    // ... 9 more
  ]
}
```

**Analysis:** The top query has an implicit conversion warning and a missing_index warning. This confirms the I/O problem is likely index-related.

### Step 5: Drill into the warning glossary to understand the issue

```json
{
  "target": "prod-east",
  "parentTool": "azure_sql_blitz_cache",
  "kind": "warning_glossary",
  "handle": "(handle from step 3)"
}
```

Response (~400 tokens):

```json
{
  "parentTool": "azure_sql_blitz_cache",
  "kind": "warning_glossary",
  "items": [
    {
      "warning": "implicit_conversion",
      "description": "Query contains implicit type conversion in WHERE clause; may prevent index usage",
      "url": "https://..."
    },
    {
      "warning": "missing_index",
      "description": "No index on this column; full table scan likely",
      "url": "https://..."
    },
    {
      "warning": "outdated_statistics",
      "description": "Statistics were last updated > 30 days ago",
      "url": "https://..."
    }
  ]
}
```

**Analysis:** The warnings confirm implicit conversion + missing index + stale stats. You now have enough context to recommend:
- Add an index on `Orders.Status`
- Update statistics
- Review the query to use explicit type casting

### Token cost comparison

**Old approach (full payloads):**
1. Current incident (full): ~3,000 tokens
2. BlitzCache with reads (full): ~3,000 tokens
3. **Total: ~6,000 tokens** before you make any recommendations

**Progressive disclosure (this example):**
1. Current incident (summary): ~400 tokens
2. Waits detail: ~600 tokens
3. BlitzCache summary: ~500 tokens
4. Queries detail: ~1,200 tokens
5. Glossary detail: ~400 tokens
6. **Total: ~3,100 tokens** — 48% savings while providing the same insights

**Key insight:** Progressive disclosure saves the most when you can triage summaries before drilling into details. If you need all sections anyway, the overhead is just a few extra MCP round-trips (minimal impact on latency).

---

## Deprecated Features

### `IncludeVerboseResults` parameter

This parameter continues to work on all query tools but is **deprecated**. Setting it to `true` returns additional raw result sets (`resultSets[]`) in the response, but you should use `azure_sql_fetch_detail_by_handle` instead for better token efficiency.

If you depend on `IncludeVerboseResults`, your code will continue to work. Plan a migration to handle-based detail fetching at your convenience.

---

## Backward Compatibility Note

All query tools retain their existing response fields for backward compatibility. If your agent ignores `handles` and only reads the summary fields, your code works identically to earlier versions. New fields (handles, counts, summaries) are additive and do not break existing clients.

