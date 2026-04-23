param()

$ErrorActionPreference = 'Stop'

function Test-Required {
    param(
        [string]$Name
    )

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Required environment variable '$Name' is not set."
    }
}

function Get-Optional {
    param(
        [string]$Name,
        [string]$DefaultValue = ''
    )

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $DefaultValue
    }

    return $value
}

function Ensure-BoolLike {
    param(
        [string]$Name,
        [string]$Value
    )

    if ($Value -notin @('true', 'false', 'True', 'False')) {
        throw "Environment variable '$Name' must be 'true' or 'false'. Current value: '$Value'."
    }
}

Test-Required -Name 'BLITZBRIDGE_CONTAINER_IMAGE'
Test-Required -Name 'BLITZBRIDGE_CONTAINER_REGISTRY_SERVER'
Test-Required -Name 'BLITZBRIDGE_SQL_SERVER_FQDN'
Test-Required -Name 'BLITZBRIDGE_SQL_DATABASE_NAME'
Test-Required -Name 'BLITZBRIDGE_SQL_SERVER_PUBLIC'
Test-Required -Name 'BLITZBRIDGE_VNET_INTEGRATION_ENABLED'

$sqlServerPublic = Get-Optional -Name 'BLITZBRIDGE_SQL_SERVER_PUBLIC'
$vnetIntegrationEnabled = Get-Optional -Name 'BLITZBRIDGE_VNET_INTEGRATION_ENABLED'
$registryManagedIdentity = Get-Optional -Name 'BLITZBRIDGE_CONTAINER_REGISTRY_USE_MANAGED_IDENTITY' -DefaultValue 'false'

Ensure-BoolLike -Name 'BLITZBRIDGE_SQL_SERVER_PUBLIC' -Value $sqlServerPublic
Ensure-BoolLike -Name 'BLITZBRIDGE_VNET_INTEGRATION_ENABLED' -Value $vnetIntegrationEnabled
Ensure-BoolLike -Name 'BLITZBRIDGE_CONTAINER_REGISTRY_USE_MANAGED_IDENTITY' -Value $registryManagedIdentity

if ($vnetIntegrationEnabled -eq 'true') {
    Test-Required -Name 'BLITZBRIDGE_INFRASTRUCTURE_SUBNET_RESOURCE_ID'
}

if ($sqlServerPublic -eq 'false' -and $vnetIntegrationEnabled -ne 'true') {
    throw 'BLITZBRIDGE_SQL_SERVER_PUBLIC is false, so BLITZBRIDGE_VNET_INTEGRATION_ENABLED must be true for private connectivity.'
}

$tokenValue = Get-Optional -Name 'BLITZBRIDGE_BEARER_TOKEN'
if ([string]::IsNullOrWhiteSpace($tokenValue)) {
    $generatedToken = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
    & azd env set BLITZBRIDGE_BEARER_TOKEN $generatedToken | Out-Null
    Write-Host 'Generated BLITZBRIDGE_BEARER_TOKEN and saved it to the azd environment.'
}

if ([string]::IsNullOrWhiteSpace((Get-Optional -Name 'BLITZBRIDGE_SQL_ROLE_NAME'))) {
    & azd env set BLITZBRIDGE_SQL_ROLE_NAME 'blitz-bridge-executor' | Out-Null
}

if ([string]::IsNullOrWhiteSpace((Get-Optional -Name 'BLITZBRIDGE_PORT'))) {
    & azd env set BLITZBRIDGE_PORT '5000' | Out-Null
}

if ([string]::IsNullOrWhiteSpace((Get-Optional -Name 'BLITZBRIDGE_CONTAINER_REGISTRY_USE_MANAGED_IDENTITY'))) {
    & azd env set BLITZBRIDGE_CONTAINER_REGISTRY_USE_MANAGED_IDENTITY 'false' | Out-Null
}
