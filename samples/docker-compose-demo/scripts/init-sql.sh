#!/bin/bash
set -euo pipefail

SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
if [ ! -x "${SQLCMD}" ]; then
  SQLCMD="/opt/mssql-tools/bin/sqlcmd"
fi

if [ ! -x "${SQLCMD}" ]; then
  echo "[init] sqlcmd not found in mssql-tools image." >&2
  exit 1
fi

if [ -z "${SA_PASSWORD:-}" ]; then
  echo "[init] SA_PASSWORD environment variable is required." >&2
  exit 1
fi

SEED_SCRIPT_NAME="${SEED_SCRIPT:-seed-workload.sql}"
SEED_SCRIPT_PATH="/workspace/sql/${SEED_SCRIPT_NAME}"
if [ ! -f "${SEED_SCRIPT_PATH}" ]; then
  echo "[init] Seed script not found: ${SEED_SCRIPT_PATH}" >&2
  exit 1
fi

wait_for_sql() {
  for attempt in $(seq 1 60); do
    if "${SQLCMD}" -S sqlserver -U sa -P "${SA_PASSWORD}" -C -Q "SELECT 1" >/dev/null 2>&1; then
      echo "[init] SQL Server is reachable."
      return 0
    fi

    echo "[init] Waiting for SQL Server (${attempt}/60)..."
    sleep 2
  done

  echo "[init] SQL Server did not become reachable in time." >&2
  return 1
}

run_sql_file() {
  local file_path="$1"
  local database_name="$2"
  echo "[init] Running ${file_path} on ${database_name}..."
  "${SQLCMD}" -S sqlserver -U sa -P "${SA_PASSWORD}" -C -d "${database_name}" -b -i "${file_path}"
}

wait_for_sql

echo "[init] Ensuring DBAtools database exists..."
"${SQLCMD}" -S sqlserver -U sa -P "${SA_PASSWORD}" -C -b -Q "IF DB_ID(N'DBAtools') IS NULL CREATE DATABASE [DBAtools];"

run_sql_file /workspace/sql/frk-install.sql DBAtools
run_sql_file "${SEED_SCRIPT_PATH}" DBAtools

echo "[init] Marking init completion..."
"${SQLCMD}" -S sqlserver -U sa -P "${SA_PASSWORD}" -C -b -d DBAtools -Q "IF OBJECT_ID(N'dbo.BlitzBridgeInitComplete', N'U') IS NULL CREATE TABLE dbo.BlitzBridgeInitComplete (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, CompletedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(), SeedScript sysname NOT NULL); INSERT dbo.BlitzBridgeInitComplete (SeedScript) VALUES ('${SEED_SCRIPT_NAME}');"

echo "[init] SQL initialization complete."
