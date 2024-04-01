targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention, the name of the resource group for your application will use this name, prefixed with rg-')
param environmentName string

@minLength(1)
@description('The location used for all deployed resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''


var tags = {
  'azd-env-name': environmentName
}

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    location: location
    tags: tags
    principalId: principalId
  }
}

module redis 'redis/aspire.hosting.azure.bicep.redis.bicep' = {
  name: 'redis'
  scope: rg
  params: {
    location: location
    keyVaultName: resources.outputs.SERVICE_BINDING_REDISKV_NAME
    redisCacheName: 'redis'
  }
}
module sbus 'sbus/aspire.hosting.azure.bicep.servicebus.bicep' = {
  name: 'sbus'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    queues: []
    serviceBusNamespaceName: 'sbus'
    topics: []
  }
}
module signalr 'signalr/aspire.hosting.azure.bicep.signalr.bicep' = {
  name: 'signalr'
  scope: rg
  params: {
    location: location
    name: 'signalr'
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
  }
}
module sqldocgen 'sqldocgen/aspire.hosting.azure.bicep.sql.bicep' = {
  name: 'sqldocgen'
  scope: rg
  params: {
    location: location
    databases: ['ProjectVicoDB']
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalName: resources.outputs.MANAGED_IDENTITY_NAME
    serverName: 'sqldocgen'
  }
}
output MANAGED_IDENTITY_CLIENT_ID string = resources.outputs.MANAGED_IDENTITY_CLIENT_ID
output MANAGED_IDENTITY_NAME string = resources.outputs.MANAGED_IDENTITY_NAME
output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = resources.outputs.AZURE_LOG_ANALYTICS_WORKSPACE_NAME
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = resources.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output SERVICE_BINDING_REDISKV_ENDPOINT string = resources.outputs.SERVICE_BINDING_REDISKV_ENDPOINT

output SBUS_SERVICEBUSENDPOINT string = sbus.outputs.serviceBusEndpoint
output SIGNALR_HOSTNAME string = signalr.outputs.hostName
output SQLDOCGEN_SQLSERVERFQDN string = sqldocgen.outputs.sqlServerFqdn