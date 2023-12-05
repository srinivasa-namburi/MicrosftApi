/*
Copyright (c) Microsoft. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.

Bicep template for deploying Project Vico Azure resources.
*/
/*
@description('Deployment name')
param deploymentName string = 'vico-deployment'
*/

@description ('Unique deployment name')
param uniqueName string = ''

@description('SKU for the Azure App Service plan for webapi')
@allowed([ 'B1', 'S1', 'S2', 'S3', 'P1V3', 'P2V3', 'P3V3', 'I1V2', 'I2V2', 'EP3' ])
param webapiASPsku string = 'P3V3'

@description('SKU for the Azure App Service plan for plugin functions')
@allowed([ 'B1', 'S1', 'S2', 'S3', 'P1V3', 'P2V3', 'P3V3', 'I1V2', 'I2V2', 'EP3' ])
param pluginASPsku string = 'EP3'

@description('Underlying AI service')
@allowed([
  'AzureOpenAI'
  'OpenAI'
])
param aiService string = 'AzureOpenAI'

@description('Model to use for chat completions')
param completionModel string = 'gpt-4-32k'

@description('Model version for chat completions')
param completionModelVersion string = '0613'

@description('Model to use for text embeddings')
param embeddingModel string = 'text-embedding-ada-002'

@description('Model version for text embeddings')
param embeddingModelVersion string = '2'

@description('Completion model tokens per Minute Rate Limit (thousands)')
param completionModelTPM int = 80

@description('Embedding model tokens per Minute Rate Limit (thousands)')
param embeddingModelTPM int = 240

@description('Model to use for summarization')
param summarizationModel string = 'davinci-002'

@description('Model version for summarization')
param summarizationModelVersion string = '1'

@description('Summarization model tokens per Minute Rate Limit (thousands)')
param summarizationModelTPM int = 100

@description('Completion model the task planner should use')
param plannerModel string = 'gpt-4-32k'

@description('Cognitive Search legacy index name')
param csLegacyIndexName string = 'section-embeddings'

@description('Cognitive Search Title index name')
param csTitleIndexName string = 'index-01-titles'

@description('Cognitive Search Section index name')
param csSectionIndexName string = 'index-01-sections'

@description('Cognitive Search Semantic Search config name')
param csSemanticSearchConfigName string = 'smr-semantic-search-config'

@description('Cognitive Search Vector Search profile name')
param csVectorSearchProfileName string = 'smr-vector-search-profile'

@description('Cognitive Search Vector Search HNSW config name')
param csVectorSearchHnswConfigName string = 'smr-hnsw-config'

@description('Azure OpenAI endpoint to use (Azure OpenAI only)')
param aiEndpoint string = ''

@secure()
@description('Azure OpenAI or OpenAI API key')
param aiApiKey string = ''

@description('Azure AD client ID for the backend web API')
param webApiClientId string = ''

@description('Azure AD tenant ID for authenticating users')
param azureAdTenantId string = ''

@description('Azure AD cloud instance for authenticating users')
param azureAdInstance string = environment().authentication.loginEndpoint

@description('Whether to deploy a new Azure OpenAI instance')
param deployNewAzureOpenAI bool = true

@description('Whether to deploy Cosmos DB for persistent chat storage')
param deployCosmosDB bool = true

@description('Blob containers to deploy for linked Form Recognizer')
param blobContainers array = [
  'ingest'
  'trainingdata'
  'ingest-results'
]

@description('What method to use to persist embeddings')
@allowed([
  'Volatile'
  'AzureCognitiveSearch'
  // 'Qdrant' - not implemented
  // 'Postgres' - not implemented
])
param memoryStore string = 'Volatile'

@description('Region for the resources')
/*
@allowed([
  'eastus'
  'australiaeast'
  'canadaeast'
  'eastus2'
  'francecentral'
  'japaneast'
  'northcentralus'
  'swedencentral'
  'switzerlandnorth'
  'uksouth'
  'westeurope'
])
*/
param location string = resourceGroup().location

