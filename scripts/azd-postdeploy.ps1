param()

$ErrorActionPreference = 'Stop'

function Get-EnvValue {
    param(
        [string]$Name
    )

    $value = [Environment]::GetEnvironmentVariable($Name)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        return $value
    }

    return ''
}

$resourceGroup = Get-EnvValue -Name 'AZURE_RESOURCE_GROUP'
$containerAppName = Get-EnvValue -Name 'SERVICE_BLITZ-BRIDGE_NAME'
if ([string]::IsNullOrWhiteSpace($containerAppName)) {
    $containerAppName = Get-EnvValue -Name 'SERVICE_BLITZ_BRIDGE_NAME'
}

$endpointUrl = ''
if (-not [string]::IsNullOrWhiteSpace($resourceGroup) -and -not [string]::IsNullOrWhiteSpace($containerAppName)) {
    $fqdn = az containerapp show `
        --resource-group $resourceGroup `
        --name $containerAppName `
        --query properties.configuration.ingress.fqdn `
        --output tsv 2>$null
    if (-not [string]::IsNullOrWhiteSpace($fqdn)) {
        $endpointUrl = "https://$fqdn"
    }
}

$managedIdentityName = Get-EnvValue -Name 'MANAGEDIDENTITYNAME'
if ([string]::IsNullOrWhiteSpace($managedIdentityName)) {
    $managedIdentityName = "${env:AZURE_ENV_NAME}-mi"
}

$sqlRoleName = Get-EnvValue -Name 'BLITZBRIDGE_SQL_ROLE_NAME'
if ([string]::IsNullOrWhiteSpace($sqlRoleName)) {
    $sqlRoleName = 'blitz-bridge-executor'
}

$sqlServerFqdn = Get-EnvValue -Name 'BLITZBRIDGE_SQL_SERVER_FQDN'
$sqlDatabaseName = Get-EnvValue -Name 'BLITZBRIDGE_SQL_DATABASE_NAME'
$bearerToken = Get-EnvValue -Name 'BLITZBRIDGE_BEARER_TOKEN'

Write-Host ''
Write-Host 'Blitz Bridge deployment complete.'
if (-not [string]::IsNullOrWhiteSpace($endpointUrl)) {
    Write-Host "Endpoint: $endpointUrl"
}
Write-Host ''
Write-Host 'DBA step required (run against target Azure SQL database):'
Write-Host '--------------------------------------------------------'
Write-Host "USE [$sqlDatabaseName];"
Write-Host "CREATE USER [$managedIdentityName] FROM EXTERNAL PROVIDER;"
Write-Host "EXEC sp_addrolemember N'$sqlRoleName', N'$managedIdentityName';"
Write-Host '--------------------------------------------------------'
Write-Host 'This role is expected to be the same minimum-permission role defined in Batch 5 / D-4.'
Write-Host ''

if (-not [string]::IsNullOrWhiteSpace($endpointUrl) -and -not [string]::IsNullOrWhiteSpace($bearerToken)) {
    Write-Host 'Sample Claude Desktop config:'
    Write-Host '--------------------------------------------------------'
    Write-Host '{'
    Write-Host '  "mcpServers": {'
    Write-Host '    "blitz-bridge-azure": {'
    Write-Host '      "command": "sh",'
    Write-Host '      "args": ['
    Write-Host "        ""-c"","
    Write-Host "        ""curl -sS -X POST -H 'Authorization: Bearer $bearerToken' -H 'Content-Type: application/json' -d @- $endpointUrl/mcp"""
    Write-Host '      ]'
    Write-Host '    }'
    Write-Host '  }'
    Write-Host '}'
    Write-Host '--------------------------------------------------------'
}

if (-not [string]::IsNullOrWhiteSpace($endpointUrl)) {
    Write-Host "Quick health check: curl $endpointUrl/health"
}

if (-not [string]::IsNullOrWhiteSpace($sqlServerFqdn)) {
    Write-Host "Target SQL server: $sqlServerFqdn"
}
