targetScope = 'resourceGroup'

@description('')
param location string = resourceGroup().location

@description('')
param sku string = 'Premium'

@description('')
param principalId string

@description('')
param principalType string


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
