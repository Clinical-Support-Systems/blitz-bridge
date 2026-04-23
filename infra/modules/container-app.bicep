@description('Deployment location')
param location string

@description('Container app name')
param containerAppName string

@description('Container Apps environment resource ID')
param containerAppsEnvironmentId string

@description('User-assigned managed identity resource ID')
param managedIdentityResourceId string

@description('Container image reference')
param image string

@description('Container app exposed port')
param targetPort int = 5000

@description('CPU cores for the container')
param cpu string = '0.5'

@description('Memory allocation for the container')
param memory string = '1Gi'

@description('Minimum replica count')
param minReplicas int = 1

@description('Maximum replica count')
param maxReplicas int = 2

@description('Target SQL server FQDN')
param sqlServerFqdn string

@description('Target SQL database name')
param sqlDatabaseName string

@description('Whether SQL server is publicly reachable')
param sqlServerPublic bool

@description('Whether VNet integration is enabled')
param vnetIntegrationEnabled bool

@description('Key Vault URI')
param keyVaultUri string

@description('Name of secret in Key Vault containing bearer token')
param bearerTokenSecretName string = 'blitzbridge-auth-token'

@description('Container registry server (for example ghcr.io or myregistry.azurecr.io)')
param registryServer string

@description('Optional registry username for non-ACR registries')
param registryUsername string = ''

@secure()
@description('Optional registry password or PAT for non-ACR registries')
param registryPassword string = ''

@description('Use managed identity auth for ACR registry pulls')
param useManagedIdentityForAcr bool = false

var registrySecretName = 'registry-password'
var sqlConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;ApplicationIntent=ReadOnly;'
var effectiveMinReplicas = sqlServerPublic || vnetIntegrationEnabled ? minReplicas : 0
var hasBasicRegistryCreds = !empty(registryUsername) && !empty(registryPassword)
var registriesBlock = useManagedIdentityForAcr
  ? [
      {
        server: registryServer
        identity: managedIdentityResourceId
      }
    ]
  : hasBasicRegistryCreds
      ? [
          {
            server: registryServer
            username: registryUsername
            passwordSecretRef: registrySecretName
          }
        ]
      : []

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityResourceId}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: false
      }
      registries: registriesBlock
      secrets: concat(
        hasBasicRegistryCreds
          ? [
              {
                name: registrySecretName
                value: registryPassword
              }
            ]
          : [],
        [
          {
            name: 'auth-token'
            keyVaultUrl: '${keyVaultUri}secrets/${bearerTokenSecretName}'
            identity: managedIdentityResourceId
          }
        ]
      )
    }
    template: {
      containers: [
        {
          image: image
          name: 'blitz-bridge'
          env: [
            {
              name: 'PORT'
              value: string(targetPort)
            }
            {
              name: 'BlitzBridge__DefaultTarget'
              value: 'primary-sql-target'
            }
            {
              name: 'BlitzBridge__Auth__Mode'
              value: 'BearerToken'
            }
            {
              name: 'BLITZBRIDGE_AUTH_TOKENS'
              secretRef: 'auth-token'
            }
            {
              name: 'SqlTargets__Profiles__primary-sql-target__ConnectionString'
              value: sqlConnectionString
            }
            {
              name: 'SqlTargets__Profiles__primary-sql-target__AllowedDatabases__0'
              value: sqlDatabaseName
            }
            {
              name: 'SqlTargets__Profiles__primary-sql-target__AllowedProcedures__0'
              value: 'sp_Blitz'
            }
            {
              name: 'SqlTargets__Profiles__primary-sql-target__AllowedProcedures__1'
              value: 'sp_BlitzCache'
            }
            {
              name: 'SqlTargets__Profiles__primary-sql-target__AllowedProcedures__2'
              value: 'sp_BlitzFirst'
            }
            {
              name: 'SqlTargets__Profiles__primary-sql-target__AllowedProcedures__3'
              value: 'sp_BlitzIndex'
            }
            {
              name: 'SqlTargets__Profiles__primary-sql-target__AllowedProcedures__4'
              value: 'sp_BlitzLock'
            }
            {
              name: 'SqlTargets__Profiles__primary-sql-target__AllowedProcedures__5'
              value: 'sp_BlitzWho'
            }
            {
              name: 'SqlTargets__Profiles__primary-sql-target__Enabled'
              value: 'true'
            }
            {
              name: 'SqlTargets__Profiles__primary-sql-target__CommandTimeoutSeconds'
              value: '60'
            }
            {
              name: 'SqlTargets__Profiles__primary-sql-target__AiMode'
              value: '2'
            }
            {
              name: 'BlitzBridge__Deployment__SqlServerPublic'
              value: toLower(string(sqlServerPublic))
            }
            {
              name: 'BlitzBridge__Deployment__VnetIntegrationEnabled'
              value: toLower(string(vnetIntegrationEnabled))
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: targetPort
                httpHeaders: []
              }
              initialDelaySeconds: 15
              periodSeconds: 30
              timeoutSeconds: 5
              failureThreshold: 6
              successThreshold: 1
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: targetPort
                httpHeaders: []
              }
              initialDelaySeconds: 20
              periodSeconds: 30
              timeoutSeconds: 5
              failureThreshold: 6
              successThreshold: 1
            }
          ]
          resources: {
            cpu: json(cpu)
            memory: memory
          }
        }
      ]
      scale: {
        minReplicas: effectiveMinReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
output url string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output containerAppId string = containerApp.id
