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

// @secure()
// param sqlPassword string

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

module aiSearch 'aiSearch/aiSearch.module.bicep' = {
  name: 'aiSearch'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
  }
}
module docing 'docing/docing.module.bicep' = {
  name: 'docing'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
  }
}
module redis 'redis/redis.module.bicep' = {
  name: 'redis'
  scope: rg
  params: {
    keyVaultName: resources.outputs.SERVICE_BINDING_KVB6088994_NAME
    location: location
  }
}
module sbus 'sbus/sbus.module.bicep' = {
  name: 'sbus'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
  }
}
module signalr 'signalr/signalr.module.bicep' = {
  name: 'signalr'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
  }
}
module sqldocgen 'sqldocgen/sqldocgen.module.bicep' = {
  name: 'sqldocgen'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalName: resources.outputs.MANAGED_IDENTITY_NAME
  }
}
output MANAGED_IDENTITY_CLIENT_ID string = resources.outputs.MANAGED_IDENTITY_CLIENT_ID
output MANAGED_IDENTITY_NAME string = resources.outputs.MANAGED_IDENTITY_NAME
output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = resources.outputs.AZURE_LOG_ANALYTICS_WORKSPACE_NAME
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = resources.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output SERVICE_BINDING_KVB6088994_ENDPOINT string = resources.outputs.SERVICE_BINDING_KVB6088994_ENDPOINT

output AISEARCH_CONNECTIONSTRING string = aiSearch.outputs.connectionString
output DOCING_BLOBENDPOINT string = docing.outputs.blobEndpoint
output SBUS_SERVICEBUSENDPOINT string = sbus.outputs.serviceBusEndpoint
output SIGNALR_HOSTNAME string = signalr.outputs.hostName
output SQLDOCGEN_SQLSERVERFQDN string = sqldocgen.outputs.sqlServerFqdn
