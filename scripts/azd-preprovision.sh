#!/usr/bin/env bash
set -euo pipefail

require_var() {
  local name="$1"
  if [[ -z "${!name:-}" ]]; then
    echo "Required environment variable '$name' is not set." >&2
    exit 1
  fi
}

ensure_bool_like() {
  local name="$1"
  local value="$2"
  if [[ "$value" != "true" && "$value" != "false" ]]; then
    echo "Environment variable '$name' must be 'true' or 'false'. Current value: '$value'." >&2
    exit 1
  fi
}

require_var "BLITZBRIDGE_CONTAINER_IMAGE"
require_var "BLITZBRIDGE_CONTAINER_REGISTRY_SERVER"
require_var "BLITZBRIDGE_SQL_SERVER_FQDN"
require_var "BLITZBRIDGE_SQL_DATABASE_NAME"
require_var "BLITZBRIDGE_SQL_SERVER_PUBLIC"
require_var "BLITZBRIDGE_VNET_INTEGRATION_ENABLED"

sql_server_public="${BLITZBRIDGE_SQL_SERVER_PUBLIC}"
vnet_integration_enabled="${BLITZBRIDGE_VNET_INTEGRATION_ENABLED}"
registry_managed_identity="${BLITZBRIDGE_CONTAINER_REGISTRY_USE_MANAGED_IDENTITY:-false}"

ensure_bool_like "BLITZBRIDGE_SQL_SERVER_PUBLIC" "$sql_server_public"
ensure_bool_like "BLITZBRIDGE_VNET_INTEGRATION_ENABLED" "$vnet_integration_enabled"
ensure_bool_like "BLITZBRIDGE_CONTAINER_REGISTRY_USE_MANAGED_IDENTITY" "$registry_managed_identity"

if [[ "$vnet_integration_enabled" == "true" && -z "${BLITZBRIDGE_INFRASTRUCTURE_SUBNET_RESOURCE_ID:-}" ]]; then
  echo "BLITZBRIDGE_INFRASTRUCTURE_SUBNET_RESOURCE_ID is required when BLITZBRIDGE_VNET_INTEGRATION_ENABLED=true." >&2
  exit 1
fi

if [[ "$sql_server_public" == "false" && "$vnet_integration_enabled" != "true" ]]; then
  echo "BLITZBRIDGE_SQL_SERVER_PUBLIC=false requires BLITZBRIDGE_VNET_INTEGRATION_ENABLED=true for private connectivity." >&2
  exit 1
fi

if [[ -z "${BLITZBRIDGE_BEARER_TOKEN:-}" ]]; then
  generated_token="$(openssl rand -base64 48)"
  azd env set BLITZBRIDGE_BEARER_TOKEN "$generated_token" >/dev/null
  echo "Generated BLITZBRIDGE_BEARER_TOKEN and saved it to the azd environment."
fi

if [[ -z "${BLITZBRIDGE_SQL_ROLE_NAME:-}" ]]; then
  azd env set BLITZBRIDGE_SQL_ROLE_NAME "blitz-bridge-executor" >/dev/null
fi

if [[ -z "${BLITZBRIDGE_PORT:-}" ]]; then
  azd env set BLITZBRIDGE_PORT "5000" >/dev/null
fi

if [[ -z "${BLITZBRIDGE_CONTAINER_REGISTRY_USE_MANAGED_IDENTITY:-}" ]]; then
  azd env set BLITZBRIDGE_CONTAINER_REGISTRY_USE_MANAGED_IDENTITY "false" >/dev/null
fi
