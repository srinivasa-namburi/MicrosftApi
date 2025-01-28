@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param principalId string

param principalName string

@description('')
param peSubnet string = ''

resource sqldocgen 'Microsoft.Sql/servers@2024-05-01-preview' = {
  name: take('sqldocgen${uniqueString(resourceGroup().id)}', 63)
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
    version: '12.0'
  }
  tags: {
    'aspire-resource-name': 'sqldocgen'
  }
}

resource peSqldocgen 'Microsoft.Network/privateEndpoints@2023-11-01' = if (peSubnet != '') {
  name: '${sqldocgen.name}-pl'
  tags: {
    'aspire-resource-name': 'sqldocgen'
  }
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${sqldocgen.name}-pl'
        properties: {
          privateLinkServiceId: sqldocgen.id
          groupIds: ['sqlServer']
        }
      }
    ]
  }
}

resource sqlAdmins 'Microsoft.Sql/servers/administrators@2024-05-01-preview' = {
  parent: sqldocgen
  name: 'ActiveDirectory'
  properties: {
      administratorType: 'ActiveDirectory'
      login: principalName
      sid: principalId
      tenantId: subscription().tenantId
    } 
  }

resource sqlFirewallRule_AllowAllAzureIps 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  name: 'AllowAllAzureIps'
  properties: {
    endIpAddress: '0.0.0.0'
    startIpAddress: '0.0.0.0'
  }
  parent: sqldocgen
}

resource ProjectVicoDB 'Microsoft.Sql/servers/databases@2021-11-01' = {
  name: 'ProjectVicoDB'
  location: location
  parent: sqldocgen
  sku: {
    name: 'HS_Gen5_2'
  }
}

output sqlServerFqdn string = sqldocgen.properties.fullyQualifiedDomainName

// Custom
output name string = sqldocgen.name
output pe_ip string = peSqldocgen.properties.customDnsConfigs[0].ipAddresses[0]