@description('Region for the Azure Maps account')
@allowed([
  'westcentralus'
  'westus2'
  'eastus'
  'westeurope'
  'northeurope'
])
param azureMapsLocation string = 'eastus'

@description('Region for the Document Intelligence account')
@allowed([
  'australiaeast'
  'brazilsouth'
  'canadacentral'
  'centralindia'
  'centralus'
  'eastasia'
  'eastus'
  'eastus2'
  'francecentral'
  'japaneast'
  'southcentralus'
  'southeastasia'
  'uksouth'
  'westeurope'
  'westus2'
])
param documentIntelligenceLocation string = 'eastus'

// https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models#gpt-4-and-gpt-4-turbo-preview-model-availability
@description('Region for the OpenAI account')
@allowed([
  'australiaeast'
  'canadaeast'
  'francecentral'
  'swedencentral'
  'switzerlandnorth'
])
param openaiLocation string = 'swedencentral'

@description('Tags to apply to all resources')
param tags object = {
  project: 'vico'
  environment: 'dev'
  version: '0.2'
}

@description('Function apps to deploy')
param functionAppNameArray array = [
  'DocQnA'
  'GeographicalData'
  // 'Earthquake'
  // 'Weather'
]
/*
@description('Names of private DNS zones to deploy')
param privateDnsZoneNames array = [
  'privatelink.search.windows.net'
]
*/
@description('Maximum capacity for the application gateway')
param appgwMaxCapacity int = 10

// Removed to facilitate redeploys under different name
// @description('Hash of the resource group ID')
// var rgIdHash = uniqueString(resourceGroup().id)

// @description('Deployment name unique to resource group')
// var uniqueName = rgIdHash

@description('VNET object for the Project Vico spoke')
param vnetObject object = {}



// Open AI deployment

resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: 'openai-${uniqueName}'
  location: openaiLocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: toLower(uniqueName)
  }
  tags: tags
}

resource openAI_completionModel 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: completionModel
  properties: {
    model: {
      format: 'OpenAI'
      name: completionModel
      version: completionModelVersion
    }
  }
  sku: {
    name: 'Standard'
    capacity: completionModelTPM
  }
}

resource openAI_embeddingModel 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: embeddingModel
  properties: {
    model: {
      format: 'OpenAI'
      name: embeddingModel
      version: embeddingModelVersion
    }
  }
    sku: {
      name: 'Standard'
      capacity: embeddingModelTPM
    }
  dependsOn: [// This "dependency" is to create models sequentially because the resource
    openAI_completionModel // provider does not support parallel creation of models properly.
  ]
}

resource openAI_summarizationModel 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: summarizationModel
  properties: {
    model: {
      format: 'OpenAI'
      name: summarizationModel
      version: summarizationModelVersion
    }
  }
    sku: {
      name: 'Standard'
      capacity: summarizationModelTPM
    }
  dependsOn: [// This "dependency" is to create models sequentially because the resource
    openAI_embeddingModel // provider does not support parallel creation of models properly.
  ]
}


// VNET Project Vico
module spokeVnet 'modules/networking/spoke.bicep' = {
  name: 'spoke-${uniqueName}'
  scope: resourceGroup()
  params: {
    uniqueName: uniqueName
    location: location
    vnetObject: vnetObject
    tags: tags
  }
}

// Deploy Key Vault
// Currently public
module keyvault 'modules/management/keyvault.bicep' = {
  name: 'kv-${uniqueName}'
  scope: resourceGroup()
  params: {
    uniqueName: uniqueName
    location: location
    tags: tags
  }
}

// Deploy Application Gateway

resource publicIPAddresses 'Microsoft.Network/publicIPAddresses@2021-05-01' = {
  name: 'pip-appgw-${uniqueName}'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
  }
  properties: {
    publicIPAddressVersion: 'IPv4'
    publicIPAllocationMethod: 'Static'
    idleTimeoutInMinutes: 4
  }
}

resource umsiAppGW 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'umsi-appgw-${uniqueName}'
  location: location
  tags: tags
}


