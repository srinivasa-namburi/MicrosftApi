param uniqueName string
param location string
param keyValueNames array
param keyValueValues array
param tags object = {}

resource appConfigStore 'Microsoft.AppConfiguration/configurationStores@2023-03-01' = {
  name: 'appCS-${uniqueName}'
  location: location
  tags: tags
  sku: {
    name: 'standard'
  }
  identity: {
    type: 'SystemAssigned'
    userAssignedIdentities: {}
  }
  properties: {
    disableLocalAuth: false
    publicNetworkAccess: 'Enabled'
  }
}

resource appConfigStoreKV 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = [for (item, i) in keyValueNames: {
  name: item
  parent: appConfigStore
  properties: {
    // contentType: 'string'
    tags: tags
    value: keyValueValues[i]
  }
}]
