targetScope = 'resourceGroup'

@description('')
param location string = resourceGroup().location

@description('')
param keyVaultName string

@description('')
param peSubnet string = ''

@description('')
param tags object = {}

resource keyVault_IeF8jZvXV 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

resource redisCache_bsDXQBNdq 'Microsoft.Cache/Redis@2020-06-01' = {
  name: toLower(take('redis${uniqueString(resourceGroup().id)}', 24))
  location: location
  tags: {
    'aspire-resource-name': 'redis'
  }
  properties: {
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    sku: {
      name: 'Standard'
      family: 'C'
      capacity: 1
    }
    publicNetworkAccess: 'Disabled'
  }
}

resource peRedisCache_bsDXQBNdq 'Microsoft.Network/privateEndpoints@2023-11-01' = if (peSubnet != '') {
  name: '${redisCache_bsDXQBNdq.name}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${redisCache_bsDXQBNdq.name}-pl'
        properties: {
          privateLinkServiceId: redisCache_bsDXQBNdq.id
          groupIds: ['redisCache']
        }
      }
    ]
  }
}

resource keyVaultSecret_Ddsc3HjrA 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault_IeF8jZvXV
  name: 'connectionString'
  location: location
  properties: {
    value: '${redisCache_bsDXQBNdq.properties.hostName},ssl=true,password=${redisCache_bsDXQBNdq.listKeys(redisCache_bsDXQBNdq.apiVersion).primaryKey}'
  }
}


// Custom
output name string = redisCache_bsDXQBNdq.name
output pe_ip string = peRedisCache_bsDXQBNdq.properties.customDnsConfigs[0].ipAddresses[0]