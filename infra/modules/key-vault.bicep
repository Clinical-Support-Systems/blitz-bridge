@description('Deployment location')
param location string

@description('Key Vault name')
param keyVaultName string

@description('Tenant ID for Key Vault')
param tenantId string

@description('Managed identity principal ID granted secrets access')
param managedIdentityPrincipalId string

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: false
    enabledForDeployment: false
    enabledForTemplateDeployment: false
    enabledForDiskEncryption: false
    softDeleteRetentionInDays: 90
    accessPolicies: [
      {
        tenantId: tenantId
        objectId: managedIdentityPrincipalId
        permissions: {
          secrets: [
            'Get'
            'List'
          ]
        }
      }
    ]
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
    publicNetworkAccess: 'Enabled'
  }
}

output vaultUri string = keyVault.properties.vaultUri
output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
