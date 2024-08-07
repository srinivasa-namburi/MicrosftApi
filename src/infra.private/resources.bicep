@description('The location used for all deployed resources')
param location string = resourceGroup().location
@description('Id of the user or app to assign application roles')
param principalId string = ''

@description('Tags that will be applied to all resources')
param tags object = {}

@description('The subnet of the Container Apps environment must be delegated to the service Microsoft.App/environments')
param containerAppEnvSubnet string

@description('The subnet of the private endpoints environment - must have aligned private DNS zones / custom DNS for resolution')
param peSubnet string

var resourceToken = uniqueString(resourceGroup().id)

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'mi-${resourceToken}'
  location: location
  tags: tags
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: replace('acr-${resourceToken}', '-', '')
  location: location
  sku: {
    name: 'Premium' // Premium SKU is required for private endpoint
  }
  properties: {
    adminUserEnabled: true
    publicNetworkAccess: 'Enabled' // We cannot move to private only due to: need Github self-hosted runners, to evaluate with customers
  }
  tags: tags
}

resource peContainerRegistry 'Microsoft.Network/privateEndpoints@2023-11-01' = if (peSubnet != '') {
  name: '${containerRegistry.name}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${containerRegistry.name}-pl'
        properties: {
          privateLinkServiceId: containerRegistry.id
          groupIds: ['registry']
        }
      }
    ]
  }
}

resource caeMiRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, managedIdentity.id, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d'))
  scope: containerRegistry
  properties: {
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId:  subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

// Kept public for now due to complexity of AMPLS - to review with customer directly on appropriate way of implementation
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'law-${resourceToken}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
  }
  tags: tags
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-02-02-preview' = {
  name: 'cae-${resourceToken}'
  location: location
  properties: {
    workloadProfiles: [{
        maximumCount: 10
        minimumCount: 3
        name: 'dedicated'
        workloadProfileType: 'D4'
      }]
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
    // The subnet of the environment must be delegated to the service 'Microsoft.App/environments'.
    vnetConfiguration:{
        infrastructureSubnetId: containerAppEnvSubnet 
        internal: true
    }
  }
  tags: tags

  resource aspireDashboard 'dotNetComponents' = {
    name: 'aspire-dashboard'
    properties: {
      componentType: 'AspireDashboard'
    }
  }

}

resource explicitContributorUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerAppEnvironment.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c'))
  scope: containerAppEnvironment
  properties: {
    principalId: principalId
    roleDefinitionId:  subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')
  }
}

resource kvb6088994 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: replace('kvb6088994-${resourceToken}', '-', '')
  location: location
  properties: {
    sku: {
      name: 'standard'
      family: 'A'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: 'true'
  }
}

resource pekvb6088994 'Microsoft.Network/privateEndpoints@2023-11-01' = if (peSubnet != '') {
  name: '${kvb6088994.name}-pl'
  tags: tags
  location: location
  properties: {
    subnet: {
      id: peSubnet
    }
    privateLinkServiceConnections: [
      {
        name: '${kvb6088994.name}-pl'
        properties: {
          privateLinkServiceId: kvb6088994.id
          groupIds: ['vault']
        }
      }
    ]
  }
}

resource kvb6088994RoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvb6088994.id, managedIdentity.id, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '00482a5a-887f-4fb3-b363-3b7fe8e74483'))
  scope: kvb6088994
  properties: {
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId:  subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '00482a5a-887f-4fb3-b363-3b7fe8e74483')
  }
}

resource kvb6088994UserReadRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvb6088994.id, principalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6'))
  scope: kvb6088994
  properties: {
    principalId: principalId
    roleDefinitionId:  subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
  }
}

output MANAGED_IDENTITY_CLIENT_ID string = managedIdentity.properties.clientId
output MANAGED_IDENTITY_NAME string = managedIdentity.name
output MANAGED_IDENTITY_PRINCIPAL_ID string = managedIdentity.properties.principalId
output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = logAnalyticsWorkspace.name
output AZURE_LOG_ANALYTICS_WORKSPACE_ID string = logAnalyticsWorkspace.id
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.properties.loginServer
output AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = managedIdentity.id
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = containerAppEnvironment.id
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = containerAppEnvironment.properties.defaultDomain
output SERVICE_BINDING_KVB6088994_ENDPOINT string = kvb6088994.properties.vaultUri
output SERVICE_BINDING_KVB6088994_NAME string = kvb6088994.name

// Custom
output KV_NAME string = kvb6088994.name
output KV_PE_IP string = pekvb6088994.properties.customDnsConfigs[0].ipAddresses[0]
