# Application Gateway Bicep Templates

This directory contains custom Bicep templates for Application Gateway deployment with .NET Aspire 9.4.

## Templates

### `application-gateway.bicep`
Basic Application Gateway template for HTTP-only deployments.

**Use Cases:**
- Development and testing environments
- Public deployments without SSL requirements
- Magic URL scenarios with built-in Azure domains

**Parameters:**
- `applicationGatewayName`: Name of the Application Gateway
- `skuName`: SKU (Basic_v2, Standard_v2, WAF_v2) 
- `subnetId`: Application Gateway subnet ID
- `customDomainName`: Optional custom domain
- `backendAddresses`: Backend pool configuration (JSON)

### `application-gateway-ssl.bicep`
Advanced Application Gateway template with SSL/TLS support.

**Use Cases:**
- Production environments
- Custom domain deployments with SSL certificates
- Private/hybrid deployments with enhanced security

**Additional Parameters:**
- `certificateData`: Base64 encoded certificate data
- `certificatePassword`: Certificate password
- `keyVaultId`: Key Vault resource ID for certificate storage
- `keyVaultCertificateName`: Certificate name in Key Vault
- `enableHttpRedirect`: HTTP to HTTPS redirect

## Integration with .NET Aspire

These templates are integrated with the AppHost using Aspire 9.4's bicep injection capability:

```csharp
// In AzureDependencies.cs
var appGateway = builder.AddBicepTemplate("appgateway", bicepTemplatePath);
```

## Application Gateway Support (Pre-release - Not Ready for Production)

> ⚠️ **WARNING**: Application Gateway integration is currently in pre-release and disabled by default. It is not ready for production use.

### Enabling Application Gateway (Experimental)

To enable the Application Gateway for testing purposes:

1. Set the environment variable:
   ```bash
   export ENABLE_APPLICATION_GATEWAY=true
   ```

2. For public deployments, no additional configuration is needed (uses auto-created subnet).

3. For private/hybrid deployments, provide subnet configuration:
   ```bash
   export APPLICATION_GATEWAY_SUBNET_ID="/subscriptions/.../subnets/appgw-subnet"
   ```

### Known Issues
- Bicep generation may produce incorrect syntax for complex parameters
- Resource deployment ordering issues with role assignments
- Subnet configuration required even for public deployments (being addressed)

## Configuration Variables

Configure Application Gateway through GitHub variables or environment settings:

| Variable | Default | Description |
|----------|---------|-------------|
| `ENABLE_APPLICATION_GATEWAY` | `false` | Enable/disable Application Gateway (Pre-release) |
| `APPLICATION_GATEWAY_DOMAIN` | *(empty)* | Custom domain name |
| `APPLICATION_GATEWAY_SSL_ENABLED` | `false` | Enable SSL support |
| `APPLICATION_GATEWAY_SUBNET_ID` | *(required)* | Application Gateway subnet |
| `APPLICATION_GATEWAY_KEYVAULT_ID` | *(empty)* | Key Vault for SSL certificates |
| `APPLICATION_GATEWAY_CERTIFICATE_NAME` | *(empty)* | Certificate name |

## Routing Rules

The Application Gateway provides unified ingress for all services:

- **`/`** → Web Frontend (default route)
- **`/api/*`** → API Service
- **`/hubs/*`** → SignalR (hosted on API when Azure SignalR disabled)  
- **`/mcp/*`** → MCP Server

## Health Probes

Each backend pool includes health probes:
- **Path:** `/health`
- **Interval:** 30 seconds
- **Timeout:** 30 seconds
- **Unhealthy Threshold:** 3 failures
- **Expected Status:** 200-399

## Deployment

The bicep templates are automatically deployed by the AppHost when:
1. Not running in development mode
2. `ENABLE_APPLICATION_GATEWAY=true`
3. Required subnet configuration is provided

## SSL Certificate Management

### Option 1: Azure Key Vault (Recommended)
```yaml
APPLICATION_GATEWAY_SSL_ENABLED: true
APPLICATION_GATEWAY_KEYVAULT_ID: /subscriptions/.../keyvaults/myvault
APPLICATION_GATEWAY_CERTIFICATE_NAME: mycert
```

### Option 2: Certificate Data (Testing)
```yaml  
APPLICATION_GATEWAY_SSL_ENABLED: true
# Certificate data and password provided as secrets
```

## Magic URLs

Application Gateway provides automatic "magic URLs" that work without custom domains:

- **HTTP:** `http://appgw-{uniqueId}.{region}.cloudapp.azure.com`
- **HTTPS:** `https://appgw-{uniqueId}.{region}.cloudapp.azure.com` (with SSL)

## Private Networking

For private/hybrid deployments, ensure:
- Application Gateway subnet is properly configured
- Network security groups allow required traffic
- Private DNS zones are configured for backend services

## Troubleshooting

### Common Issues

1. **Backend Unhealthy**
   - Verify health probe endpoints return 200-399 status codes
   - Check network connectivity between Application Gateway and backends
   - Validate Container App health endpoints

2. **SSL Certificate Issues**
   - Ensure Key Vault permissions are correctly configured
   - Verify certificate is valid and not expired
   - Check certificate format (PFX required)

3. **Routing Problems**
   - Validate path-based routing rules
   - Check backend pool configuration
   - Verify listener configuration

### Monitoring

Monitor Application Gateway through:
- Azure Portal Application Gateway insights
- Azure Monitor logs and metrics
- Health probe status in backend settings

## Updates and Maintenance

- Templates are version controlled with the AppHost
- Updates deploy automatically with Aspire publish process
- No manual bicep maintenance required