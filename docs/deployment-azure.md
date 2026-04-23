# Deploying Blitz Bridge to Azure with `azd`

This guide deploys Blitz Bridge to Azure Container Apps with:

- User-assigned managed identity for Azure SQL authentication
- Key Vault secret storage for the HTTP bearer token
- Log Analytics + diagnostic settings for queryable audit and platform logs
- Parameterized image pull from either ACR or `ghcr.io`
- External (existing) Azure SQL server/database (not provisioned by this deployment)

## Prerequisites

- Azure Developer CLI (`azd`)
- Azure CLI (`az`) logged in to your target subscription
- An existing Azure SQL server and database with FRK installed
- A built/published container image for Blitz Bridge

## What `azd up` creates

`infra/main.bicep` provisions:

- `Microsoft.App/managedEnvironments` (Container Apps environment)
- `Microsoft.ManagedIdentity/userAssignedIdentities` (app identity)
- `Microsoft.KeyVault/vaults` + bearer token secret
- `Microsoft.App/containerApps` (Blitz Bridge service)
- `Microsoft.OperationalInsights/workspaces` (Log Analytics workspace)
- Diagnostic settings routing Container Apps environment/app and Key Vault logs to Log Analytics
- Optional ACR pull role assignment (`AcrPull`) when managed-identity registry auth is enabled

## Required environment parameters

Set these in your azd environment (`azd env set ...`) before `azd up`:

- `BLITZBRIDGE_CONTAINER_IMAGE` (for example `ghcr.io/org/blitz-bridge:1.0.0`)
- `BLITZBRIDGE_CONTAINER_REGISTRY_SERVER` (for example `ghcr.io` or `myacr.azurecr.io`)
- `BLITZBRIDGE_SQL_SERVER_FQDN` (existing SQL server FQDN)
- `BLITZBRIDGE_SQL_DATABASE_NAME` (existing database name)
- `BLITZBRIDGE_SQL_SERVER_PUBLIC` (`true` or `false`)
- `BLITZBRIDGE_VNET_INTEGRATION_ENABLED` (`true` or `false`)

Additional conditional parameters:

- `BLITZBRIDGE_INFRASTRUCTURE_SUBNET_RESOURCE_ID` (required when VNet integration is `true`)
- `BLITZBRIDGE_CONTAINER_REGISTRY_USE_MANAGED_IDENTITY` (`true` for ACR + MI pull, else `false`)
- `BLITZBRIDGE_CONTAINER_REGISTRY_ACR_NAME` (required if registry MI auth is enabled)
- `BLITZBRIDGE_CONTAINER_REGISTRY_USERNAME` and `BLITZBRIDGE_CONTAINER_REGISTRY_PASSWORD` (for non-ACR private registries)
- `BLITZBRIDGE_SQL_ROLE_NAME` (defaults to `blitz-bridge-executor`)
- `BLITZBRIDGE_BEARER_TOKEN` (optional: auto-generated if omitted by pre-provision hook)

## Run deployment

```bash
azd env new
azd env set AZURE_LOCATION eastus
azd env set BLITZBRIDGE_CONTAINER_IMAGE ghcr.io/<org>/<image>:<tag>
azd env set BLITZBRIDGE_CONTAINER_REGISTRY_SERVER ghcr.io
azd env set BLITZBRIDGE_SQL_SERVER_FQDN <server>.database.windows.net
azd env set BLITZBRIDGE_SQL_DATABASE_NAME <database>
azd env set BLITZBRIDGE_SQL_SERVER_PUBLIC true
azd env set BLITZBRIDGE_VNET_INTEGRATION_ENABLED false
azd up
```

`scripts/azd-preprovision.ps1` validates required inputs and generates a bearer token when missing.

`scripts/azd-postdeploy.ps1` prints:

- deployed endpoint URL
- exact DBA-run T-SQL grant statement
- sample Claude Desktop config using bearer token auth

## DBA SQL grant step (manual, privileged)

After deployment, run the printed SQL against the target database:

```sql
CREATE USER [<managed-identity-name>] FROM EXTERNAL PROVIDER;
EXEC sp_addrolemember N'<blitz-bridge-role>', N'<managed-identity-name>';
```

Important:

- This is intentionally **not auto-executed** by `azd`.
- `<blitz-bridge-role>` should be the same minimum-permission role defined in Batch 5 / D-4 (do not create a separate duplicate role for deployment).

## Client wiring with bearer token

Use the post-deploy output sample, or equivalent:

- Endpoint: `https://<container-app-fqdn>/mcp`
- Header: `Authorization: Bearer <token>`

The token value is stored in Key Vault and referenced by Container Apps secret integration.

## Health behavior

`/health` returns:

- `200` with **Healthy** when SQL profiles are reachable
- `200` with **Degraded** when SQL is temporarily unreachable
- `503` only for unhealthy app-level failures

This keeps startup resilient for managed-identity and private-network timing while still exposing connectivity state.

## Cleanup

```bash
azd down
```

This removes the resource group and all resources created by this deployment.
