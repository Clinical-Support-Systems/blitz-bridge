# Progressive Disclosure Response Shapes (Phase 1 Prototype)

Prototype only. This sketches response contracts for a summary-plus-handles pattern. No `src/` implementation changes are proposed here.

## Working Rules

- Parent diagnostic tools stay small: summary facts first, expandable detail second.
- Handles are opaque tokens. Clients should not parse them.
- In Phase 1, handles should point to **sections/slices** of a response, not every individual row. That keeps the first response predictable.
- Drill-down responses should identify the parent tool and the handle kind so failures are obvious at 2 a.m.

## Common Handle Shape

```json
{
  "handle": "opaque-token",
  "parentTool": "azure_sql_blitz_cache",
  "kind": "queries",
  "title": "Top cached queries",
  "preview": "10 of 10 rows shown in compact summary",
  "severity": "info",
  "itemCount": 10,
  "totalCount": 10
}
```

Recommended common fields:

- `handle`: opaque token returned by the parent tool
- `parentTool`: originating tool name
- `kind`: discriminator for the detail payload
- `title`: human-readable label
- `preview`: one-line explanation of what the handle expands
- `severity`: `info|warning|critical` where meaningful
- `itemCount` / `totalCount`: optional count hints

## Recommended Drill-Down Tool

### Recommended name

`azure_sql_fetch_detail_by_handle`

### Inputs

```json
{
  "target": "profile-name",
  "parentTool": "azure_sql_blitz_cache",
  "kind": "queries",
  "handle": "opaque-token",
  "maxRows": 100,
  "cursor": null
}
```

### Response envelope

```json
{
  "target": "profile-name",
  "parentTool": "azure_sql_blitz_cache",
  "kind": "queries",
  "handle": "opaque-token",
  "scope": {
    "databaseName": "AppDb",
    "sortOrder": "cpu"
  },
  "items": [],
  "notes": []
}
```

For text-heavy detail (`ai_prompt`, `ai_advice`), replace `items` with:

```json
{
  "contentType": "text/plain",
  "content": "..."
}
```

## Tool-by-Tool Sketches

## 1) `azure_sql_target_capabilities`

### Compact response

```json
{
  "toolName": "azure_sql_target_capabilities",
  "target": "prod-east",
  "currentDatabase": "DBAtools",
  "engineEditionName": "Azure SQL Database",
  "allowlistedDatabaseCount": 3,
  "allowlistedProcedureCount": 5,
  "installedProcedureCount": 5,
  "supportsAiPromptGeneration": true,
  "supportsDirectAiCalls": false,
  "summary": [],
  "handles": [
    { "kind": "allowed_databases", "title": "Allowed databases" },
    { "kind": "allowed_procedures", "title": "Allowed procedures" },
    { "kind": "installed_procedures", "title": "Installed procedures" },
    { "kind": "ai_readiness", "title": "AI readiness details" }
  ],
  "notes": []
}
```

### Drill-down

- **Recommended tool:** `azure_sql_fetch_detail_by_handle`
- **Kinds:** `allowed_databases`, `allowed_procedures`, `installed_procedures`, `ai_readiness`
- **Detail shape:** list of strings for the first three; small object for `ai_readiness`

### Separate-tool option

- `azure_sql_target_capabilities_detail(target, handle, maxRows?)`

### IncludeVerboseResults posture

- **N/A** for current contract. No existing verbose toggle here.

## 2) `azure_sql_health_check`

### Compact response

```json
{
  "toolName": "azure_sql_health_check",
  "target": "prod-east",
  "databaseName": "AppDb",
  "totalFindings": 143,
  "highestVisiblePriority": 1,
  "visibleFindingGroups": ["Reliability", "Performance", "Security"],
  "summary": [],
  "handles": [
    {
      "kind": "findings",
      "title": "Health findings",
      "preview": "25 compact rows; 143 total findings",
      "severity": "warning"
    }
  ],
  "notes": []
}
```

### Drill-down

