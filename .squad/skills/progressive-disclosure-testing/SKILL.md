# Progressive Disclosure Testing

## When to use

Use this pattern when a summary tool emits opaque section handles and a companion detail tool replays the underlying request to expand a chosen section.

## Pattern

1. Build a deterministic FRK-style fixture dataset for the parent procedure.
2. Call the parent tool and assert only section-level handles are emitted in stable order.
3. Capture one returned handle and call the detail tool with explicit `target`, `parentTool`, `kind`, and `handle`.
4. Assert the detail payload is non-empty and echoes the requested dispatch metadata.
5. Add negative tests for `unknown_parent_tool`, `unknown_kind`, malformed handles, and mismatched dispatch metadata.

## Guardrails

- Do not assert row-level handles for `sp_BlitzCache` unless stable `QueryHash`-based identity is exposed in the contract.
- Do not assert row-level handles for `sp_BlitzFirst`; snapshot data is execution-time scoped.
- Prefer direct tool-boundary tests with a fake `ISqlExecutionService` so the summary and detail paths share the same deterministic fixture.
