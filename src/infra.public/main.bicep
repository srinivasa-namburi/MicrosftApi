// Copyright (c) Microsoft Corporation. All rights reserved.

targetScope = 'resourceGroup'

@minLength(1)
@maxLength(64)
@description('Name of the resource group')
param resourceGroupName string

@minLength(1)
@description('The location used for all deployed resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

@description('The resource identifier of the subnet for the private endpoints environment – must have aligned private DNS zones / custom DNS for resolution')
param peSubnet string = ''

@description('The resource identifier of the subnet used by the Container Apps instance. Must be delegated to Microsoft.App/environments')
param containerAppEnvSubnet string = ''

@description('The resource identifier of the subnet used by the PostgreSQL Flexible Server. Required for private deployments')
param postgresSubnet string = ''

@description('The resource identifier of the subnet used by the private DNS zone for PostgreSQL Flexible Server. Required for private deployments with PostgreSQL')
param postgresDnsZoneId string = ''

@description('If the SQL server is already existing')
param existingSqlServer bool = false

@description('Administrator password for PostgreSQL')
@secure()
param administratorPassword string

@allowed([
  'public'
  'private'
])
@description('Deployment model: public or private')
param deploymentModel string = 'public'

@allowed([
  'aisearch'
  'postgres'
])
@description('Memory backend to use: aisearch or postgres')
param memoryBackend string = 'aisearch'

var isPrivate = deploymentModel == 'private'
var tags = {
  'azd-env-name': resourceGroupName
}

module resources 'resources.bicep' = {
  scope: resourceGroup('${resourceGroupName}')
  name: 'resources'
  params: {
    location: location
    tags: tags
    principalId: principalId
    containerAppEnvSubnet: containerAppEnvSubnet
    deploymentModel: deploymentModel
  }
}

module aiSearch 'aiSearch/aiSearch.module.bicep' = if (memoryBackend == 'aisearch') {
  name: 'aiSearch'
  scope: resourceGroup('${resourceGroupName}')
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    deploymentModel: deploymentModel
  }
}

module docing 'docing/docing.module.bicep' = {
  name: 'docing'
  scope: resourceGroup('${resourceGroupName}')
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    deploymentModel: deploymentModel
  }
}

module insights 'insights/insights.module.bicep' = {
  name: 'insights'
  scope: resourceGroup('${resourceGroupName}')
  params: {
    location: location
    logAnalyticsWorkspaceId: resources.outputs.AZURE_LOG_ANALYTICS_WORKSPACE_ID
  }
}

module redis 'redis/redis.module.bicep' = {
  name: 'redis'
  scope: resourceGroup('${resourceGroupName}')
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalName: resources.outputs.MANAGED_IDENTITY_NAME
    deploymentModel: deploymentModel
  }
}

module sbus 'sbus/sbus.module.bicep' = {
  name: 'sbus'
  scope: resourceGroup('${resourceGroupName}')
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    deploymentModel: deploymentModel
  }
}

module signalr 'signalr/signalr.module.bicep' = {
  name: 'signalr'
  scope: resourceGroup('${resourceGroupName}')
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    deploymentModel: deploymentModel
  }
}

module sqldocgen 'sqldocgen/sqldocgen.module.bicep' = {
  name: 'sqldocgen'
  scope: resourceGroup('${resourceGroupName}')
  params: {
    location: location
    sqlAdminLogin: 'sqladmin'
    deploymentModel: deploymentModel
    exists: existingSqlServer 
  }
}

module eventhub 'eventhub/eventhub.module.bicep' = {
  name: 'eventhub'
  scope: resourceGroup('${resourceGroupName}')
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    deploymentModel: deploymentModel
  }
}

module orleansStorage 'orleans/orleans.module.bicep' = {
  name: 'orleansStorage'
  scope: resourceGroup('${resourceGroupName}')
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    deploymentModel: deploymentModel
  }
}

