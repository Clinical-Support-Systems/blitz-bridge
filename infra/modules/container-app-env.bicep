@description('Deployment location')
param location string

@description('Container Apps environment name')
param environmentName string

@description('Log Analytics customer ID')
param logAnalyticsCustomerId string

@secure()
@description('Log Analytics shared key')
param logAnalyticsSharedKey string

@description('Enable VNet integration')
param vnetIntegrationEnabled bool

@description('Optional infrastructure subnet resource ID for ACA environment')
param infrastructureSubnetResourceId string = ''

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: union(
    {
      appLogsConfiguration: {
        destination: 'log-analytics'
        logAnalyticsConfiguration: {
          customerId: logAnalyticsCustomerId
          sharedKey: logAnalyticsSharedKey
        }
      }
    },
    vnetIntegrationEnabled
      ? {
          vnetConfiguration: {
            infrastructureSubnetId: infrastructureSubnetResourceId
            internal: false
          }
        }
      : {}
  )
}

output environmentId string = managedEnvironment.id
output environmentName string = managedEnvironment.name
