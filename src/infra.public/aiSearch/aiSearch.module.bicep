@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param principalId string

param principalType string

resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = {
  name: take('aisearch${uniqueString(resourceGroup().id)}', 60)
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
    disableLocalAuth: false
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http403'
      }
    }
  }
}

resource aiSearch_SearchIndexDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7'))
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
    principalType: principalType
  }
  scope: aiSearch
}

resource aiSearch_SearchServiceContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0'))
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0')
    principalType: principalType
  }
  scope: aiSearch
}

output connectionString string = 'Endpoint=https://${aiSearch.name}.search.windows.net'
