targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment used as part of the naming convention. The resource group will be named rg-<environmentName>')
param environmentName string

@minLength(1)
@description('The location used for all deployed resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

@description('The resource identifier of the subnet for the private endpoints environment – must have aligned private DNS zones / custom DNS for resolution')
param peSubnet string = ''

@description('The resource identifier of the subnet used by the Container Apps instance. Must be delegated to Microsoft.App/environments')
param containerAppEnvSubnet string = ''

@description('If the SQL server is already existing')
param existingSqlServer bool

@allowed([
  'public'
  'private'
])
@description('Deployment model: public or private')
param deploymentModel string = 'public'

var isPrivate = deploymentModel == 'private'
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
    deploymentModel: deploymentModel
  }
}

module aiSearch 'aiSearch/aiSearch.module.bicep' = {
  name: 'aiSearch'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    deploymentModel: deploymentModel
  }
}

module docing 'docing/docing.module.bicep' = {
  name: 'docing'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    deploymentModel: deploymentModel
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
    deploymentModel: deploymentModel
  }
}

module sbus 'sbus/sbus.module.bicep' = {
  name: 'sbus'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    deploymentModel: deploymentModel
  }
}

module signalr 'signalr/signalr.module.bicep' = {
  name: 'signalr'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    deploymentModel: deploymentModel
  }
}

module sqldocgen 'sqldocgen/sqldocgen.module.bicep' = {
  name: 'sqldocgen'
  scope: rg
  params: {
    location: location
    sqlAdminLogin: 'sqladmin'
    deploymentModel: deploymentModel
    exists: existingSqlServer 
  }
}
// Deploy private endpoints if deploymentModel is private.
module privateEndpoints 'privateEndpoints.bicep' = if (isPrivate) {
  name: 'privateEndpoints'
  scope: rg
  params: {
    location: location
    peSubnet: peSubnet
    aiSearchId: aiSearch.outputs.resourceId
    aiSearchName: aiSearch.outputs.resourceName
    docingId: docing.outputs.resourceId
    docingName: docing.outputs.resourceName
    redisId: redis.outputs.resourceId
    redisName: redis.outputs.resourceName
    sbusId: sbus.outputs.resourceId
    sbusName: sbus.outputs.resourceName
    signalrId: signalr.outputs.resourceId
    signalrName: signalr.outputs.resourceName
    sqldocgenId: sqldocgen.outputs.resourceId
    sqldocgenName: sqldocgen.outputs.resourceName
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

// Custom outputs
output AI_SEARCH_RESOURCE_ID string = aiSearch.outputs.resourceId
output DOCING_RESOURCE_ID string = docing.outputs.resourceId
output REDIS_RESOURCE_ID string = redis.outputs.resourceId
output SBUS_RESOURCE_ID string = sbus.outputs.resourceId
output SIGNALR_RESOURCE_ID string = signalr.outputs.resourceId
output SQLDOCGEN_RESOURCE_ID string = sqldocgen.outputs.resourceId
