# Security Policy

## Reporting a vulnerability

If you discover a security issue, please report it privately to the maintainers through your normal internal disclosure channel. Include:

- affected version/commit
- reproduction steps
- impact assessment
- suggested mitigation (if known)

Please do not open public issues for undisclosed vulnerabilities.

## Security guarantees in Blitz Bridge

Blitz Bridge is designed as a **read-only diagnostic bridge** and enforces:

- profile-based target scoping (no ad-hoc connection-string discovery)
- procedure allowlisting to FRK diagnostic procedures
- read-only SQL connection intent (`ApplicationIntent=ReadOnly`)
- optional HTTP bearer-token gate for hosted `/mcp`

## Authentication model

- **Stdio mode**: local process transport, no HTTP auth layer.
- **HTTP mode**: optional bearer-token allowlist via `BlitzBridge:Auth` / `BLITZBRIDGE_AUTH_TOKENS`.
- In Azure deployment, token material is intended to be held in Key Vault and injected at runtime.

## What Blitz Bridge does NOT protect against

Blitz Bridge does not replace your broader platform security model. In particular:

- If an agent/client is compromised, it can still request allowed diagnostics and exfiltrate returned data (including query-plan-derived details).
- If overly broad SQL permissions are granted to the service principal/login, Blitz Bridge cannot reduce them at runtime.
- If bearer tokens or connection metadata are leaked outside your trust boundary, that is an operator credential management issue.
- Blitz Bridge does not provide tenant isolation or data-loss-prevention controls by itself.

## Operational recommendations

- Use least-privilege role grants from `docs/sql/blitz-bridge-role.sql`.
- Keep bearer tokens in a secret store and rotate regularly.
- Restrict network exposure for hosted deployments.
- Audit diagnostic usage through your log pipeline (for Azure: Log Analytics + diagnostic settings).
