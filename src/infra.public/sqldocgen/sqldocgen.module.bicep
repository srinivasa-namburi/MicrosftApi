@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@description('SQL administrator login for the new server. Must be 1-16 characters and follow SQL Server naming rules.')
param sqlAdminLogin string = 'sqladmin'

@description('Deployment model: public or private')
param deploymentModel string

@description('Indicates whether the SQL server already exists (true) or not (false)')
param exists bool = false


var sqlAdminPassword = guid(take('sqldocgen${uniqueString(resourceGroup().id)}', 63))
var isPrivate = deploymentModel == 'private'

resource sqldocgen 'Microsoft.Sql/servers@2024-05-01-preview' = {
  name: take('sqldocgen${uniqueString(resourceGroup().id)}', 63)
  location: location
  properties: union(
    {
      publicNetworkAccess: deploymentModel == 'private' ? 'Disabled' : 'Enabled'
      version: '12.0'
    },
    exists ? {} : {
      administratorLogin: sqlAdminLogin
      administratorLoginPassword: sqlAdminPassword
    }
  )
  tags: {
    'aspire-resource-name': 'sqldocgen'
  }
}

// Allow all Azure IPs to access the server - this only works if publicNetworkAccess is enabled
resource sqlFirewallRule_AllowAllAzureIps 'Microsoft.Sql/servers/firewallRules@2021-11-01' = if (!isPrivate) {
  name: 'AllowAllAzureIps'
  properties: {
    endIpAddress: '0.0.0.0'
    startIpAddress: '0.0.0.0'
  }
  parent: sqldocgen
}


resource ProjectVicoDB 'Microsoft.Sql/servers/databases@2024-05-01-preview' = {
  name: 'ProjectVicoDB'
  location: location
  parent: sqldocgen
  sku: {
    name: 'HS_Gen5_2'
  }
}

output sqlServerFqdn string = sqldocgen.properties.fullyQualifiedDomainName
output resourceId string = sqldocgen.id
output resourceName string = sqldocgen.name