module appgw 'modules/networking/applicationgateway.bicep' = {
  name: 'appgw-${uniqueName}'
  dependsOn: [spokeVnet]
  scope: resourceGroup()
  params: {
    uniqueName: uniqueName
    location: location
    subnetId: spokeVnet.outputs.subnetsId[0]
    umsiAppGWId: umsiAppGW.id
    SemanticKernelWebFQDN: SemanticKernelWeb.properties.defaultHostName
    publicIPAddressesId: publicIPAddresses.id
    appgwMaxCapacity: appgwMaxCapacity
    tags: tags
  }
}

// Deploy private DNS Zones
/*
resource privateDnsZones 'Microsoft.Network/privateDnsZones@2020-06-01' = [for dnsZoneName in privateDnsZoneNames: {
  name: dnsZoneName
  location: 'global'
  tags: tags
}]
*/
// Deploy Log Analytics workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'la-${uniqueName}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 90
    features: {
      searchVersion: 1
      legacy: 0
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Deploy Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appins-${uniqueName}'
  location: location
  kind: 'string'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

// Deploy Azure Maps
resource mapAccount 'Microsoft.Maps/accounts@2023-06-01' = {
  name: 'map-${uniqueName}'
  location: azureMapsLocation
  tags: tags
  sku: {
    name: 'G2'
  }
  kind: 'Gen2'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    cors: {
      corsRules: [
        {
          allowedOrigins: [
            '*'
          ]
        }
      ]
    }
    disableLocalAuth: false // TODO replace with AAD auth
  }
}

// Deploy Form Recognizer / Azure Document Intelligence
resource formRecognizer 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: 'formr-${uniqueName}'
  location: documentIntelligenceLocation
  tags: tags
  sku: {
    name: 'S0'
  }
  kind: 'FormRecognizer'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    disableLocalAuth: false // TODO replace with AAD auth
    apiProperties: {
      statisticsEnabled: false
    }
  }
}

resource staFormRecognizer 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: take('stadi${uniqueName}', 23)
  location: location
  tags: tags
  sku: {
    name: 'Standard_ZRS'
  }
  identity: {
    type: 'SystemAssigned'
  }
  kind: 'StorageV2'
  properties: {
    isHnsEnabled: true
    supportsHttpsTrafficOnly: true
    accessTier: 'Hot'
    allowBlobPublicAccess: false // require auth to read blobs
    allowSharedKeyAccess: true // change to false when access keys are no longer required
    minimumTlsVersion: 'TLS1_2'
    networkAcls: {
      defaultAction: 'Allow' // Deny when proper network segmentation is done and PE are created
      // bypass: 'AzureServices'
      // ipRules: (allowIpRanges == []) ? null : ipRules
    }
  }
}

resource blobFeed 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' = {
  name: 'default'
  parent: staFormRecognizer
  properties: {
    changeFeed: {
      enabled: false
    }
    cors: {
      corsRules: [
          {
              allowedOrigins: [
                  'https://documentintelligence.ai.azure.com'
              ]
              allowedMethods: [
                  'DELETE'
                  'GET'
                  'HEAD'
                  'MERGE'
                  'OPTIONS'
                  'PATCH'
                  'POST'
                  'PUT'
              ]
              maxAgeInSeconds: 120
              exposedHeaders: [
                  '*'
              ]
              allowedHeaders: [
                  '*'
              ]
          }
      ]
  }
  }
}

resource containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-09-01' = [for blobContainer in blobContainers: {
  parent: blobFeed
  name: blobContainer
  properties: {
    publicAccess: 'None'
  }
  dependsOn: []
}]

resource assignStorageRoles 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = {
  name: guid('stadi${uniqueName}', 'StorageBlobDataContributor', resourceGroup().name, 'stadi', formRecognizer.id)
  scope: staFormRecognizer
  properties: {
    description: 'Assign storage Role'
    principalId: formRecognizer.id // objectId of the VMSS MSI
    principalType: 'ServicePrincipal'
    roleDefinitionId: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  }
}

// Deploy Cognitive Search

