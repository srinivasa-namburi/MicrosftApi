param uniqueName string
param location string
param subnetId string
param umsiAppGWId string
param publicIPAddressesId string
param SemanticKernelWebFQDN string
param appgwMaxCapacity int
param tags object = {}

resource applicationGateWay 'Microsoft.Network/applicationGateways@2023-04-01' = {
  name: 'appgw-${uniqueName}'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${umsiAppGWId}': {}
    }
  }
  properties:{
    sku: {
      name: 'WAF_v2'
      tier: 'WAF_v2'
    }
    gatewayIPConfigurations: [
      {
        name: 'appgwIpConfig'
        properties: {
          subnet: {
            id: subnetId
          }
        }
      }
    ]
    frontendIPConfigurations: [
      {
        name: 'appgwPublicFrontendIPv4'
        properties: {
          publicIPAddress: {
            id: publicIPAddressesId
          }
        }
      }
      {
        name: 'appgwPrivateFrontendIPv4'
        properties: {
          subnet: {
            id: subnetId
          }
          privateIPAddress: '10.0.0.4'
          privateIPAllocationMethod: 'Static'
        }
      }
    ]
    frontendPorts: [
      {
        name: 'port_40443'
        properties: {
          port: 40443
        }
      }
    ]
    backendAddressPools: [
      {
        name: 'webapiBackendPool'
        properties: {
          backendAddresses: [
            {
              fqdn: SemanticKernelWebFQDN
            }
          ]
        }
      }
    ]
    backendHttpSettingsCollection: [
      {
        name: 'webapiBackendHttpSettings'
        properties: {
          port: 40443
          protocol: 'Http'
          cookieBasedAffinity: 'Enabled'
          requestTimeout: 20
          affinityCookieName: 'ApplicationGatewayAffinity'
        }
      }
    ]
    httpListeners: [
      {
        name: 'webapiHttpListener'
        properties: {
          frontendIPConfiguration: {
            id: resourceId('Microsoft.Network/applicationGateways/frontendIPConfigurations', 'appgw-${uniqueName}', 'appgwPublicFrontendIPv4')
          }
          frontendPort: {
            id: resourceId('Microsoft.Network/applicationGateways/frontendPorts', 'appgw-${uniqueName}', 'port_40443')
          }
          protocol: 'Http'
          requireServerNameIndication: false
          hostName: SemanticKernelWebFQDN

        }
      }
    ]
    requestRoutingRules: [
      {
        name: 'webapiRoutingRule-40443'
        properties: {
          backendAddressPool: {
            id: resourceId('Microsoft.Network/applicationGateways/backendAddressPools', 'appgw-${uniqueName}', 'webapiBackendPool')
          }
          backendHttpSettings: {
            id: resourceId('Microsoft.Network/applicationGateways/backendHttpSettingsCollection', 'appgw-${uniqueName}', 'webapiBackendHttpSettings')
          }
          httpListener: {
            id: resourceId('Microsoft.Network/applicationGateways/httpListeners', 'appgw-${uniqueName}', 'webapiHttpListener')
          }
          priority: 100
          ruleType: 'Basic'
        }
      }
    ]
    enableHttp2: true
    sslCertificates: [
    ]
    probes: [] // TODO add probe webapi
    autoscaleConfiguration: {
      minCapacity: 0
      maxCapacity: appgwMaxCapacity
    }
    firewallPolicy: {
      id: appgwWafPolicy.id
    }
  }
}

resource appgwWafPolicy 'Microsoft.Network/ApplicationGatewayWebApplicationFirewallPolicies@2023-04-01' = {
  name: 'wafpolicy-${uniqueName}'
  location: location
  tags: tags
  properties: {
    policySettings: {
      state: 'Enabled'
      mode: 'Prevention'
      fileUploadLimitInMb: 100
      requestBodyCheck: true
      maxRequestBodySizeInKb: 128
    }
    managedRules: {
      exclusions: []
      managedRuleSets: [
        {
          ruleSetType: 'OWASP'
          ruleSetVersion: '3.2'
          ruleGroupOverrides: null
        }
        {
          ruleSetType: 'Microsoft_BotManagerRuleSet'
          ruleSetVersion: '0.1'
          ruleGroupOverrides: null
        }
      ]
    }
  }
}
