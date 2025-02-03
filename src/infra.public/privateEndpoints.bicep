@description('The location for the private endpoints to be deployed.')
param location string = resourceGroup().location

@description('Resource ID of the subnet to use for private endpoints.')
param peSubnet string

// For each resource, accept both the resource ID and resource name.
@description('Resource ID of the Azure Search service.')
param aiSearchId string
@description('Resource name of the Azure Search service.')
param aiSearchName string

@description('Resource ID of the storage account (docing).')
param docingId string
@description('Resource name of the storage account (docing).')
param docingName string

@description('Resource ID of the Redis cache.')
param redisId string
@description('Resource name of the Redis cache.')
param redisName string

@description('Resource ID of the Service Bus namespace.')
param sbusId string
@description('Resource name of the Service Bus namespace.')
param sbusName string

@description('Resource ID of the SignalR service.')
param signalrId string
@description('Resource name of the SignalR service.')
param signalrName string

@description('Resource ID of the SQL server.')
param sqldocgenId string
@description('Resource name of the SQL server.')
param sqldocgenName string

// Private endpoint for Azure Search
resource peAiSearch 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${aiSearchName}-pl'
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${aiSearchName}-pl'
        properties: {
          privateLinkServiceId: aiSearchId
          groupIds: [
            'searchService'
          ]
        }
      }
    ]
  }
}

// Private endpoint for storage account (docing)
resource peDocing 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${docingName}-pl'
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${docingName}-pl'
        properties: {
          privateLinkServiceId: docingId
          groupIds: [
            'blob'
          ]
        }
      }
    ]
  }
}

// Private endpoint for Redis
resource peRedis 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${redisName}-pl'
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${redisName}-pl'
        properties: {
          privateLinkServiceId: redisId
          groupIds: [
            'redisCache'
          ]
        }
      }
    ]
  }
}

// Private endpoint for Service Bus
resource peSbus 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${sbusName}-pl'
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${sbusName}-pl'
        properties: {
          privateLinkServiceId: sbusId
          groupIds: [
            'namespace'
          ]
        }
      }
    ]
  }
}

// Private endpoint for SignalR
resource peSignalr 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${signalrName}-pl'
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${signalrName}-pl'
        properties: {
          privateLinkServiceId: signalrId
          groupIds: [
            'signalr'
          ]
        }
      }
    ]
  }
}

// Private endpoint for SQL Server
resource peSqldocgen 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${sqldocgenName}-pl'
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${sqldocgenName}-pl'
        properties: {
          privateLinkServiceId: sqldocgenId
          groupIds: [
            'sqlServer'
          ]
        }
      }
    ]
  }
}

output aiSearchPeName string = peAiSearch.name
output docingPeName string = peDocing.name
output redisPeName string = peRedis.name
output sbusPeName string = peSbus.name
output signalrPeName string = peSignalr.name
output sqldocgenPeName string = peSqldocgen.name
