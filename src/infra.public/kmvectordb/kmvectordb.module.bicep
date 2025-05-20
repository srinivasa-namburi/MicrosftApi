targetScope = 'resourceGroup'

@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location
param principalId string
param principalType string
@description('Deployment model: public or private')
param deploymentModel string
@description('The resource identifier of the subnet used by the PostgreSQL Flexible Server. Required for private deployments')
param postgresSubnet string = ''
@description('The resource identifier of the private DNS zone for PostgreSQL Flexible Server. Required for private deployments')
param postgresDnsZoneId string = ''

// Parameters for PostgreSQL administrator
@secure()
@description('The password for the PostgreSQL administrator')
param administratorPassword string

var isPrivate = deploymentModel == 'private'

// PostgreSQL Flexible Server
resource kmvectordb 'Microsoft.DBforPostgreSQL/flexibleServers@2023-03-01-preview' = {
  name: take('kmvectordb${uniqueString(resourceGroup().id)}', 63)
  location: location
  sku: {
    name: 'Standard_E4ds_v4'
    tier: 'MemoryOptimized'
  }
  properties: {
    version: '16'
    administratorLogin: 'pgadmin'
    administratorLoginPassword: administratorPassword
    storage: {
      storageSizeGB: 1024
      autoGrow: 'Enabled'
      tier: 'P30'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    network: {
      delegatedSubnetResourceId: isPrivate ? postgresSubnet : null
      privateDnsZoneArmResourceId: isPrivate ? postgresDnsZoneId : null
    }
    highAvailability: {
      mode: 'Disabled'
    }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
    }
  }
  tags: {
    'aspire-resource-name': 'kmvectordb-server'
  }
}

// Database within the server
resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-03-01-preview' = {
  name: 'kmvectordb'
  parent: kmvectordb
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// Configure server parameters to allow the vector extension
resource serverParametersVector 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2023-03-01-preview' = {
  name: 'azure.extensions'
  parent: kmvectordb
  properties: {
    value: 'vector'
    source: 'user-override'
  }
  dependsOn: [
    database
  ]
}

// NOTE: After allowing the extension at server level, you still need to create it in each database:
// CREATE EXTENSION IF NOT EXISTS vector;
// See SetupDataInitializerService.cs for the implementation

// RBAC assignment for the User Assigned Managed Identity
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kmvectordb.id, principalId, 'PostgreSQL Flexible Server Contributor')
  scope: kmvectordb
  properties: {
    principalId: principalId
    principalType: principalType
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c') // Contributor role
  }
}

// Add firewall rule to allow access from all Azure services (when not in private mode)
resource firewallRuleAllowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-03-01-preview' = if (!isPrivate) {
  name: 'AllowAllAzureServices'
  parent: kmvectordb
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output connectionString string = 'Host=${kmvectordb.properties.fullyQualifiedDomainName};Database=kmvectordb;Username=${'pgadmin'};Password=${administratorPassword}'
output resourceId string = kmvectordb.id
output resourceName string = kmvectordb.name
output serverFqdn string = kmvectordb.properties.fullyQualifiedDomainName
