@description('The location used for all deployed resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

@description('The type of the principal ID')
param principalType string = 'User'

@description('Deployment model: public or private')
param deploymentModel string = 'public'

var resourceName = 'orleans${uniqueString(resourceGroup().id)}'
var isPrivate = deploymentModel == 'private'

resource storage 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: resourceName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    networkAcls: isPrivate ? {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    } : null
  }
}

// Blob container for Orleans
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' = {
  parent: storage
  name: 'default'
}

resource orleansContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobService
  name: 'blob-orleans'
  properties: {
    publicAccess: 'None'
  }
}

// Table service for clustering and checkpointing
resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2022-09-01' = {
  parent: storage
  name: 'default'
}

resource clusteringTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2022-09-01' = {
  parent: tableService
  name: 'clustering'
}

resource checkpointingTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2022-09-01' = {
  parent: tableService
  name: 'checkpointing'
}

// Role assignment
resource roleAssignmentBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(resourceGroup().id, principalId, 'StorageBlobDataContributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
    principalId: principalId
    principalType: principalType
  }
}

resource roleAssignmentTable 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(resourceGroup().id, principalId, 'StorageTableDataContributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3') // Storage Table Data Contributor
    principalId: principalId
    principalType: principalType
  }
}

var blobEndpoint = storage.properties.primaryEndpoints.blob
var tableEndpoint = storage.properties.primaryEndpoints.table
var key = storage.listKeys().keys[0].value

output resourceId string = storage.id
output resourceName string = storage.name
output blobEndpoint string = blobEndpoint
output tableEndpoint string = tableEndpoint
output connectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${key};EndpointSuffix=${environment().suffixes.storage}'
output blobConnectionString string = 'BlobEndpoint=${blobEndpoint};SharedAccessSignature=${key}'
output tableConnectionString string = 'TableEndpoint=${tableEndpoint};SharedAccessSignature=${key}'
