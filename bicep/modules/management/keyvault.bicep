param uniqueName string
param location string
param tags object = {}

resource keyvault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: 'kv-${uniqueName}'
  location: location
  tags: tags
  properties: {
    createMode: 'default' // This is required to recover a soft-deleted key vault
    enabledForTemplateDeployment: true
    enableRbacAuthorization: true
    enableSoftDelete: true
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
      ipRules: [
      ]
    }
    // publicNetworkAccess: 'disabled' // Only traffic from private endpoint
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    tenantId: subscription().tenantId
  }
}
