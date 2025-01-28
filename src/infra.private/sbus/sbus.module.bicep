@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param sku string = 'Premium'

param principalId string

param principalType string

@description('')
param peSubnet string = ''

resource sbus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: take('sbus${uniqueString(resourceGroup().id)}', 50)
  location: location
  properties: {
    publicNetworkAccess: 'Disabled'
  }
  sku: {
    name: sku
    tier: 'Premium'
    capacity: 1
  }
  tags: {
    'aspire-resource-name': 'sbus'
  }
  
}

resource peSbus 'Microsoft.Network/privateEndpoints@2023-11-01' = if (peSubnet != '') {
  name: '${sbus.name}-pl'
  tags: {
    'aspire-resource-name': 'sbus'
  }
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${sbus.name}-pl'
        properties: {
          privateLinkServiceId: sbus.id
          groupIds: ['namespace']
        }
      }
    ]
  }
}

resource sbus_AzureServiceBusDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sbus.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '090c5cfd-751d-490a-894a-3ce6f1109419'))
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '090c5cfd-751d-490a-894a-3ce6f1109419')
    principalType: principalType
  }
  scope: sbus
}

output serviceBusEndpoint string = sbus.properties.serviceBusEndpoint

// Custom
output name string = sbus.name
output pe_ip string = peSbus.properties.customDnsConfigs[0].ipAddresses[0]
