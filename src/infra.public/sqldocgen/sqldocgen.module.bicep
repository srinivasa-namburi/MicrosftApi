@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param principalId string

param principalName string

resource sqldocgen 'Microsoft.Sql/servers@2024-05-01-preview' = {
  name: take('sqldocgen${uniqueString(resourceGroup().id)}', 63)
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    version: '12.0'
  }
  tags: {
    'aspire-resource-name': 'sqldocgen'
  }
}

// resource sqlAdmins 'Microsoft.Sql/servers/administrators@2024-05-01-preview' = {
//   parent: sqldocgen
//   name: 'ActiveDirectory'
//   properties: {
//       administratorType: 'ActiveDirectory'
//       login: principalName
//       sid: principalId
//       tenantId: subscription().tenantId
//     } 
//   }

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
