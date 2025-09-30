// Copyright (c) Microsoft Corporation. All rights reserved.

@description('The location for the Application Gateway resource.')
param location string = resourceGroup().location

@description('The name of the Application Gateway.')
param applicationGatewayName string = 'appgw-${uniqueString(resourceGroup().id)}'

@description('The SKU name for the Application Gateway.')
@allowed(['Standard_v2', 'WAF_v2'])
param skuName string = 'Standard_v2'

@description('The SKU tier for the Application Gateway.')
@allowed(['Standard_v2', 'WAF_v2'])
param skuTier string = 'Standard_v2'

@description('The subnet ID for the Application Gateway.')
param subnetId string

@description('The backend addresses configuration for services.')
param backendAddresses object = {
  api: []
  web: []
  mcp: []
}

@description('The custom domain name.')
param customDomainName string

@description('The certificate data for SSL (base64 encoded).')
@secure()
param certificateData string = ''

@description('The certificate password for SSL.')
@secure()
param certificatePassword string = ''

@description('The Key Vault ID where SSL certificate is stored (alternative to certificate data).')
param keyVaultId string = ''

@description('The name of the certificate in Key Vault.')
param keyVaultCertificateName string = ''

@description('Whether to enable HTTP to HTTPS redirect.')
param enableHttpRedirect bool = true

@description('The tags to apply to the Application Gateway.')
param tags object = {}

// Managed Identity for Key Vault access (if using Key Vault certificates)
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = if (keyVaultId != '' && keyVaultCertificateName != '') {
  name: '${applicationGatewayName}-identity'
  location: location
  tags: tags
}

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
    dnsSettings: {
      domainNameLabel: customDomainName
    }
  }
  tags: union(tags, {
    'aspire-resource-name': 'appgateway-pip'
    'aspire-resource-type': 'public-ip'
  })
}

// Application Gateway with SSL support
resource applicationGateway 'Microsoft.Network/applicationGateways@2024-01-01' = {
  name: applicationGatewayName
  location: location
  zones: ['1', '2', '3']
  identity: keyVaultId != '' && keyVaultCertificateName != '' ? {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  } : null
  
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
    
    // SSL Certificates
    sslCertificates: keyVaultId != '' && keyVaultCertificateName != '' ? [
      {
        name: 'appGatewaySslCert'
        properties: {
          keyVaultSecretId: '${keyVaultId}/secrets/${keyVaultCertificateName}'
        }
      }
    ] : certificateData != '' ? [
      {
        name: 'appGatewaySslCert'
        properties: {
          data: certificateData
          password: certificatePassword
        }
      }
    ] : []
    
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
          requestTimeout: 30
          connectionDraining: {
            enabled: true
            drainTimeoutInSec: 60
          }
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
          requestTimeout: 30
          connectionDraining: {
            enabled: true
            drainTimeoutInSec: 60
          }
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
          requestTimeout: 30
          connectionDraining: {
            enabled: true
            drainTimeoutInSec: 60
          }
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
    
    // HTTP and HTTPS Listeners
    httpListeners: concat(
      [
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
            hostName: customDomainName
          }
        }
      ],
      (keyVaultId != '' && keyVaultCertificateName != '') || certificateData != '' ? [
        {
          name: 'appGatewayHttpsListener'
          properties: {
            frontendIPConfiguration: {
              id: resourceId('Microsoft.Network/applicationGateways/frontendIPConfigurations', applicationGatewayName, 'appGatewayFrontendIP')
            }
            frontendPort: {
              id: resourceId('Microsoft.Network/applicationGateways/frontendPorts', applicationGatewayName, 'appGatewayFrontendPort443')
            }
            protocol: 'Https'
            hostName: customDomainName
            sslCertificate: {
              id: resourceId('Microsoft.Network/applicationGateways/sslCertificates', applicationGatewayName, 'appGatewaySslCert')
            }
          }
        }
      ] : []
    )
    
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
    requestRoutingRules: concat(
      enableHttpRedirect && ((keyVaultId != '' && keyVaultCertificateName != '') || certificateData != '') ? [
        {
          name: 'httpRedirectRule'
          properties: {
            priority: 100
            ruleType: 'Basic'
            httpListener: {
              id: resourceId('Microsoft.Network/applicationGateways/httpListeners', applicationGatewayName, 'appGatewayHttpListener')
            }
            redirectConfiguration: {
              id: resourceId('Microsoft.Network/applicationGateways/redirectConfigurations', applicationGatewayName, 'httpRedirectConfig')
            }
          }
        }
      ] : [
        {
          name: 'httpRoutingRule'
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
      ],
      (keyVaultId != '' && keyVaultCertificateName != '') || certificateData != '' ? [
        {
          name: 'httpsRoutingRule'
          properties: {
            priority: 200
            ruleType: 'PathBasedRouting'
            httpListener: {
              id: resourceId('Microsoft.Network/applicationGateways/httpListeners', applicationGatewayName, 'appGatewayHttpsListener')
            }
            urlPathMap: {
              id: resourceId('Microsoft.Network/applicationGateways/urlPathMaps', applicationGatewayName, 'urlPathMap')
            }
          }
        }
      ] : []
    )
    
    // Redirect Configuration (HTTP to HTTPS)
    redirectConfigurations: enableHttpRedirect && ((keyVaultId != '' && keyVaultCertificateName != '') || certificateData != '') ? [
      {
        name: 'httpRedirectConfig'
        properties: {
          redirectType: 'Permanent'
          targetListener: {
            id: resourceId('Microsoft.Network/applicationGateways/httpListeners', applicationGatewayName, 'appGatewayHttpsListener')
          }
          includePath: true
          includeQueryString: true
        }
      }
    ] : []
  }
  
  tags: union(tags, {
    'aspire-resource-name': 'appgateway'
    'aspire-resource-type': 'application-gateway'
    'ssl-enabled': string((keyVaultId != '' && keyVaultCertificateName != '') || certificateData != '')
  })
}

