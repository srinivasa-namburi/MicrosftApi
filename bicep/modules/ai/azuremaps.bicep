param uniqueName string
param location string
param tags object = {}

resource mapAccount 'Microsoft.Maps/accounts@2023-06-01' = {
  name: 'map-${uniqueName}'
  location: location
  tags: tags
  sku: {
    name: 'G2'
  }
  kind: 'Gen2'
  identity: {
    type: 'SystemAssigned'
    userAssignedIdentities: {}
  }
  properties: {
    cors: {
      corsRules: [
        {
          allowedOrigins: [
            '*'
          ]
        }
      ]
    }
    disableLocalAuth: false // TODO replace with AAD auth
    /*
    linkedResources: [
      {
        id: 'string'
        uniqueName: 'string'
      }
    ]
    */
  }
}
