/*
Copyright (c) Microsoft. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.

Bicep parameters for main.bicep template for deploying Project Vico Azure resources.
*/

using 'main.bicep'

param uniqueName = ''

// param deploymentName = 'vico-deployment'
param webapiASPsku = 'P3V3'
param pluginASPsku = 'EP3'
param aiService = 'AzureOpenAI'

param completionModel = 'gpt-4-32k'
param completionModelVersion = '0613'
param embeddingModel = 'text-embedding-ada-002'
param embeddingModelVersion = '2'
param plannerModel = 'gpt-4-32k'

param aiEndpoint = ''
param aiApiKey = ''

param webApiClientId = ''
param azureAdTenantId = ''

param deployNewAzureOpenAI = true
param deployCosmosDB = true

param memoryStore = 'AzureCognitiveSearch' // CognitiveSearch, Volatile

param location = 'swedencentral'
param azureMapsLocation = 'northeurope'
param documentIntelligenceLocation = 'westeurope'

param tags = {
  project: 'vico'
  environment: 'dev'
  version: '0.0.1'
}

param functionAppNameArray = [
  'DocQnA'
  'GeographicalData'
  // 'Earthquake'
  // 'Weather'
]

/*
param privateDnsZoneNames = [
  'privatelink.search.windows.net' // Azure Cognitive Search
  'privatelink.3.azurestaticapps.net' // Static Web Apps
  'privatelink.documents.azure.com' // Cosmos DB
  'privatelink.azurewebsites.net' // Web Apps | Function Apps
  'privatelink.blob.core.windows.net' // Storage Account Blob - Public Cloud Only
  'privatelink.file.core.windows.net' // Storage Account File - Public Cloud Only
  'privatelink.table.core.windows.net' // Storage Account Table - Public Cloud Only
  'privatelink.queue.core.windows.net' // Storage Account Queue - Public Cloud Only
  'privatelink.vaultcore.azure.net' // Key Vault
]
*/

param appgwMaxCapacity = 10

param vnetObject = {
  addressSpace: {
    addressPrefixes: [
      '10.0.0.0/16'
    ]
  }
  subnets: [
    {
      name: 'ApplicationGatewaySubnet'
      properties: {
        addressPrefix: '10.0.0.0/24'
        nsgRules: []
        /*serviceEndpoints: [
          {
            service: 'Microsoft.Web'
            locations: [
              '*'
            ]
          }
        ]*/
        privateEndpointNetworkPolicies: 'Enabled'
        privateLinkServiceNetworkPolicies: 'Enabled'
      }
    }
    {
      name: 'SemanticKernelSubnet'
      properties: {
        addressPrefix: '10.0.1.0/24'
        nsgRules: []
        /*serviceEndpoints: [
          {
            service: 'Microsoft.Web'
            locations: [
              '*'
            ]
          }
        ]*/
        delegations: [
          {
            name: 'delegation'
            properties: {
              serviceName: 'Microsoft.Web/serverfarms'
            }
          }
        ]
        privateEndpointNetworkPolicies: 'Enabled'
        privateLinkServiceNetworkPolicies: 'Enabled'
      }
    }
    {
      name: 'PluginsSubnet'
      properties: {
        addressPrefix: '10.0.2.0/24'
        serviceEndpoints: []
        nsgRules: []
        delegations: [
          {
            name: 'delegation'
            properties: {
              serviceName: 'Microsoft.Web/serverfarms'
            }
          }
        ]
        privateEndpointNetworkPolicies: 'Enabled'
        privateLinkServiceNetworkPolicies: 'Enabled'
      }
    }
    {
      name: 'WebSubnet'
      properties: {
        addressPrefix: '10.0.3.0/24'
        serviceEndpoints: []
        nsgRules: []
        privateEndpointNetworkPolicies: 'Enabled'
        privateLinkServiceNetworkPolicies: 'Enabled'
      }
    }
    {
      name: 'StorageSubnet'
      properties: {
        addressPrefix: '10.0.4.0/27'
        serviceEndpoints: []
        nsgRules: []
        privateEndpointNetworkPolicies: 'Enabled'
        privateLinkServiceNetworkPolicies: 'Enabled'
      }
    }
    {
      name: 'AISubnet'
      properties: {
        addressPrefix: '10.0.4.32/27'
        serviceEndpoints: []
        nsgRules: []
        privateEndpointNetworkPolicies: 'Enabled'
        privateLinkServiceNetworkPolicies: 'Enabled'
      }
    }
    {
      name: 'ServiceSubnet'
      properties: {
        addressPrefix: '10.0.4.64/27'
        serviceEndpoints: []
        nsgRules: []
        privateEndpointNetworkPolicies: 'Enabled'
        privateLinkServiceNetworkPolicies: 'Enabled'
      }
    }
  ]
}