resource azureCognitiveSearch 'Microsoft.Search/searchServices@2022-09-01' = if (memoryStore == 'AzureCognitiveSearch') {
  name: 'acs-${uniqueName}'
  location: location
  sku: {
    name: 'standard'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
  }
}

// Deploy Azure App Config
// Add secrets // TODO move to KeyVault integration of Azure App Config (requires code change)
var secretKeyValueNames = [
  'AI:CognitiveSearch:Endpoint'
  'AI:CognitiveSearch:Key'
  'AI:CognitiveSearch:Index'
  'AI:CognitiveSearch:TitleIndex'
  'AI:CognitiveSearch:SectionIndex'
  'AI:CognitiveSearch:SemanticSearchConfigName'
  'AI:CognitiveSearch:VectorSearchProfileName'
  'AI:CognitiveSearch:VectorSearchHnswConfigName'
  'AI:DocumentIntelligence:Endpoint'
  'AI:DocumentIntelligence:Key'
  'AI:OpenAI:CompletionModel'
  'AI:OpenAI:EmbeddingModel'
  'AI:OpenAI:SummarizationModel'
  'AI:OpenAI:Endpoint'
  'AI:OpenAI:Key'
  'AIService:Endpoint'
  'AIService:Key'
  'AIService:Models:Completion'
  'AIService:Models:Embedding'
  'AIService:Models:Planner'
  'AzureMapsKey'
  'OcrSupport:AzureFormRecognizer:Endpoint'
  'OcrSupport:AzureFormRecognizer:Key'
  'CustomPlugins:OpenAPIPlugins'
  'APPLICATIONINSIGHTS_CONNECTION_STRING'
  'ApplicationInsights:ConnectionString'
]

var hostNamesFapp = [for (functionAppName, i) in functionAppNameArray: 'https://func-vico-${functionAppName}-${uniqueName}.azurewebsites.net/.well-known/ai-plugin.json']

var secretKeyValueValues = [
  (memoryStore) == 'AzureCognitiveSearch'? 'https://${azureCognitiveSearch.name}.search.windows.net': ''
  (memoryStore) == 'AzureCognitiveSearch'? azureCognitiveSearch.listAdminKeys().primaryKey: ''
  csLegacyIndexName
  csTitleIndexName
  csSectionIndexName
  csSemanticSearchConfigName
  csVectorSearchProfileName
  csVectorSearchHnswConfigName
  formRecognizer.properties.endpoint
  formRecognizer.listKeys().key1
  completionModel
  embeddingModel
  summarizationModel
  openAI.properties.endpoint
  openAI.listKeys().key1
  openAI.properties.endpoint
  openAI.listKeys().key1
  completionModel
  embeddingModel
  plannerModel
  mapAccount.listKeys().primaryKey
  formRecognizer.properties.endpoint
  formRecognizer.listKeys().key1
  '["${join(hostNamesFapp, '","')}"]'
  appInsights.properties.ConnectionString
  appInsights.properties.ConnectionString
]

resource appConfigStore 'Microsoft.AppConfiguration/configurationStores@2022-05-01' = {
  name: 'appConfStore-${uniqueName}'
  location: location
  tags: tags
  sku: {
    name: 'standard'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    disableLocalAuth: false
    publicNetworkAccess: 'Enabled'
  }
}

resource appConfigStoreKV 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = [for (item, i) in secretKeyValueNames: {
  name: item
  parent: appConfigStore
  properties: {
    contentType: 'application/json'
    tags: tags
    value: secretKeyValueValues[i]
  }
}]

// Deploy SemanticKernel Infrastructure

// Temp until openAI key is stored in Key Vault to avoid key leakage through Bicep outputs
/*
resource cosmosDBE 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = {
  name: toLower('cosmos-${uniqueName}')
}*/
// ------------------------------------------------------------------------------------
resource SemanticKernelASP 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: 'asp-${uniqueName}-webapi'
  location: location
  kind: 'app'
  sku: {
    name: webapiASPsku
  }
  properties: {
    elasticScaleEnabled: false
    maximumElasticWorkerCount: 1
  }
}

