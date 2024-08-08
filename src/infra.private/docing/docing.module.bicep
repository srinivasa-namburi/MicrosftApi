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

resource storageAccount_ZrwAiVlDH 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: toLower(take('docing${uniqueString(resourceGroup().id)}', 24))
  location: location
  tags: {
    'aspire-resource-name': 'docing'
  }
  sku: {
    name: 'Standard_GRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    networkAcls: {
      defaultAction: 'Deny'
    }
    publicNetworkAccess: 'Disabled'
  }
}

resource peStorageAccount_ZrwAiVlDH 'Microsoft.Network/privateEndpoints@2023-11-01' = if (peSubnet != '') {
  name: '${storageAccount_ZrwAiVlDH.name}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${storageAccount_ZrwAiVlDH.name}-pl'
        properties: {
          privateLinkServiceId: storageAccount_ZrwAiVlDH.id
          groupIds: ['blob']
        }
      }
    ]
  }
}

resource blobService_G4CRMfvgh 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' = {
  parent: storageAccount_ZrwAiVlDH
  name: 'default'
  properties: {
  }
}

resource roleAssignment_g1GHeJxdK 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount_ZrwAiVlDH
  name: guid(storageAccount_ZrwAiVlDH.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: principalId
    principalType: principalType
  }
}

resource roleAssignment_h6bEtMn47 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount_ZrwAiVlDH
  name: guid(storageAccount_ZrwAiVlDH.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
    principalId: principalId
    principalType: principalType
  }
}

resource roleAssignment_eB8mbL3FS 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount_ZrwAiVlDH
  name: guid(storageAccount_ZrwAiVlDH.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
    principalId: principalId
    principalType: principalType
  }
}

output blobEndpoint string = storageAccount_ZrwAiVlDH.properties.primaryEndpoints.blob
output queueEndpoint string = storageAccount_ZrwAiVlDH.properties.primaryEndpoints.queue
output tableEndpoint string = storageAccount_ZrwAiVlDH.properties.primaryEndpoints.table

// Custom
output name string = storageAccount_ZrwAiVlDH.name
output pe_ip string = peStorageAccount_ZrwAiVlDH.properties.customDnsConfigs[0].ipAddresses[0]
