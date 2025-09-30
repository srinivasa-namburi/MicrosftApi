// Copyright (c) Microsoft Corporation. All rights reserved.

@description('The location for the Application Gateway resource.')
param location string = resourceGroup().location

@description('The name of the Application Gateway.')
param applicationGatewayName string = 'appgw-${uniqueString(resourceGroup().id)}'

@description('The SKU name for the Application Gateway.')
@allowed(['Basic_v2', 'Standard_v2', 'WAF_v2'])
param skuName string = 'Standard_v2'

@description('The SKU tier for the Application Gateway.')
@allowed(['Basic_v2', 'Standard_v2', 'WAF_v2'])
param skuTier string = 'Standard_v2'

@description('The subnet ID for the Application Gateway.')
param subnetId string

@description('The backend addresses configuration for services.')
param backendAddresses object = {
  api: []
  web: []
  mcp: []
}

@description('The custom domain name (optional).')
param customDomainName string = ''

@description('Whether to enable HTTP to HTTPS redirect.')
param enableHttpRedirect bool = true

@description('The tags to apply to the Application Gateway.')
param tags object = {}

// Public IP for Application Gateway
resource publicIp 'Microsoft.Network/publicIPAddresses@2024-01-01' = {
  name: '${applicationGatewayName}-pip'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Regional'
  }
  zones: ['1', '2', '3']
  properties: {
    publicIPAllocationMethod: 'Static'
    dnsSettings: customDomainName != '' ? {
      domainNameLabel: customDomainName
    } : null
  }
  tags: union(tags, {
    'aspire-resource-name': 'appgateway-pip'
    'aspire-resource-type': 'public-ip'
  })
}

