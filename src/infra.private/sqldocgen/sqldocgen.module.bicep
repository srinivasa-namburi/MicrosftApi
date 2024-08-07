targetScope = 'resourceGroup'

@description('')
param location string = resourceGroup().location

@description('')
param principalId string

@description('')
param principalName string

@description('')
param peSubnet string = ''

@description('')
param tags object = {}

resource sqlServer_34MHlY0Ot 'Microsoft.Sql/servers@2020-11-01-preview' = {
  name: toLower(take('sqldocgen${uniqueString(resourceGroup().id)}', 24))
  location: location
  tags: {
    'aspire-resource-name': 'sqldocgen'
  }
  properties: {
    version: '12.0'
    publicNetworkAccess: 'Disabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      login: principalName
      sid: principalId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

resource peSqlServer_34MHlY0Ot 'Microsoft.Network/privateEndpoints@2023-11-01' = if (peSubnet != '') {
  name: '${sqlServer_34MHlY0Ot.name}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${sqlServer_34MHlY0Ot.name}-pl'
        properties: {
          privateLinkServiceId: sqlServer_34MHlY0Ot.id
          groupIds: ['sqlServer']
        }
      }
    ]
  }
}

resource sqlDatabase_RnsdBrRX2 'Microsoft.Sql/servers/databases@2020-11-01-preview' = {
  parent: sqlServer_34MHlY0Ot
  name: 'ProjectVicoDB'
  location: location
  sku: {
    name: 'HS_Gen5_6'
  }
  properties: {
  }
}

output sqlServerFqdn string = sqlServer_34MHlY0Ot.properties.fullyQualifiedDomainName

// Custom
output name string = sqlServer_34MHlY0Ot.name
output pe_ip string = peSqlServer_34MHlY0Ot.properties.customDnsConfigs[0].ipAddresses[0]