// Key Vault access policy (if using Key Vault certificates)
resource keyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2023-07-01' = if (keyVaultId != '' && keyVaultCertificateName != '') {
  name: '${last(split(keyVaultId, '/'))}/add'
  properties: {
    accessPolicies: [
      {
        tenantId: managedIdentity.properties.tenantId
        objectId: managedIdentity.properties.principalId
        permissions: {
          secrets: ['get']
          certificates: ['get']
        }
      }
    ]
  }
}

// Outputs
@description('The Application Gateway resource ID.')
output applicationGatewayId string = applicationGateway.id

@description('The Application Gateway name.')
output applicationGatewayName string = applicationGateway.name

@description('The public IP address of the Application Gateway.')
output publicIpAddress string = publicIp.properties.ipAddress

@description('The FQDN of the Application Gateway.')
output fqdn string = publicIp.properties.dnsSettings.fqdn

@description('The Application Gateway frontend URL.')
output frontendUrl string = ((keyVaultId != '' && keyVaultCertificateName != '') || certificateData != '') ? 'https://${publicIp.properties.dnsSettings.fqdn}' : 'http://${publicIp.properties.dnsSettings.fqdn}'

@description('API endpoint URL through Application Gateway.')
output apiEndpointUrl string = ((keyVaultId != '' && keyVaultCertificateName != '') || certificateData != '') ? 'https://${publicIp.properties.dnsSettings.fqdn}/api' : 'http://${publicIp.properties.dnsSettings.fqdn}/api'

@description('SignalR endpoint URL through Application Gateway.')
output signalREndpointUrl string = ((keyVaultId != '' && keyVaultCertificateName != '') || certificateData != '') ? 'https://${publicIp.properties.dnsSettings.fqdn}/hubs' : 'http://${publicIp.properties.dnsSettings.fqdn}/hubs'

@description('MCP endpoint URL through Application Gateway.')
output mcpEndpointUrl string = ((keyVaultId != '' && keyVaultCertificateName != '') || certificateData != '') ? 'https://${publicIp.properties.dnsSettings.fqdn}/mcp' : 'http://${publicIp.properties.dnsSettings.fqdn}/mcp'

@description('Whether SSL is enabled.')
output sslEnabled bool = (keyVaultId != '' && keyVaultCertificateName != '') || certificateData != ''