resource SemanticKernelWeb 'Microsoft.Web/sites@2022-09-01' = {
  name: 'app-${uniqueName}-webapi'
  dependsOn: [spokeVnet]
  location: location
  kind: 'app'
  tags: tags
  properties: {
    serverFarmId: SemanticKernelASP.id
    httpsOnly: true
    enabled: true
    virtualNetworkSubnetId: spokeVnet.outputs.subnetsId[1]
  }
}

resource SemanticKernelWebConfig 'Microsoft.Web/sites/config@2022-09-01' = {
  parent: SemanticKernelWeb
  name: 'web'
  properties: {
    metadata: [
      {
        name: 'CURRENT_STACK'
        value: 'dotnetcore'
      }
    ]
    healthCheckPath: '/healthz'
    alwaysOn: true
    cors: {
      allowedOrigins: [
        '*'
      ]
      supportCredentials: false
    }
    detailedErrorLoggingEnabled: true
    minTlsVersion: '1.2'
    netFrameworkVersion: 'v6.0'
    scmType: 'None'
    use32BitWorkerProcess: false // check if this is aligned with dotnet packaging step
    vnetRouteAllEnabled: true
    webSocketsEnabled: true
    appSettings: [
      {
        name: 'Service:AppConfigService'
        value: appConfigStore.listKeys().value[2].connectionString // read-only key
      }
      {
        name: 'ApplicationInsights:ConnectionString'
        value: appInsights.properties.ConnectionString
      }
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: appInsights.properties.ConnectionString
      }
      {
        name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
        value: '~2'
      }
      {
        name: 'WEBSITE_RUN_FROM_PACKAGE'
        value: '1'
      }
    ]
  }
}

resource appInsightExtension 'Microsoft.Web/sites/siteextensions@2022-09-01' = {
  parent: SemanticKernelWeb
  name: 'Microsoft.ApplicationInsights.AzureWebSites'
  dependsOn: [ SemanticKernelWebConfig ]
}

// Deploy Plugins Infrastructure

resource PluginsKernelASP 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: 'asp-${uniqueName}-plugins'
  location: location
  kind: 'app'
  sku: {
    name: pluginASPsku
  }
  properties: {
    elasticScaleEnabled: true
    maximumElasticWorkerCount: 20
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = [for functionAppName in functionAppNameArray: {
  name: toLower(take('sta${toLower(functionAppName)}${uniqueName}', 23))
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    defaultToOAuthAuthentication: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow' // TODO: Change to 'Deny'
    }
  }
}]

resource functionApp 'Microsoft.Web/sites@2022-09-01' = [for (functionAppName, i) in functionAppNameArray: {
  name: 'func-vico-${functionAppName}-${uniqueName}'
  location: location
  kind: 'functionapp'
  dependsOn: [spokeVnet]
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: PluginsKernelASP.id
    httpsOnly: true
    virtualNetworkSubnetId: spokeVnet.outputs.subnetsId[2]
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${toLower(take('sta${toLower(functionAppName)}${uniqueName}', 23))};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount[i].listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${toLower(take('sta${toLower(functionAppName)}${uniqueName}', 23))};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount[i].listKeys().keys[0].value}'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'WEBSITE_NODE_DEFAULT_VERSION'
          value: '~14'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'Service:AppConfigService'
          value: appConfigStore.listKeys().value[2].connectionString // read-only key
        }
        /*{
          name: 'WEBSITE_VNET_ROUTE_ALL'
          value: '1'
        }
        {
          name: 'WEBSITE_CONTENTOVERVNET'
          value: '1'
        }*/
      ]
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      alwaysOn: false
    }
  }
}]