module kmvectordb 'kmvectordb/kmvectordb.module.bicep' = if (memoryBackend == 'postgres') {
  name: 'kmvectordb'
  scope: resourceGroup('${resourceGroupName}')
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    deploymentModel: deploymentModel
    postgresSubnet: isPrivate ? postgresSubnet : ''
    postgresDnsZoneId: isPrivate ? postgresDnsZoneId : ''
    administratorPassword: administratorPassword
  }
}

// Deploy private endpoints if deploymentModel is private.
module privateEndpoints 'privateEndpoints.bicep' = if (isPrivate) {
  name: 'privateEndpoints'
  scope: resourceGroup('${resourceGroupName}')
  params: {
    location: location
    peSubnet: peSubnet
    aiSearchId: memoryBackend == 'aisearch' ? aiSearch.outputs.resourceId : ''
    aiSearchName: memoryBackend == 'aisearch' ? aiSearch.outputs.resourceName : ''
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
    eventhubId: eventhub.outputs.resourceId
    eventhubName: eventhub.outputs.resourceName
    orleansStorageId: orleansStorage.outputs.resourceId
    orleansStorageName: orleansStorage.outputs.resourceName
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
output AISEARCH_CONNECTIONSTRING string = memoryBackend == 'aisearch' ? aiSearch.outputs.connectionString : ''
output DOCING_BLOBENDPOINT string = docing.outputs.blobEndpoint
output INSIGHTS_APPINSIGHTSCONNECTIONSTRING string = insights.outputs.appInsightsConnectionString
output REDIS_CONNECTIONSTRING string = redis.outputs.connectionString
output SBUS_SERVICEBUSENDPOINT string = sbus.outputs.serviceBusEndpoint
output SIGNALR_HOSTNAME string = signalr.outputs.hostName
output SQLDOCGEN_SQLSERVERFQDN string = sqldocgen.outputs.sqlServerFqdn

output EVENTHUB_CONNECTIONSTRING string = eventhub.outputs.connectionString
output EVENTHUB_NAME string = eventhub.outputs.eventHubName
output EVENTHUB_CONSUMERGROUP string = eventhub.outputs.consumerGroupName
output EVENTHUB_EVENTHUBSENDPOINT string = eventhub.outputs.eventHubsEndpoint

output ORLEANS_STORAGE_BLOBENDPOINT string = orleansStorage.outputs.blobEndpoint
output ORLEANS_STORAGE_CONNECTIONSTRING string = orleansStorage.outputs.connectionString
output ORLEANS_BLOB_CONNECTIONSTRING string = orleansStorage.outputs.blobConnectionString
output ORLEANS_STORAGE_TABLEENDPOINT string = orleansStorage.outputs.tableEndpoint
output ORLEANS_TABLE_CONNECTIONSTRING string = orleansStorage.outputs.tableConnectionString

output KMVECTORDB_SERVER_CONNECTIONSTRING string = memoryBackend == 'postgres' ? kmvectordb.outputs.connectionString : ''
output KMVECTORDB_SERVER_FQDN string = memoryBackend == 'postgres' ? kmvectordb.outputs.serverFqdn : ''

output AI_SEARCH_RESOURCE_ID string = memoryBackend == 'aisearch' ? aiSearch.outputs.resourceId : ''
output DOCING_RESOURCE_ID string = docing.outputs.resourceId
output REDIS_RESOURCE_ID string = redis.outputs.resourceId
output SBUS_RESOURCE_ID string = sbus.outputs.resourceId
output SIGNALR_RESOURCE_ID string = signalr.outputs.resourceId
output SQLDOCGEN_RESOURCE_ID string = sqldocgen.outputs.resourceId
output EVENTHUB_RESOURCE_ID string = eventhub.outputs.resourceId
output ORLEANS_STORAGE_RESOURCE_ID string = orleansStorage.outputs.resourceId
output KMVECTORDB_RESOURCE_ID string = memoryBackend == 'postgres' ? kmvectordb.outputs.resourceId : ''