- **Recommended tool:** `azure_sql_fetch_detail_by_handle`
- **Inputs:** `target`, `parentTool=azure_sql_health_check`, `kind=findings`, `handle`, optional `maxRows`, optional `cursor`
- **Response shape:** `items` array of detailed finding rows with full projected columns such as `Priority`, `FindingsGroup`, `Finding`, `DatabaseName`, `Details`, `CheckID`, `URL`

### Separate-tool option

- `azure_sql_health_check_detail(target, handle, maxRows?, cursor?)`

### IncludeVerboseResults posture

- **Recommendation: deprecate**
- **Why keep it:** short-term compatibility; useful as an escape hatch during rollout
- **Why deprecate it:** it duplicates the new drill-down contract and defeats the point of progressive disclosure
- **Why not repurpose it:** same name, different meaning is how you manufacture incident confusion

## 3) `azure_sql_current_incident`

### Compact response

```json
{
  "toolName": "azure_sql_current_incident",
  "target": "prod-east",
  "totalWaitRows": 18,
  "totalFindingRows": 7,
  "topWaitTypes": ["LCK_M_X", "PAGEIOLATCH_SH"],
  "summary": [],
  "handles": [
    {
      "kind": "waits",
      "title": "Current waits",
      "preview": "18 wait rows",
      "severity": "warning"
    },
    {
      "kind": "findings",
      "title": "Incident findings",
      "preview": "7 finding rows",
      "severity": "warning"
    }
  ],
  "notes": []
}
```

### Drill-down

- **Recommended tool:** `azure_sql_fetch_detail_by_handle`
- **Kinds:** `waits`, `findings`
- **Response shape:** `items` array of detailed wait or finding rows, plus `scope` describing the point-in-time snapshot

### Separate-tool option

- `azure_sql_current_incident_detail(target, handle, maxRows?, cursor?)`

### IncludeVerboseResults posture

- **Recommendation: deprecate**
- **Keep case:** easy migration path for existing clients
- **Deprecate case:** current-incident data is already snapshot-oriented; a dedicated detail fetch is cleaner than bolting the raw sets back onto the parent response

## 4) `azure_sql_blitz_cache`

### Compact response

```json
{
  "toolName": "azure_sql_blitz_cache",
  "target": "prod-east",
  "databaseName": "AppDb",
  "sortOrder": "cpu",
  "aiMode": 2,
  "queryCount": 10,
  "warningGlossaryCount": 4,
  "hasAiPrompt": true,
  "hasAiAdvice": false,
  "summary": [],
  "handles": [
    {
      "kind": "queries",
      "title": "Top cached queries",
      "preview": "10 compact query rows",
      "severity": "warning"
    },
    {
      "kind": "warning_glossary",
      "title": "Warning glossary",
      "preview": "4 glossary rows",
      "severity": "info"
    },
    {
      "kind": "ai_prompt",
      "title": "FRK AI prompt",
      "preview": "Prompt text available",
      "severity": "info"
    }
  ],
  "notes": []
}
```

### Drill-down

- **Recommended tool:** `azure_sql_fetch_detail_by_handle`
- **Kinds:** `queries`, `warning_glossary`, `ai_prompt`, `ai_advice`
- **Response shape:**
  - `queries`: full projected query rows, potentially with less-truncated text fields
  - `warning_glossary`: glossary rows
  - `ai_prompt` / `ai_advice`: text payload

### Separate-tool option

- `azure_sql_blitz_cache_detail(target, handle, maxRows?, cursor?)`

### IncludeVerboseResults posture

- **Recommendation: deprecate**
- **Keep case:** helpful while clients migrate off inline raw result sets
- **Deprecate case:** this tool has the highest token-bloat risk, so leaving verbose mode as a first-class path undermines the whole redesign
- **Repurpose case (not recommended):** could mean “inline AI artifacts only,” but that is too subtle to be dependable

## 5) `azure_sql_blitz_index`

### Compact response

