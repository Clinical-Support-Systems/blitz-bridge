targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Name prefix for created resources')
@minLength(2)
param resourceNamePrefix string

@description('Container image reference for blitz-bridge')
param containerImage string

@description('Container registry server (for example ghcr.io or myregistry.azurecr.io)')
param containerRegistryServer string

@description('Use managed identity for ACR image pull')
param containerRegistryUseManagedIdentity bool = false

@description('Optional ACR name (same resource group) when managed identity pull is enabled')
param containerRegistryAcrName string = ''

@description('Optional registry username for non-ACR registries (for example ghcr.io)')
param containerRegistryUsername string = ''

@secure()
@description('Optional registry password or PAT for non-ACR registries')
param containerRegistryPassword string = ''

@description('Enable VNet integration for Container Apps environment')
param vnetIntegrationEnabled bool

@description('Infrastructure subnet resource ID for Container Apps environment when VNet integration is enabled')
param infrastructureSubnetResourceId string = ''

@description('Target Azure SQL server FQDN (existing)')
param sqlServerFqdn string

@description('Target Azure SQL database name (existing)')
param sqlDatabaseName string

@description('Whether target SQL server is publicly reachable')
param sqlServerPublic bool

@secure()
@description('Bearer token value for HTTP auth')
param bearerToken string

@description('SQL database role name defined by Batch 5 / D-4 minimum-permission spec')
param sqlRoleName string = 'blitz-bridge-executor'

@description('Container app target port')
param targetPort int = 5000

@description('Container app CPU')
param containerCpu string = '0.5'

@description('Container app memory')
param containerMemory string = '1Gi'

@description('Bearer token secret name in Key Vault')
param bearerTokenSecretName string = 'blitzbridge-auth-token'

var containerAppEnvironmentName = '${resourceNamePrefix}-cae'
var containerAppName = '${resourceNamePrefix}-app'
var managedIdentityName = '${resourceNamePrefix}-mi'
var keyVaultName = toLower(replace('${resourceNamePrefix}-kv', '_', ''))
var logAnalyticsName = '${resourceNamePrefix}-law'
var enableAcrPullAssignment = containerRegistryUseManagedIdentity && !empty(containerRegistryAcrName)

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'logAnalytics'
  params: {
    location: location
    workspaceName: logAnalyticsName
  }
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsName
}

module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'managedIdentity'
  params: {
    location: location
    identityName: managedIdentityName
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    keyVaultName: keyVaultName
    tenantId: tenant().tenantId
    managedIdentityPrincipalId: managedIdentity.outputs.principalId
  }
}

resource keyVaultForSecret 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource bearerTokenSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: bearerTokenSecretName
  parent: keyVaultForSecret
  properties: {
    value: bearerToken
  }
}

module containerEnvironment 'modules/container-app-env.bicep' = {
  name: 'containerEnvironment'
  params: {
    location: location
    environmentName: containerAppEnvironmentName
    logAnalyticsCustomerId: logAnalytics.outputs.customerId
    logAnalyticsSharedKey: listKeys(logAnalyticsWorkspace.id, '2023-09-01').primarySharedKey
    vnetIntegrationEnabled: vnetIntegrationEnabled
    infrastructureSubnetResourceId: infrastructureSubnetResourceId
  }
}

module containerApp 'modules/container-app.bicep' = {
  name: 'containerApp'
  params: {
    location: location
    containerAppName: containerAppName
    containerAppsEnvironmentId: containerEnvironment.outputs.environmentId
    managedIdentityResourceId: managedIdentity.outputs.identityId
    image: containerImage
    targetPort: targetPort
    cpu: containerCpu
    memory: containerMemory
    minReplicas: 1
    maxReplicas: 2
    sqlServerFqdn: sqlServerFqdn
    sqlDatabaseName: sqlDatabaseName
    sqlServerPublic: sqlServerPublic
    vnetIntegrationEnabled: vnetIntegrationEnabled
    keyVaultUri: keyVault.outputs.vaultUri
    bearerTokenSecretName: bearerTokenSecretName
    registryServer: containerRegistryServer
    registryUsername: containerRegistryUsername
    registryPassword: containerRegistryPassword
    useManagedIdentityForAcr: containerRegistryUseManagedIdentity
  }
  dependsOn: [
    bearerTokenSecret
  ]
}

resource managedEnvironmentResource 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppEnvironmentName
}

resource containerAppResource 'Microsoft.App/containerApps@2024-03-01' existing = {
  name: containerAppName
}

resource keyVaultResource 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource managedEnvironmentDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${containerAppEnvironmentName}-diag'
  scope: managedEnvironmentResource
  properties: {
    workspaceId: logAnalytics.outputs.workspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource containerAppDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${containerAppName}-diag'
  scope: containerAppResource
  properties: {
    workspaceId: logAnalytics.outputs.workspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${keyVaultName}-diag'
  scope: keyVaultResource
  properties: {
    workspaceId: logAnalytics.outputs.workspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = if (enableAcrPullAssignment) {
  name: containerRegistryAcrName
}

resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (enableAcrPullAssignment) {
  name: guid(acr.id, managedIdentityName, 'AcrPull')
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
    principalId: managedIdentity.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

output containerAppName string = containerAppName
output containerAppUrl string = containerApp.outputs.url
output managedIdentityName string = managedIdentity.outputs.name
output managedIdentityPrincipalId string = managedIdentity.outputs.principalId
output keyVaultName string = keyVaultName
output keyVaultUri string = keyVault.outputs.vaultUri
output bearerTokenSecretName string = bearerTokenSecretName
output sqlServerFqdnOut string = sqlServerFqdn
output sqlDatabaseNameOut string = sqlDatabaseName
output sqlRoleNameOut string = sqlRoleName
output logAnalyticsWorkspaceName string = logAnalytics.outputs.workspaceName
output sqlServerPublicOut bool = sqlServerPublic
output vnetIntegrationEnabledOut bool = vnetIntegrationEnabled
