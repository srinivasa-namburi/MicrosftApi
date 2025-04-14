@description('The location used for all deployed resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

@description('The type of the principal ID')
param principalType string = 'User'

@description('Deployment model: public or private')
param deploymentModel string = 'public'

var resourceName = 'eventhub-${uniqueString(resourceGroup().id)}'
var isPrivate = deploymentModel == 'private'
var hubName = 'greenlight-hub'
var consumerGroupName = 'greenlight-cg-streams'

resource eventHubNamespace 'Microsoft.EventHub/namespaces@2022-10-01-preview' = {
  name: resourceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
    capacity: 1
  }
  properties: {
    isAutoInflateEnabled: false
    publicNetworkAccess: isPrivate ? 'Disabled' : 'Enabled'
    minimumTlsVersion: '1.2'
  }
}

resource eventHub 'Microsoft.EventHub/namespaces/eventhubs@2022-10-01-preview' = {
  parent: eventHubNamespace
  name: hubName
  properties: {
    messageRetentionInDays: 7
    partitionCount: 4
  }
}

resource consumerGroup 'Microsoft.EventHub/namespaces/eventhubs/consumergroups@2022-10-01-preview' = {
  parent: eventHub
  name: consumerGroupName
}

resource accessPolicy 'Microsoft.EventHub/namespaces/authorizationRules@2022-10-01-preview' = {
  parent: eventHubNamespace
  name: 'RootManageSharedAccessKey'
  properties: {
    rights: [
      'Listen'
      'Manage'
      'Send'
    ]
  }
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(resourceGroup().id, principalId, 'EventHubDataOwner')
  scope: eventHubNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'f526a384-b230-433a-b45c-95f59c4a2dec') // Event Hub Data Owner
    principalId: principalId
    principalType: principalType
  }
}

var eventHubConnectionString = !empty(listKeys(accessPolicy.id, accessPolicy.apiVersion).primaryConnectionString) ? listKeys(accessPolicy.id, accessPolicy.apiVersion).primaryConnectionString : ''

output resourceId string = eventHubNamespace.id
output resourceName string = eventHubNamespace.name
output connectionString string = eventHubConnectionString
output eventHubName string = hubName
output consumerGroupName string = consumerGroupName
output eventHubsEndpoint string = eventHubNamespace.properties.serviceBusEndpoint
