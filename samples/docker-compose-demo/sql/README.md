# SQL assets for docker-compose demo

- FRK installer: `frk-install.sql`
- Pinned FRK tag: `20240222`
- Source URL: `https://raw.githubusercontent.com/BrentOzarULTD/SQL-Server-First-Responder-Kit/20240222/Install-Core-Blitz-No-Query-Store.sql`

Seed scripts:

- `seed-workload.sql` (demo default): generates query-cache activity for richer `sp_BlitzCache` output.
- `seed-test.sql` (test fixture): minimal deterministic seed for faster integration-style runs.

`frk-install.sql` is intentionally shared between demo and test flows so behavior stays consistent.