// Deploy Cosmos DB
module cosmosDB 'modules/storage/cosmosdb.bicep' = if (deployCosmosDB) {
  name: 'cosmos-${uniqueName}'
  scope: resourceGroup()
  params: {
    uniqueName: uniqueName
    location: location
    tags: tags
  }
}
/*
resource CosmosPrivateEndpoint 'Microsoft.Network/privateEndpoints@2021-08-01' = if (deployCosmosDB) {
  name: '${cosmosAccount.name}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: virtualNetwork.properties.subnets[4].id
    }
    privateLinkServiceConnections: [
      {
        name: '${cosmosAccount.name}-pl'
        properties: {
          privateLinkServiceId: cosmosAccount.id
          groupIds: [ 'Sql' ] // NoSQL - https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-configure-private-endpoints#private-zone-name-mapping
        }
      }
    ]
  }
}

resource CosmosEndpointAEntry 'Microsoft.Network/privateDnsZones/A@2020-06-01' = if (deployCosmosDB) {
  name: cosmosAccount.name
  parent: privateDnsZones[2]
  properties: {
    ttl: 3600
    aRecords: [
      {
        ipv4Address: CosmosPrivateEndpoint.properties.customDnsConfigs[0].ipAddresses[0]
      }
    ]
  }
}
*/


/*
resource FAPrivateEndpoint 'Microsoft.Network/privateEndpoints@2021-08-01' = [for (functionAppName, i) in functionAppNameArray: {
  name: 'func-${functionAppName}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: virtualNetwork.properties.subnets[3].id
    }
    privateLinkServiceConnections: [
      {
        name: '${functionAppName}-pl'
        properties: {
          privateLinkServiceId: functionApp[i].id
          groupIds: [ 'sites' ]
        }
      }
    ]
  }
}]

resource FAEndpointAEntry 'Microsoft.Network/privateDnsZones/A@2020-06-01' = [for (functionAppName, i) in functionAppNameArray: {
  name: functionAppName
  parent: privateDnsZones[3]
  properties: {
    ttl: 3600
    aRecords: [
      {
        ipv4Address: FAPrivateEndpoint[i].properties.customDnsConfigs[0].ipAddresses[0]
      }
    ]
  }
}]
*/

/*
resource SABlobPrivateEndpoint 'Microsoft.Network/privateEndpoints@2021-08-01' = [for (functionAppName, i) in functionAppNameArray: {
  name: '${storageAccount[i].name}-blob-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: virtualNetwork.properties.subnets[4].id
    }
    privateLinkServiceConnections: [
      {
        name: '${storageAccount[i].name}-blob-pl'
        properties: {
          privateLinkServiceId: storageAccount[i].id
          groupIds: [ 'blob' ]
        }
      }
    ]
  }
}]

resource SABlobEndpointAEntry 'Microsoft.Network/privateDnsZones/A@2020-06-01' = [for (functionAppName, i) in functionAppNameArray: {
  name: 'sta${toLower(functionAppName)}'
  parent: privateDnsZones[4]
  properties: {
    ttl: 3600
    aRecords: [
      {
        ipv4Address: SABlobPrivateEndpoint[i].properties.customDnsConfigs[0].ipAddresses[0]
      }
    ]
  }
}]

resource SAFilePrivateEndpoint 'Microsoft.Network/privateEndpoints@2021-08-01' = [for (functionAppName, i) in functionAppNameArray: {
  name: '${storageAccount[i].name}-file-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: virtualNetwork.properties.subnets[4].id
    }
    privateLinkServiceConnections: [
      {
        name: '${storageAccount[i].name}-file-pl'
        properties: {
          privateLinkServiceId: storageAccount[i].id
          groupIds: [ 'file' ]
        }
      }
    ]
  }
}]

resource SAFileEndpointAEntry 'Microsoft.Network/privateDnsZones/A@2020-06-01' = [for (functionAppName, i) in functionAppNameArray: {
  name: 'sta${toLower(functionAppName)}'
  parent: privateDnsZones[5]
  properties: {
    ttl: 3600
    aRecords: [
      {
        ipv4Address: SAFilePrivateEndpoint[i].properties.customDnsConfigs[0].ipAddresses[0]
      }
    ]
  }
}]
resource SATablePrivateEndpoint 'Microsoft.Network/privateEndpoints@2021-08-01' = [for (functionAppName, i) in functionAppNameArray: {
  name: '${storageAccount[i].name}-table-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: virtualNetwork.properties.subnets[4].id
    }
    privateLinkServiceConnections: [
      {
        name: '${storageAccount[i].name}-table-pl'
        properties: {
          privateLinkServiceId: storageAccount[i].id
          groupIds: [ 'table' ]
        }
      }
    ]
  }
}]
resource SATableEndpointAEntry 'Microsoft.Network/privateDnsZones/A@2020-06-01' = [for (functionAppName, i) in functionAppNameArray: {
  name: 'sta${toLower(functionAppName)}'
  parent: privateDnsZones[6]
  properties: {
    ttl: 3600
    aRecords: [
      {
        ipv4Address: SATablePrivateEndpoint[i].properties.customDnsConfigs[0].ipAddresses[0]
      }
    ]
  }
}]
resource SAQueuePrivateEndpoint 'Microsoft.Network/privateEndpoints@2021-08-01' = [for (functionAppName, i) in functionAppNameArray: {
  name: '${storageAccount[i].name}-queue-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: virtualNetwork.properties.subnets[4].id
    }
    privateLinkServiceConnections: [
      {
        name: '${storageAccount[i].name}-queue-pl'
        properties: {
          privateLinkServiceId: storageAccount[i].id
          groupIds: [ 'queue' ]
        }
      }
    ]
  }
}]
resource SAQueueEndpointAEntry 'Microsoft.Network/privateDnsZones/A@2020-06-01' = [for (functionAppName, i) in functionAppNameArray: {
  name: 'sta${toLower(functionAppName)}'
  parent: privateDnsZones[7]
  properties: {
    ttl: 3600
    aRecords: [
      {
        ipv4Address: SAQueuePrivateEndpoint[i].properties.customDnsConfigs[0].ipAddresses[0]
      }
    ]
  }
}]
*/

