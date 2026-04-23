# Docker Compose Demo (5-minute local sandbox)

This sandbox stands up SQL Server 2022 + Blitz Bridge locally, installs FRK into `DBAtools`, seeds workload for meaningful `sp_BlitzCache` results, and exposes MCP over HTTP.

## Files in this folder

- `docker-compose.yml` — SQL + init + bridge wiring with health-gated startup
- `.env.example` — required local secrets template
- `profiles.json` — demo profile mounted into the bridge container (for visibility/reference)
- `sql/frk-install.sql` — vendored FRK installer (pinned)
- `sql/seed-workload.sql` — demo workload seed for cache-rich diagnostics
- `sql/seed-test.sql` — lightweight reusable seed for integration-test style runs
- `scripts/init-sql.sh` — fail-fast SQL initialization entrypoint
- `scripts/verify-demo.ps1` — startup and MCP smoke validation

## FRK pinning

- **Pinned tag:** `20240222`
- **Source:** `https://raw.githubusercontent.com/BrentOzarULTD/SQL-Server-First-Responder-Kit/20240222/Install-Core-Blitz-No-Query-Store.sql`
- **Vendored as:** `sql/frk-install.sql`

## Quick start

1. Copy environment file:

```powershell
Copy-Item .env.example .env
```

2. Edit `.env` with your local secrets:

```env
SA_PASSWORD=ChangeMe_StrongPassw0rd!
BLITZ_BRIDGE_TOKEN=change-me-demo-token
```

3. Start from a clean machine-like state:

```powershell
docker compose down --volumes --remove-orphans
docker compose up --build
```

Bridge endpoint: `http://localhost:5000/mcp`

## Validate MCP + SQL path

Run:

```powershell
.\scripts\verify-demo.ps1
```

This verifies:
- compose config validity,
- SQL init completion,
- bridge startup,
- MCP `tools/list`, and
- `azure_sql_health_check` against `demo-sql-target` / `DBAtools`.

## Ordering and startup guardrails

- `sql-init` starts only after SQL is healthy and login-ready.
- `sql-init` creates `DBAtools` if missing, runs `frk-install.sql`, then runs the selected seed script.
- `blitzbridge` starts only after SQL is healthy and `sql-init` completed successfully.
- Init script uses `set -euo pipefail` and `sqlcmd -b` so SQL errors fail fast with clear logs.

## Reuse for tests

`frk-install.sql` is the shared install baseline for both demo and test scenarios.

- Demo seed (default compose): `seed-workload.sql` (query-cache heavy; useful for `sp_BlitzCache`)
- Test seed: `seed-test.sql` (minimal and deterministic)

To run init with test seed, set `SEED_SCRIPT=seed-test.sql` for `sql-init`.

## Troubleshooting

- SQL init failed: `docker compose logs sql-init`
- SQL healthcheck not green: `docker compose logs sqlserver`
- Bridge not serving MCP: `docker compose logs blitzbridge`