```json
{
  "toolName": "azure_sql_blitz_index",
  "target": "prod-east",
  "databaseName": "AppDb",
  "schemaName": "dbo",
  "tableName": "Orders",
  "aiMode": 2,
  "existingIndexCount": 6,
  "missingIndexCount": 2,
  "columnDataTypeCount": 14,
  "foreignKeyCount": 3,
  "hasAiPrompt": true,
  "hasAiAdvice": false,
  "summary": [],
  "handles": [
    {
      "kind": "existing_indexes",
      "title": "Existing indexes",
      "preview": "6 index rows",
      "severity": "info"
    },
    {
      "kind": "missing_indexes",
      "title": "Missing index suggestions",
      "preview": "2 recommendation rows",
      "severity": "warning"
    },
    {
      "kind": "column_data_types",
      "title": "Column metadata",
      "preview": "14 column rows",
      "severity": "info"
    },
    {
      "kind": "foreign_keys",
      "title": "Foreign keys",
      "preview": "3 foreign key rows",
      "severity": "info"
    },
    {
      "kind": "ai_prompt",
      "title": "FRK AI prompt",
      "preview": "Prompt text available",
      "severity": "info"
    }
  ],
  "notes": []
}
```

### Drill-down

- **Recommended tool:** `azure_sql_fetch_detail_by_handle`
- **Kinds:** `existing_indexes`, `missing_indexes`, `column_data_types`, `foreign_keys`, `ai_prompt`, `ai_advice`
- **Response shape:** `items` array for table-like sections; text payload for AI artifacts

### Separate-tool option

- `azure_sql_blitz_index_detail(target, handle, maxRows?, cursor?)`

### IncludeVerboseResults posture

- **Recommendation: deprecate**
- **Keep case:** backwards compatibility for existing operators
- **Deprecate case:** this tool already exposes many sections; a second “give me everything” switch makes the contract muddy fast

## Generic Tool vs Separate Detail Tools

## Separate tools per parent: the case for it

- Stronger contracts per tool
- Cleaner descriptions in MCP tool listings
- Easier to validate parent-specific inputs without a discriminator matrix
- Better for debugging because the tool name tells you exactly which path failed

## Separate tools per parent: the case against it

- Tool sprawl: five more tools immediately
- Repeated paging/handle semantics across each tool
- Higher maintenance burden for docs, tests, and client guidance

## Single generic detail tool: the case for it

- One drill-down interaction pattern for every diagnostic surface
- Lower MCP surface-area growth
- Easier for clients to learn: “run summary tool, then fetch handle”
- Lets us standardize handle expiry, pagination, and text-vs-table payload rules once

## Single generic detail tool: the case against it

- `parentTool + kind` becomes a small protocol inside the tool contract
- Easier to over-generalize and hide important differences
- We lose some discoverability in tool listings

## Recommendation

Use **one generic drill-down tool**: `azure_sql_fetch_detail_by_handle`.

That only works if the tool stays explicit:

- `parentTool` is required
- `kind` is required
- allowed `kind` values are strictly validated per `parentTool`
- response always echoes `parentTool`, `kind`, and `handle`

That gives us less tool sprawl without collapsing into magic.

## IncludeVerboseResults Recommendation

## Keep: case for it

- Safest migration path
- Useful as a temporary operator/debug switch
- Existing clients do not break immediately

## Deprecate: case for it

- Progressive disclosure becomes the primary contract instead of an optional side path
- Parent responses stay predictably small
- Backend behavior is easier to reason about because there is one obvious expansion path

## Repurpose: case for it

- Could be used to ask for richer previews without fetching all detail

## Repurpose: case against it

- The current name means “include raw verbose output”
- Reusing the same flag for a new meaning is ambiguous and error-prone

## Recommendation

- **Deprecate `IncludeVerboseResults`** on:
  - `azure_sql_health_check`
  - `azure_sql_current_incident`
  - `azure_sql_blitz_cache`
  - `azure_sql_blitz_index`
- Do **not** repurpose the flag.
- During transition, keeping it as a compatibility alias is acceptable, but the contract should clearly state that handle-based drill-down is the preferred path.

## Phase 1 Call

If we implement this later, the simplest non-magical path is:

1. parent tool returns summary fields plus section-level handles
2. client fetches section detail through `azure_sql_fetch_detail_by_handle`
3. `IncludeVerboseResults` is treated as deprecated compatibility, not the future model
