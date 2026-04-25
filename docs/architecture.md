# Blitz Bridge architecture overview

Blitz Bridge sits between MCP clients and Azure SQL as a constrained diagnostic execution layer:

1. MCP client calls a named tool (`azure_sql_*`).
2. Blitz Bridge resolves a preconfigured target profile.
3. Procedure/database allowlists are enforced before execution.
4. FRK stored procedures execute with read-only connection intent.
5. Structured results are returned to the client.

## Core design constraints

- No arbitrary SQL execution surface.
- No write operations or schema mutations through Blitz Bridge.
- Profiles are configured server-side; connection strings are not exposed to clients.
- Procedure access is limited to an allowlisted FRK subset.

## Auth and deployment model

- Stdio mode is local process transport and does not use HTTP auth.
- Hosted HTTP mode supports bearer token allowlists.
- Azure deployment (`azd`) uses managed identity + Key Vault + Container Apps + Log Analytics.

## Operational boundaries

Blitz Bridge constrains execution shape, but does not replace credential governance. Operators own token lifecycle, SQL grants, and network exposure decisions.
