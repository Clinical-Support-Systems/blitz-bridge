# Progressive Disclosure Response Shapes

## Purpose

Use this pattern when an MCP or API response is getting too wide, too verbose, or too expensive to return inline every time.

## Pattern

1. Keep the parent response compact and operationally useful on its own.
2. Return opaque handles for major result sections instead of dumping raw result sets inline.
3. Prefer section-level handles first; row-level handles can wait until there is a clear need.
4. Use one shared detail-fetch contract only if it stays explicit with required discriminators.
5. Keep the implementation stateless: re-run the allowlisted backend read path instead of caching prior result sets.

## Recommended Contract

- Parent response:
  - summary scalars/counts
  - `summary`
  - `handles`
  - existing compact arrays during additive rollout
  - `notes`
- Handle fields:
  - `handle`
  - `parentTool`
  - `kind`
  - `title`
  - `preview`
  - optional `severity`, `itemCount`, `totalCount`
- Detail fetch inputs:
  - `target`
  - `parentTool`
  - `kind`
  - `handle`
  - optional `maxRows`
- Detail fetch outputs:
  - `target`
  - `parentTool`
  - `kind`
  - `handle`
  - `scope`
  - either `items` or `contentType` + `content`
  - `notes`

## Guardrails

- Do not make handles parseable by clients.
- Do not repurpose old verbose flags to mean something new.
- Echo `parentTool`, `kind`, and `handle` in the detail response.
- Validate allowed `kind` values per parent tool so the generic detail endpoint does not become a grab bag.
- Use a versioned handle format so future shape changes fail cleanly.
- Measure response size on every tool call; a chars/4 estimate is good enough for trend telemetry.

## Error Contract

- Invalid request payloads should fail as `invalid_request`.
- Unknown parent tools should fail as `unknown_parent_tool`.
- Unknown section kinds should fail as `unknown_kind`.
- Malformed or mismatched handles should fail as `malformed_handle`.
- Access drift between summary and detail calls should fail as `access_denied`.
- Missing refreshed sections should fail as `section_not_found`.

## When Not To Use It

- When the base response is already small and stable
- When clients always need the full payload anyway
- When the detail surface is genuinely different enough that separate tools are clearer
