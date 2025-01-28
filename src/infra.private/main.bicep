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

@description('The resource identifier of the subnet for the private endpoints environment - must have aligned private DNS zones / custom DNS for resolution')
param peSubnet string

@description('The resource identifier of the container apps environment subnet. Must be delegated to the service Microsoft.App/environments')
param containerAppEnvSubnet string

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
    containerAppEnvSubnet: containerAppEnvSubnet
    peSubnet: peSubnet
  }
}
module aiSearch 'aiSearch/aiSearch.module.bicep' = {
  name: 'aiSearch'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    peSubnet: peSubnet
  }
}
module docing 'docing/docing.module.bicep' = {
  name: 'docing'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    peSubnet: peSubnet
  }
}
module insights 'insights/insights.module.bicep' = {
  name: 'insights'
  scope: rg
  params: {
    location: location
    logAnalyticsWorkspaceId: resources.outputs.AZURE_LOG_ANALYTICS_WORKSPACE_ID
  }
}
module redis 'redis/redis.module.bicep' = {
  name: 'redis'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalName: resources.outputs.MANAGED_IDENTITY_NAME
    peSubnet: peSubnet
  }
}
module sbus 'sbus/sbus.module.bicep' = {
  name: 'sbus'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    peSubnet: peSubnet
  }
}
module signalr 'signalr/signalr.module.bicep' = {
  name: 'signalr'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    peSubnet: peSubnet
  }
}
module sqldocgen 'sqldocgen/sqldocgen.module.bicep' = {
  name: 'sqldocgen'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalName: resources.outputs.MANAGED_IDENTITY_NAME
    peSubnet: peSubnet
  }
}
output MANAGED_IDENTITY_CLIENT_ID string = resources.outputs.MANAGED_IDENTITY_CLIENT_ID
output MANAGED_IDENTITY_NAME string = resources.outputs.MANAGED_IDENTITY_NAME
output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = resources.outputs.AZURE_LOG_ANALYTICS_WORKSPACE_NAME
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = resources.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID
output AZURE_CONTAINER_REGISTRY_NAME string = resources.outputs.AZURE_CONTAINER_REGISTRY_NAME
output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_NAME
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output AISEARCH_CONNECTIONSTRING string = aiSearch.outputs.connectionString
output DOCING_BLOBENDPOINT string = docing.outputs.blobEndpoint
output INSIGHTS_APPINSIGHTSCONNECTIONSTRING string = insights.outputs.appInsightsConnectionString
output REDIS_CONNECTIONSTRING string = redis.outputs.connectionString
output SBUS_SERVICEBUSENDPOINT string = sbus.outputs.serviceBusEndpoint
output SIGNALR_HOSTNAME string = signalr.outputs.hostName
output SQLDOCGEN_SQLSERVERFQDN string = sqldocgen.outputs.sqlServerFqdn

// Custom
output AI_SEARCH_NAME string = aiSearch.outputs.name
output AI_SEARCH_PE_IP string = aiSearch.outputs.pe_ip
output DOCING_NAME string = docing.outputs.name
output DOCING_PE_IP string = docing.outputs.pe_ip
output REDIS_NAME string = redis.outputs.name
output REDIS_PE_IP string = redis.outputs.pe_ip
output SBUS_NAME string = sbus.outputs.name
output SBUS_PE_IP string = sbus.outputs.pe_ip
output SIGNALR_NAME string = signalr.outputs.name
output SIGNALR_PE_IP string = signalr.outputs.pe_ip
output SQLDOCGEN_NAME string = sqldocgen.outputs.name
output SQLDOCGEN_PE_IP string = sqldocgen.outputs.pe_ip
