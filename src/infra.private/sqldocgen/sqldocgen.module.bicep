targetScope = 'resourceGroup'

@description('')
param location string = resourceGroup().location

@description('')
param principalId string

@description('')
param principalName string


resource sqlServer_34MHlY0Ot 'Microsoft.Sql/servers@2020-11-01-preview' = {
  name: toLower(take('sqldocgen${uniqueString(resourceGroup().id)}', 24))
  location: location
  tags: {
    'aspire-resource-name': 'sqldocgen'
  }
  properties: {
    version: '12.0'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      login: principalName
      sid: principalId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

resource sqlFirewallRule_n7p8WgM0M 'Microsoft.Sql/servers/firewallRules@2020-11-01-preview' = {
  parent: sqlServer_34MHlY0Ot
  name: 'AllowAllAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
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