/*
resource SKPrivateEndpoint 'Microsoft.Network/privateEndpoints@2021-08-01' = {
  name: '${SemanticKernelWeb.name}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: virtualNetwork.properties.subnets[3].id
    }
    privateLinkServiceConnections: [
      {
        name: '${SemanticKernelWeb.name}-pl'
        properties: {
          privateLinkServiceId: SemanticKernelWeb.id
          groupIds: [ 'sites' ]
        }
      }
    ]
  }
}

resource SKEndpointAEntry 'Microsoft.Network/privateDnsZones/A@2020-06-01' = {
  name: SemanticKernelWeb.name
  parent: privateDnsZones[3]
  properties: {
    ttl: 3600
    aRecords: [
      {
        ipv4Address: SKPrivateEndpoint.properties.customDnsConfigs[0].ipAddresses[0]
      }
    ]
  }
}
*/

/*
resource CSPrivateEndpoint 'Microsoft.Network/privateEndpoints@2021-08-01' = if (memoryStore == 'AzureCognitiveSearch') {
  name: '${azureCognitiveSearch.name}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: virtualNetwork.properties.subnets[5].id
    }
    privateLinkServiceConnections: [
      {
        name: '${azureCognitiveSearch.name}-pl'
        properties: {
          privateLinkServiceId: azureCognitiveSearch.id
          groupIds: [ 'searchService' ]
        }
      }
    ]
  }
}

resource CSEndpointAEntry 'Microsoft.Network/privateDnsZones/A@2020-06-01' = if (memoryStore == 'AzureCognitiveSearch') {
  name: azureCognitiveSearch.name
  parent: privateDnsZones[0]
  properties: {
    ttl: 3600
    aRecords: [
      {
        ipv4Address: CSPrivateEndpoint.properties.customDnsConfigs[0].ipAddresses[0]
      }
    ]
  }
}
*/

/*
resource kvAppGWAccess 'Microsoft.KeyVault/vaults/accessPolicies@2022-07-01' = {
  name: 'add'
  parent: keyvault
  properties: {
    accessPolicies: [
      {
        objectId: umsiAppGW.properties.principalId
        permissions: {
          certificates: [
            'get'
            'list'
          ]
          keys: []
          secrets: []
          storage: []
        }
        tenantId: subscription().tenantId
      }
    ]
  }
}
*/
