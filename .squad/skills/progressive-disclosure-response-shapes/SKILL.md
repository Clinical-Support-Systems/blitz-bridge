# Progressive Disclosure Response Shapes

## Purpose

Use this pattern when an MCP or API response is getting too wide, too verbose, or too expensive to return inline every time.

## Pattern

1. Keep the parent response compact and operationally useful on its own.
2. Return opaque handles for major result sections instead of dumping raw result sets inline.
3. Prefer section-level handles first; row-level handles can wait until there is a clear need.
4. Use one shared detail-fetch contract only if it stays explicit with required discriminators.

## Recommended Contract

- Parent response:
  - summary fields
  - `summary`
  - `handles`
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
  - optional `maxRows`, `cursor`

## Guardrails

- Do not make handles parseable by clients.
- Do not repurpose old verbose flags to mean something new.
- Echo `parentTool`, `kind`, and `handle` in the detail response.
- Validate allowed `kind` values per parent tool so the generic detail endpoint does not become a grab bag.

## When Not To Use It

- When the base response is already small and stable
- When clients always need the full payload anyway
- When the detail surface is genuinely different enough that separate tools are clearer
