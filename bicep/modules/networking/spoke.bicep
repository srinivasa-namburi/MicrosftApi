param location string
param uniqueName string
param vnetObject object
param tags object = {}

resource nsgs 'Microsoft.Network/networkSecurityGroups@2023-04-01' = [for subnet in vnetObject.subnets: if (subnet.properties.?nsgRules != null && subnet.properties.?nsgRules != []) {
  name: 'nsg-${subnet.name}-${uniqueName}'
  location: location
  tags: tags
  properties: {
    securityRules: subnet.properties.nsgRules
  }
}]

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-04-01' = {
  name: 'vnet-${uniqueName}'
  location: location
  properties: {
    addressSpace: vnetObject.addressSpace
    subnets: [for (subnet, index) in vnetObject.subnets: {
      name: subnet.name
      properties: {
        addressPrefix: subnet.properties.addressPrefix
        networkSecurityGroup: subnet.properties.?nsgRules != null && subnet.properties.?nsgRules != [] ? {
          id: nsgs[index].id
        } : null
        serviceEndpoints: subnet.properties.?serviceEndpoints != null ? subnet.properties.?serviceEndpoints : []
        delegations: subnet.properties.?delegations != null ? subnet.properties.?delegations : []
        privateEndpointNetworkPolicies: subnet.properties.privateEndpointNetworkPolicies
        privateLinkServiceNetworkPolicies: subnet.properties.privateLinkServiceNetworkPolicies
      }
    }]
  }
}

output subnetsId array = [for (subnet, index) in vnetObject.subnets: virtualNetwork.properties.subnets[index].id] 
