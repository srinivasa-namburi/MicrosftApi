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


resource searchService_65MAWFiAj 'Microsoft.Search/searchServices@2023-11-01' = {
  name: toLower(take('aiSearch${uniqueString(resourceGroup().id)}', 24))
  location: location
  tags: {
    'aspire-resource-name': 'aiSearch'
  }
  sku: {
    name: 'standard'
  }
  properties: {
    replicaCount: 2
    partitionCount: 2
    hostingMode: 'default'
    disableLocalAuth: true
    publicNetworkAccess: 'Disabled'
  }
}

resource peSearchService_65MAWFiAj 'Microsoft.Network/privateEndpoints@2023-11-01' = if (peSubnet != '') {
  name: '${searchService_65MAWFiAj.name}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${searchService_65MAWFiAj.name}-pl'
        properties: {
          privateLinkServiceId: searchService_65MAWFiAj.id
          groupIds: ['searchService']
        }
      }
    ]
  }
}

resource roleAssignment_kdkawv46r 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: searchService_65MAWFiAj
  name: guid(searchService_65MAWFiAj.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
    principalId: principalId
    principalType: principalType
  }
}

resource roleAssignment_V8mKGEf6f 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: searchService_65MAWFiAj
  name: guid(searchService_65MAWFiAj.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0')
    principalId: principalId
    principalType: principalType
  }
}

output connectionString string = 'Endpoint=https://${searchService_65MAWFiAj.name}.search.windows.net'

// Custom
output name string = searchService_65MAWFiAj.name
output pe_ip string = peSearchService_65MAWFiAj.properties.customDnsConfigs[0].ipAddresses[0]
