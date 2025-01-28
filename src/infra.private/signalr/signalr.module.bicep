@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param principalId string

param principalType string

@description('')
param peSubnet string = ''

resource signalr 'Microsoft.SignalRService/signalR@2024-03-01' = {
  name: take('signalr${uniqueString(resourceGroup().id)}', 63)
  location: location
  tags: {
    'aspire-resource-name': 'signalr'
  }
  sku: {
    name: 'Premium_P1'
    tier: 'Premium'
    capacity: 2
  }
  kind: 'SignalR'
  properties: {
    publicNetworkAccess: 'Enabled' // This communicates with frontend client app directly, so needs to be enabled
    features: [
      {
        flag: 'ServiceMode'
        value: 'Default'
      }
    ]
    cors: {
      allowedOrigins: [
        '*'
      ]
    }
  }
}

resource peSignalr 'Microsoft.Network/privateEndpoints@2023-11-01' = if (peSubnet != '') {
  name: '${signalr.name}-pl'
  tags: {
    'aspire-resource-name': 'signalr'
  }
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${signalr.name}-pl'
        properties: {
          privateLinkServiceId: signalr.id
          groupIds: ['signalr']
        }
      }
    ]
  }
}


resource signalr_SignalRAppServer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(signalr.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '420fcaa2-552c-430f-98ca-3264be4806c7'))
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '420fcaa2-552c-430f-98ca-3264be4806c7')
    principalType: principalType
  }
  scope: signalr
}

output hostName string = signalr.properties.hostName

// Custom
output name string = signalr.name
output pe_ip string = peSignalr.properties.customDnsConfigs[0].ipAddresses[0]