// Application Gateway
resource applicationGateway 'Microsoft.Network/applicationGateways@2024-01-01' = {
  name: applicationGatewayName
  location: location
  zones: ['1', '2', '3']
  properties: {
    sku: {
      name: skuName
      tier: skuTier
      capacity: 2
    }
    
    // Gateway IP Configuration
    gatewayIPConfigurations: [
      {
        name: 'appGatewayIpConfig'
        properties: {
          subnet: {
            id: subnetId
          }
        }
      }
    ]
    
    // Frontend IP Configuration
    frontendIPConfigurations: [
      {
        name: 'appGatewayFrontendIP'
        properties: {
          publicIPAddress: {
            id: publicIp.id
          }
        }
      }
    ]
    
    // Frontend Ports (HTTP and HTTPS)
    frontendPorts: [
      {
        name: 'appGatewayFrontendPort80'
        properties: {
          port: 80
        }
      }
      {
        name: 'appGatewayFrontendPort443'
        properties: {
          port: 443
        }
      }
    ]
    
    // Backend Address Pools
    backendAddressPools: [
      {
        name: 'apiBackendPool'
        properties: {
          backendAddresses: backendAddresses.api
        }
      }
      {
        name: 'webBackendPool'
        properties: {
          backendAddresses: backendAddresses.web
        }
      }
      {
        name: 'mcpBackendPool'
        properties: {
          backendAddresses: backendAddresses.mcp
        }
      }
    ]
    
    // Backend HTTP Settings
    backendHttpSettingsCollection: [
      {
        name: 'apiBackendHttpSettings'
        properties: {
          port: 8080
          protocol: 'Http'
          cookieBasedAffinity: 'Disabled'
          requestTimeout: 20
          probe: {
            id: resourceId('Microsoft.Network/applicationGateways/probes', applicationGatewayName, 'apiHealthProbe')
          }
        }
      }
      {
        name: 'webBackendHttpSettings'
        properties: {
          port: 8080
          protocol: 'Http'
          cookieBasedAffinity: 'Disabled'
          requestTimeout: 20
          probe: {
            id: resourceId('Microsoft.Network/applicationGateways/probes', applicationGatewayName, 'webHealthProbe')
          }
        }
      }
      {
        name: 'mcpBackendHttpSettings'
        properties: {
          port: 8080
          protocol: 'Http'
          cookieBasedAffinity: 'Disabled'
          requestTimeout: 20
          probe: {
            id: resourceId('Microsoft.Network/applicationGateways/probes', applicationGatewayName, 'mcpHealthProbe')
          }
        }
      }
    ]
    
    // Health Probes
    probes: [
      {
        name: 'apiHealthProbe'
        properties: {
          protocol: 'Http'
          host: '127.0.0.1'
          path: '/health'
          interval: 30
          timeout: 30
          unhealthyThreshold: 3
          minServers: 0
          match: {
            statusCodes: ['200-399']
          }
        }
      }
      {
        name: 'webHealthProbe'
        properties: {
          protocol: 'Http'
          host: '127.0.0.1'
          path: '/health'
          interval: 30
          timeout: 30
          unhealthyThreshold: 3
          minServers: 0
          match: {
            statusCodes: ['200-399']
          }
        }
      }
      {
        name: 'mcpHealthProbe'
        properties: {
          protocol: 'Http'
          host: '127.0.0.1'
          path: '/health'
          interval: 30
          timeout: 30
          unhealthyThreshold: 3
          minServers: 0
          match: {
            statusCodes: ['200-399']
          }
        }
      }
    ]
    
    // HTTP Listeners
    httpListeners: [
      {
        name: 'appGatewayHttpListener'
        properties: {
          frontendIPConfiguration: {
            id: resourceId('Microsoft.Network/applicationGateways/frontendIPConfigurations', applicationGatewayName, 'appGatewayFrontendIP')
          }
          frontendPort: {
            id: resourceId('Microsoft.Network/applicationGateways/frontendPorts', applicationGatewayName, 'appGatewayFrontendPort80')
          }
          protocol: 'Http'
        }
      }
    ]
    
    // URL Path Maps for routing
    urlPathMaps: [
      {
        name: 'urlPathMap'
        properties: {
          defaultBackendAddressPool: {
            id: resourceId('Microsoft.Network/applicationGateways/backendAddressPools', applicationGatewayName, 'webBackendPool')
          }
          defaultBackendHttpSettings: {
            id: resourceId('Microsoft.Network/applicationGateways/backendHttpSettingsCollection', applicationGatewayName, 'webBackendHttpSettings')
          }
          pathRules: [
            {
              name: 'apiPathRule'
              properties: {
                paths: ['/api/*']
                backendAddressPool: {
                  id: resourceId('Microsoft.Network/applicationGateways/backendAddressPools', applicationGatewayName, 'apiBackendPool')
                }
                backendHttpSettings: {
                  id: resourceId('Microsoft.Network/applicationGateways/backendHttpSettingsCollection', applicationGatewayName, 'apiBackendHttpSettings')
                }
              }
            }
            {
              name: 'hubsPathRule'
              properties: {
                paths: ['/hubs/*']
                backendAddressPool: {
                  id: resourceId('Microsoft.Network/applicationGateways/backendAddressPools', applicationGatewayName, 'apiBackendPool')
                }
                backendHttpSettings: {
                  id: resourceId('Microsoft.Network/applicationGateways/backendHttpSettingsCollection', applicationGatewayName, 'apiBackendHttpSettings')
                }
              }
            }
            {
              name: 'mcpPathRule'
              properties: {
                paths: ['/mcp/*']
                backendAddressPool: {
                  id: resourceId('Microsoft.Network/applicationGateways/backendAddressPools', applicationGatewayName, 'mcpBackendPool')
                }
                backendHttpSettings: {
                  id: resourceId('Microsoft.Network/applicationGateways/backendHttpSettingsCollection', applicationGatewayName, 'mcpBackendHttpSettings')
                }
              }
            }
          ]
        }
      }
    ]
    
    // Request Routing Rules
    requestRoutingRules: [
      {
        name: 'routingRule'
        properties: {
          priority: 100
          ruleType: 'PathBasedRouting'
          httpListener: {
            id: resourceId('Microsoft.Network/applicationGateways/httpListeners', applicationGatewayName, 'appGatewayHttpListener')
          }
          urlPathMap: {
            id: resourceId('Microsoft.Network/applicationGateways/urlPathMaps', applicationGatewayName, 'urlPathMap')
          }
        }
      }
    ]
  }
  
  tags: union(tags, {
    'aspire-resource-name': 'appgateway'
    'aspire-resource-type': 'application-gateway'
  })
}

// Outputs
@description('The Application Gateway resource ID.')
output applicationGatewayId string = applicationGateway.id

@description('The Application Gateway name.')
output applicationGatewayName string = applicationGateway.name

@description('The public IP address of the Application Gateway.')
output publicIpAddress string = publicIp.properties.ipAddress

@description('The FQDN of the Application Gateway.')
output fqdn string = publicIp.properties.dnsSettings != null ? publicIp.properties.dnsSettings.fqdn : publicIp.properties.ipAddress

@description('The Application Gateway frontend URL.')
output frontendUrl string = 'http://${publicIp.properties.dnsSettings != null ? publicIp.properties.dnsSettings.fqdn : publicIp.properties.ipAddress}'

@description('API endpoint URL through Application Gateway.')
output apiEndpointUrl string = 'http://${publicIp.properties.dnsSettings != null ? publicIp.properties.dnsSettings.fqdn : publicIp.properties.ipAddress}/api'

@description('SignalR endpoint URL through Application Gateway.')
output signalREndpointUrl string = 'http://${publicIp.properties.dnsSettings != null ? publicIp.properties.dnsSettings.fqdn : publicIp.properties.ipAddress}/hubs'

@description('MCP endpoint URL through Application Gateway.')
output mcpEndpointUrl string = 'http://${publicIp.properties.dnsSettings != null ? publicIp.properties.dnsSettings.fqdn : publicIp.properties.ipAddress}/mcp'