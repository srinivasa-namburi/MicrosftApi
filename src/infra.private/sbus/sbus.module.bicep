targetScope = 'resourceGroup'

@description('')
param location string = resourceGroup().location

@description('')
param sku string = 'Premium'

@description('')
param principalId string

@description('')
param principalType string

@description('')
param peSubnet string = ''

@description('')
param tags object = {}


resource serviceBusNamespace_r4wPCvQVC 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: toLower(take('sbus${uniqueString(resourceGroup().id)}', 24))
  location: location
  tags: {
    'aspire-resource-name': 'sbus'
  }
  sku: {
    name: sku
    tier: 'Premium'
    capacity: 1
  }
  properties: {
      publicNetworkAccess: 'Disabled'
  }
}

resource peServiceBusNamespace_r4wPCvQVC 'Microsoft.Network/privateEndpoints@2023-11-01' = if (peSubnet != '') {
  name: '${serviceBusNamespace_r4wPCvQVC.name}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${serviceBusNamespace_r4wPCvQVC.name}-pl'
        properties: {
          privateLinkServiceId: serviceBusNamespace_r4wPCvQVC.id
          groupIds: ['namespace']
        }
      }
    ]
  }
}

resource roleAssignment_Cp26g1LUw 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: serviceBusNamespace_r4wPCvQVC
  name: guid(serviceBusNamespace_r4wPCvQVC.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '090c5cfd-751d-490a-894a-3ce6f1109419'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '090c5cfd-751d-490a-894a-3ce6f1109419')
    principalId: principalId
    principalType: principalType
  }
}

output serviceBusEndpoint string = serviceBusNamespace_r4wPCvQVC.properties.serviceBusEndpoint

// Custom
output name string = serviceBusNamespace_r4wPCvQVC.name
output pe_ip string = peServiceBusNamespace_r4wPCvQVC.properties.customDnsConfigs[0].ipAddresses[0]