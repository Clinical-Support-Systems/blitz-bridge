#!/usr/bin/env bash
set -euo pipefail

resource_group="${AZURE_RESOURCE_GROUP:-}"
container_app_name="${SERVICE_BLITZ_BRIDGE_NAME:-}"
endpoint_url=""

if [[ -n "$resource_group" && -n "$container_app_name" ]]; then
  fqdn="$(az containerapp show --resource-group "$resource_group" --name "$container_app_name" --query properties.configuration.ingress.fqdn --output tsv 2>/dev/null || true)"
  if [[ -n "$fqdn" ]]; then
    endpoint_url="https://${fqdn}"
  fi
fi

managed_identity_name="${MANAGEDIDENTITYNAME:-${AZURE_ENV_NAME:-blitz-bridge}-mi}"
sql_role_name="${BLITZBRIDGE_SQL_ROLE_NAME:-blitz-bridge-executor}"
sql_server_fqdn="${BLITZBRIDGE_SQL_SERVER_FQDN:-}"
sql_database_name="${BLITZBRIDGE_SQL_DATABASE_NAME:-}"
bearer_token="${BLITZBRIDGE_BEARER_TOKEN:-}"

echo
echo "Blitz Bridge deployment complete."
if [[ -n "$endpoint_url" ]]; then
  echo "Endpoint: $endpoint_url"
fi
echo
echo "DBA step required (run against target Azure SQL database):"
echo "--------------------------------------------------------"
echo "USE [$sql_database_name];"
echo "CREATE USER [$managed_identity_name] FROM EXTERNAL PROVIDER;"
echo "EXEC sp_addrolemember N'$sql_role_name', N'$managed_identity_name';"
echo "--------------------------------------------------------"
echo "This role is expected to be the same minimum-permission role defined in Batch 5 / D-4."
echo

if [[ -n "$endpoint_url" && -n "$bearer_token" ]]; then
  cat <<EOF
Sample Claude Desktop config:
--------------------------------------------------------
{
  "mcpServers": {
    "blitz-bridge-azure": {
      "command": "sh",
      "args": [
        "-c",
        "curl -sS -X POST -H 'Authorization: Bearer $bearer_token' -H 'Content-Type: application/json' -d @- $endpoint_url/mcp"
      ]
    }
  }
}
--------------------------------------------------------
EOF
fi

if [[ -n "$endpoint_url" ]]; then
  echo "Quick health check: curl $endpoint_url/health"
fi

if [[ -n "$sql_server_fqdn" ]]; then
  echo "Target SQL server: $sql_server_fqdn"
fi
