@description('Location for the private endpoints.')
param location string = resourceGroup().location

@description('Resource ID of the subnet that hosts private endpoints.')
param peSubnet string

@description('Resource ID of the Azure SQL logical server.')
param sqldocgenId string

@description('Resource name of the Azure SQL logical server.')
param sqldocgenName string

@description('Resource ID of the docing storage account.')
param docingId string

@description('Resource name of the docing storage account.')
param docingName string

@description('Resource ID of the Orleans storage account.')
param orleansStorageId string

@description('Resource name of the Orleans storage account.')
param orleansStorageName string

@description('Resource ID of the Azure Event Hubs namespace. Leave empty to skip.')
param eventhubId string = ''

@description('Resource name of the Azure Event Hubs namespace.')
param eventhubName string = ''

@description('Resource ID of the Azure AI Search service. Leave empty to skip.')
param aiSearchId string = ''

@description('Resource name of the Azure AI Search service.')
param aiSearchName string = ''

var sqlEndpointName = take('${sqldocgenName}-sql-pe', 80)
var docingBlobPeName = take('${docingName}-blob-pe', 80)
var orleansBlobPeName = take('${orleansStorageName}-blob-pe', 80)
var orleansTablePeName = take('${orleansStorageName}-table-pe', 80)
var searchPeName = take('${aiSearchName}-search-pe', 80)
var eventhubPeName = take('${eventhubName}-namespace-pe', 80)

resource peSql 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: sqlEndpointName
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: sqlEndpointName
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

resource peDocingBlob 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: docingBlobPeName
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: docingBlobPeName
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

resource peOrleansBlob 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: orleansBlobPeName
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: orleansBlobPeName
        properties: {
          privateLinkServiceId: orleansStorageId
          groupIds: [
            'blob'
          ]
        }
      }
    ]
  }
}

resource peOrleansTable 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: orleansTablePeName
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: orleansTablePeName
        properties: {
          privateLinkServiceId: orleansStorageId
          groupIds: [
            'table'
          ]
        }
      }
    ]
  }
}

// Docing only requires blob private endpoint; queue/table endpoints intentionally omitted

// Orleans storage requires blob and table private endpoints; queue omitted per design

resource peSearch 'Microsoft.Network/privateEndpoints@2023-11-01' = if (!empty(aiSearchId)) {
  name: searchPeName
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: searchPeName
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

resource peEventHub 'Microsoft.Network/privateEndpoints@2023-11-01' = if (!empty(eventhubId)) {
  name: eventhubPeName
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: eventhubPeName
        properties: {
          privateLinkServiceId: eventhubId
          groupIds: [
            'namespace'
          ]
        }
      }
    ]
  }
}

output sqlPrivateEndpointName string = peSql.name
output docingBlobPrivateEndpointName string = peDocingBlob.name
output orleansBlobPrivateEndpointName string = peOrleansBlob.name
output orleansTablePrivateEndpointName string = peOrleansTable.name
output searchPrivateEndpointName string = !empty(aiSearchId) ? peSearch.name : ''
output eventhubPrivateEndpointName string = !empty(eventhubId) ? peEventHub.name : ''
