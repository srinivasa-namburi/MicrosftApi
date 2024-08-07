targetScope = 'resourceGroup'

@description('')
param location string = resourceGroup().location

@description('')
param principalId string

@description('')
param principalType string

@description('')
param peSubnet string = ''

@description('')
param tags object = {}

resource signalRService_iD3Yrl49T 'Microsoft.SignalRService/signalR@2022-02-01' = {
  name: toLower(take('signalr${uniqueString(resourceGroup().id)}', 24))
  location: location
  tags: {
    'aspire-resource-name': 'signalr'
  }
  sku: {
    name: 'Premium_P1'
    tier: 'Premium'
    capacity: 3
  }
  kind: 'SignalR'
  properties: {
    publicNetworkAccess: 'Disabled'
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

resource peSignalRService_iD3Yrl49T 'Microsoft.Network/privateEndpoints@2023-11-01' = if (peSubnet != '') {
  name: '${signalRService_iD3Yrl49T.name}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${signalRService_iD3Yrl49T.name}-pl'
        properties: {
          privateLinkServiceId: signalRService_iD3Yrl49T.id
          groupIds: ['signalr']
        }
      }
    ]
  }
}

resource roleAssignment_35voRFfVj 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: signalRService_iD3Yrl49T
  name: guid(signalRService_iD3Yrl49T.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '420fcaa2-552c-430f-98ca-3264be4806c7'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '420fcaa2-552c-430f-98ca-3264be4806c7')
    principalId: principalId
    principalType: principalType
  }
}

output hostName string = signalRService_iD3Yrl49T.properties.hostName

// Custom
output name string = signalRService_iD3Yrl49T.name
output pe_ip string = peSignalRService_iD3Yrl49T.properties.customDnsConfigs[0].ipAddresses[0]