# GitHub Actions Modern Deployment Guide

This guide provides **complete, self-contained instructions** for deploying Microsoft Greenlight using the modern Kubernetes-first approach with .NET Aspire 9.4.x and GitHub Actions workflows.

> **Recommended**: This is the default deployment method for most customers who receive source code access via GitHub.

## Overview

The modern GitHub deployment architecture uses:
- **.NET Aspire AppHost** - Single source of truth for service composition and Azure resource provisioning
- **Kubernetes/AKS** - Container orchestration with Helm charts generated from AppHost
- **Azure PaaS Services** - SQL, Storage, Redis, SignalR, Event Hubs, AI Search via Azure Bicep from AppHost
- **GitHub Actions** - Modern workflow with jobs: Aspire Publish → Azure + Helm Deploy
- **GitHub Environments** - Environment-specific configuration and approval gates

## Prerequisites

### 1. Azure Resources
Create these resources in your target subscription:

#### Required Services:
- **AKS Cluster** - Target Kubernetes cluster with appropriate node pool sizing

#### Recommended Services:
- **Azure OpenAI** - GPT-4o or GPT-4-128K deployment named accordingly, text-embedding-ada-002 v2
  - Note: The system can start without OpenAI configured. AI features will be unavailable until configured.
  - Can be configured post-deployment via the Configuration UI at `/admin/configuration?section=secrets`
- **Azure Maps** - For geospatial functionality
  - Note: Can be configured post-deployment via the Configuration UI at `/admin/configuration?section=secrets`
  - The application will start without Azure Maps features until configured

#### Optional Services (for advanced scenarios):
- **VNET + Subnets** - For private networking mode
- **Private DNS Zones** - Required for private mode PostgreSQL: `privatelink.postgres.database.azure.com`
- **Azure AI Search** - Automatically created during deployment if OpenAI is configured
  - Note: Production always uses Azure AI Search, even if you use PostgreSQL locally

### 2. Provision AKS Cluster

We provide scripts to automatically provision an AKS cluster with the correct configuration for your deployment model.

#### PowerShell (Windows) - Supports All Deployment Models

```powershell
# Public cluster (default - recommended for initial setup)
.\build\scripts\provision-aks-cluster.ps1 `
    -ResourceGroup rg-greenlight-dev `
    -Location eastus `
    -ClusterName aks-greenlight-dev

# Private cluster (API not accessible from internet)
.\build\scripts\provision-aks-cluster.ps1 `
    -ResourceGroup rg-greenlight-dev `
    -Location eastus `
    -ClusterName aks-greenlight-dev `
    -DeploymentModel private `
    -VnetResourceGroup rg-network `
    -VnetName vnet-greenlight `
    -SubnetName snet-aks

# Hybrid cluster (in VNET but publicly accessible API)
.\build\scripts\provision-aks-cluster.ps1 `
    -ResourceGroup rg-greenlight-dev `
    -Location eastus `
    -ClusterName aks-greenlight-dev `
    -DeploymentModel hybrid `
    -VnetResourceGroup rg-network `
    -VnetName vnet-greenlight `
    -SubnetName snet-aks `
    -NodeCount 5 `
    -NodeSize Standard_DS4_v2
```

#### Bash (Linux/macOS) - Supports All Deployment Models

```bash
# Public cluster (default)
build/scripts/provision-aks-cluster.sh rg-greenlight-dev eastus aks-greenlight-dev

# Customize node count and size
build/scripts/provision-aks-cluster.sh rg-greenlight-dev eastus aks-greenlight-dev \
    --node-count 5 \
    --node-size Standard_D4s_v6

# Private cluster (API not accessible from internet)
build/scripts/provision-aks-cluster.sh rg-greenlight-dev eastus aks-greenlight-dev \
    --deployment-model private \
    --vnet-resource-group rg-network \
    --vnet-name vnet-greenlight \
    --subnet-name snet-aks

# Hybrid cluster (in VNET but publicly accessible API)
build/scripts/provision-aks-cluster.sh rg-greenlight-dev eastus aks-greenlight-dev \
    --deployment-model hybrid \
    --vnet-resource-group rg-network \
    --vnet-name vnet-greenlight \
    --subnet-name snet-aks
```

**⚠️ IMPORTANT: Deployment Model Compatibility**

| Deployment Model | AKS Configuration | Requirements |
|-----------------|-------------------|--------------|
| **public** | Public cluster, Azure CNI | None |
| **private** | Private cluster, VNET integrated | Pre-existing VNET/Subnet, GitHub self-hosted runners |
| **hybrid** | VNET integrated, public API | Pre-existing VNET/Subnet, Application Gateway for ingress |

**What the scripts do:**
- Create resource group if it doesn't exist
- Provision AKS cluster with managed identity
- Enable monitoring, autoscaling, and availability zones
- Configure networking based on deployment model
- Install NGINX Ingress Controller (public/hybrid only)
- Create standard namespaces (greenlight-dev, greenlight-staging, greenlight-prod)
- Retrieve and configure kubeconfig locally (public/hybrid only)

**Private/Hybrid Networking Requirements:**

For `DEPLOYMENT_MODEL=private` or `DEPLOYMENT_MODEL=hybrid`, you must pre-create:

1. **VNET with dedicated subnets:**
   - **AKS subnet** (`snet-aks`): /20 or larger for cluster nodes
   - **Private endpoints subnet** (`snet-pe`): /24 for PaaS service private endpoints
   - **PostgreSQL subnet** (`snet-postgres`): /24 with delegation to `Microsoft.DBforPostgreSQL/flexibleServers` (if using PostgreSQL)
   - **Application Gateway subnet** (`snet-appgw`): /24 for Application Gateway (hybrid mode only)

2. **Private DNS Zones:**
   - `privatelink.database.windows.net` - Azure SQL Database
   - `privatelink.redis.cache.windows.net` - Azure Redis Cache
   - `privatelink.blob.core.windows.net` - Azure Storage
   - `privatelink.search.windows.net` - Azure AI Search
   - `privatelink.service.signalr.net` - Azure SignalR Service
   - `privatelink.postgres.database.azure.com` - PostgreSQL Flexible Server

   **Example subnet creation:**
   ```bash
   # Create VNET
   az network vnet create --name vnet-greenlight --resource-group rg-network --address-prefix 10.0.0.0/16

   # Create subnets
   az network vnet subnet create --name snet-aks --vnet-name vnet-greenlight --resource-group rg-network --address-prefix 10.0.0.0/20
   az network vnet subnet create --name snet-pe --vnet-name vnet-greenlight --resource-group rg-network --address-prefix 10.0.16.0/24

   # PostgreSQL subnet with delegation (if needed)
   az network vnet subnet create --name snet-postgres --vnet-name vnet-greenlight --resource-group rg-network \
       --address-prefix 10.0.17.0/24 --delegations Microsoft.DBforPostgreSQL/flexibleServers
   ```

3. **GitHub Configuration:**
   - **Private mode**: Requires self-hosted runners within the VNET
   - **Hybrid mode**: Standard GitHub-hosted runners work

### 3. Service Principal Setup

**Prerequisites:**
- **Azure CLI installed and authenticated** - Run `az login` with an account that has:
  - **Application Administrator** or **Cloud Application Administrator** role in Azure AD
  - **Owner** or **Contributor** role on the target subscription/resource group
- **Appropriate permissions** - Either (1) Cloud Application Administrator or (2) Application Developer permission on the tenant

Create a deployment service principal with Owner permissions:

```bash
# Verify you're logged in and have correct permissions
az account show
az ad signed-in-user show --query userPrincipalName

# For subscription-level access (recommended)
az ad sp create-for-rbac \
    --name "sp-ms-industrypermitting-deploy" \
    --scopes "/subscriptions/<subscriptionId>" \
    --role owner

# For resource group-level access (if RG pre-exists)
az ad sp create-for-rbac \
    --name "sp-ms-industrypermitting-deploy" \
    --scopes "/subscriptions/<subscriptionId>/resourceGroups/<resourceGroupName>" \
    --role owner
```

**Save the output** - You'll need the JSON response for the `AZURE_CREDENTIALS` secret later.

### 4. Application Service Principal

**Prerequisites:**
- **PowerShell 5.1+ or PowerShell Core 7+** - Check with `$PSVersionTable.PSVersion`
- **Azure PowerShell module** - Install with `Install-Module -Name Az -Force -AllowClobber`
- **Authenticated to Azure** - Run `Connect-AzAccount` if not already authenticated

Run the application setup script to create the Entra ID app registration:

```powershell
# Verify prerequisites
$PSVersionTable.PSVersion
Get-Module -ListAvailable Az
Get-AzContext  # Should show your authenticated context

# Run the script from repository root
.\build\scripts\sp-create.ps1
```

**Save the output** - The script outputs credentials needed for the `PVICO_ENTRA_CREDENTIALS` secret.

## GitHub Environment Setup

### 1. Create GitHub Environment

In your GitHub repository:
1. Go to Settings → Environments
2. Click "New environment"
3. Create environments matching your deployment stages (e.g., `dev`, `staging`, `prod`)
4. Configure protection rules as needed (e.g., required reviewers for production)

### 2. Create Environment Configuration

Create environment-specific YAML files based on the sample:

```bash
cp build/environment-variables-github-sample.yml build/environment-variables-github-dev.yml
```

Edit your environment file with actual values:

```yaml
environment:
  name: dev  # or prod, staging, etc.
variables:
  AZURE_SUBSCRIPTION_ID: "your-subscription-id"
  AZURE_RESOURCE_GROUP: "rg-greenlight-dev"
  AZURE_LOCATION: "eastus"  # or usgovvirginia for US Gov
  AKS_NAMESPACE: "greenlight-dev"
  HELM_RELEASE: "greenlight"
secrets:
  AZURE_CREDENTIALS: "{ \"clientId\": \"...\", \"clientSecret\": \"...\", \"subscriptionId\": \"...\", \"tenantId\": \"...\" }"
```

### 2. Bootstrap GitHub Environment

#### Prerequisites for Bootstrap Script
Before running the bootstrap script, ensure you have:

**Required Tools:**
- **GitHub CLI** - `gh --version` should show 2.20.0 or later
  - **Installation**: Download from [GitHub CLI releases](https://github.com/cli/cli/releases) or:
    - **Windows**: `winget install GitHub.cli` or `choco install gh`
    - **macOS**: `brew install gh`
    - **Linux**: Follow [GitHub CLI installation guide](https://github.com/cli/cli/blob/trunk/docs/install_linux.md)
- **jq** - JSON processor for parsing configuration files
  - **Windows**: Download from [jq releases](https://github.com/stedolan/jq/releases) or use `choco install jq`
  - **macOS**: `brew install jq`
  - **Linux**: `sudo apt-get install jq` or `yum install jq`

**Authentication Requirements:**
```bash
# 1. Authenticate with GitHub CLI
gh auth login
# Choose: GitHub.com → HTTPS → Authenticate via web browser OR personal access token

# 2. Verify authentication and repository access
gh auth status
gh repo view your-org/your-repo

# 3. Test repository permissions
gh api repos/your-org/your-repo
```

**GitHub Personal Access Token (PAT) Requirements:**
If using token authentication, your GitHub PAT must have these permissions:
- **Repository access**: Read and write to target repository
- **Administration**: Repository (for environment management)
- **Actions**: Write (for secrets and variables)
- **Metadata**: Read (for repository information)

**GitHub App Alternative:**
For organizations, consider using GitHub Apps with these permissions:
- **Repository permissions**:
  - Actions: Write
  - Administration: Write
  - Contents: Read
  - Environments: Write
  - Metadata: Read
  - Secrets: Write
  - Variables: Write

#### Running the Bootstrap Script

Once prerequisites are met, run the bootstrap script:

```bash
build/scripts/build-modern-bootstrap-github.sh \
    build/environment-variables-github-dev.yml \
    your-org/your-repo
```

**What the script does:**
1. **Validates authentication** - Checks GitHub CLI access and repository permissions
2. **Parses configuration** - Reads environment variables from YAML file
3. **Creates GitHub Environment** - Sets up deployment protection rules
4. **Configures variables** - Sets environment-specific variables
5. **Sets secrets** - Configures encrypted secrets for deployment

**Expected output:**
```
✓ GitHub CLI authenticated
✓ Repository 'your-org/your-repo' accessible
✓ Environment 'dev' created
✓ Variables configured (4 variables)
✓ Secrets configured (1 secret)
✓ Environment protection rules applied
Bootstrap completed successfully!
```

**Troubleshooting:**
- **"gh: command not found"** - Install GitHub CLI from [official documentation](https://cli.github.com/manual/installation)
- **"jq: command not found"** - Install jq JSON processor for your platform
- **"Not authenticated"** - Run `gh auth login` and follow authentication flow
- **"Repository not found"** - Verify repository name format: `owner/repository`
- **"Insufficient permissions"** - Check PAT permissions or organization settings
- **"Environment already exists"** - Script will update existing environment configuration

### 3. Manual Environment Configuration

Alternatively, configure environments manually in GitHub:

1. Navigate to **Settings** → **Environments** in your GitHub repository
2. Click **New environment** and name it (e.g., "dev")
3. Configure **Protection rules** if desired (required reviewers, deployment branches)
4. Add **Environment variables**:
   - `AZURE_RESOURCE_GROUP`
   - `AZURE_LOCATION`
   - `AKS_NAMESPACE`
   - `HELM_RELEASE`

5. Add **Environment secrets**:
   - `AZURE_CREDENTIALS`

### 4. Repository Secrets Setup

In **Settings** → **Secrets and variables** → **Actions**, add these repository-level secrets:

#### Required Secrets:
- **PVICO_ENTRA_CREDENTIALS** - JSON output from sp-create.ps1 script (REQUIRED for authentication)
  ```json
  {"TenantId": "87654321-4321-4321-4321-210987654321", "ClientId": "12345678-1234-1234-1234-123456789012", "Scopes": "https://graph.microsoft.com/.default", "ClientSecret": "your-app-secret-value"}
  ```
  **⚠️ CRITICAL**: This secret is REQUIRED for the application to function. Without it, users cannot authenticate.

#### Optional Secrets (Can be configured post-deployment):
- **PVICO_AZUREMAPS_KEY** - Azure Maps API key (optional)
  ```
  your-azure-maps-key-here
  ```
  **Note**: Can be left empty initially and configured later via Configuration UI at `/admin/configuration?section=secrets`.
  The application will start without Azure Maps features until configured.

- **PVICO_OPENAI_CONNECTIONSTRING** - Connection string for Azure OpenAI (optional)
  ```
  Endpoint=https://your-openai-resource.openai.azure.com/;Key=your-openai-key-here
  ```
  **Note**: Can be left empty initially and configured later via Configuration UI at `/admin/configuration?section=secrets`.
  The application will start without AI features until configured.

#### Repository Variables:
- **PVICO_OPENAI_RESOURCEGROUP** - Resource group containing Azure OpenAI
- **MEMORY_BACKEND** - `aisearch` (default) or `postgres`
- **DEPLOYMENT_MODEL** - `public`, `private`, or `hybrid`

For private/hybrid networking mode, also add:
- **POSTGRES_DNSZONE_RESOURCEID** - Resource ID of private DNS zone for PostgreSQL
- **AZURE_SUBNET_PE** - Subnet resource ID for private endpoints
- **AZURE_SUBNET_POSTGRES** - Subnet resource ID for PostgreSQL (if using postgres backend)

For custom domain support, add:
- **HOSTNAME_OVERRIDE** - JSON configuration for custom domains and ingress settings
  ```json
  {"WebApplicationUrl": "https://myapp.example.com", "ApiBaseUrl": "https://api.myapp.example.com"}
  ```

For service toggles, add:
- **ENABLE_AZURE_SIGNALR** - `true` (default) or `false` to disable SignalR service

For Kubernetes resource sizing, add:
- **KUBERNETES_RESOURCES_CONFIG** - JSON configuration for pod resource requests/limits
  ```json
  {"api-main": {"resources": {"requests": {"memory": "512Mi", "cpu": "500m"}, "limits": {"memory": "1Gi", "cpu": "1"}}}, "web-main": {"resources": {"requests": {"memory": "256Mi", "cpu": "250m"}, "limits": {"memory": "512Mi", "cpu": "500m"}}}, "silo": {"resources": {"requests": {"memory": "1Gi", "cpu": "1"}, "limits": {"memory": "2Gi", "cpu": "2"}}, "autoscaling": {"minReplicas": 4, "maxReplicas": 10}}, "mcp-server": {"resources": {"requests": {"memory": "256Mi", "cpu": "250m"}, "limits": {"memory": "512Mi", "cpu": "500m"}}}}
  ```
  **Note**: The sample configuration file (`build/environment-variables-github-sample.yml`) includes a recommended configuration based on current Container Apps production sizing.

## Workflow Configuration

### 1. Workflow Overview

The modern workflow is located at `.github/workflows/modern-deploy.yml`. It consists of two jobs:

#### Job 1: Publish
- Checks out code and sets up .NET 9.0
- Installs Aspire CLI via curl script
- Builds solution for validation
- Runs `aspire publish` to generate Azure Bicep and Kubernetes Helm artifacts
- Applies comprehensive post-publish fixes including:
  - Azure resource naming patches for compatibility
  - Helm template fixes for port conflicts
  - ConfigMap to Secret migration for connection strings
  - Module output exposure for deployment
- Uploads artifacts for deployment job

#### Job 2: Deploy
- Downloads publish artifacts
- Authenticates with Azure using service principal credentials
- **Step 1: Build and Push Docker Images to ACR**
  - **Uses optimized parallel builds with cached base image** (1-2 min vs 10 min)
  - Builds all service images simultaneously
  - Creates and pushes images: db-setupmanager, api-main, mcp-server, silo, web-docgen
  - Automatically attaches ACR to AKS cluster for image pulls
- **Step 2: Deploy Azure Resources**
  - Applies deploy-stage fixes (principal type, subscription scope, role alignment)
  - Attempts subscription-scoped deployment first (for users with full permissions)
  - Falls back to resource group-scoped deployment if subscription access denied
  - Exports connection strings and endpoints to file for persistence
  - Captures outputs for use in Helm deployment
- **Step 3: Configure AKS**
  - Retrieves AKS cluster credentials
  - Configures ACR login for container image pulls
  - Sets up kubectl context for deployment
- **Step 4: Deploy Kubernetes Applications**
  - Loads Azure resource connection strings from file
  - Injects workload identity service account if configured
  - Deploys application to AKS using generated Helm charts
  - Connection strings stored in Kubernetes Secrets (not ConfigMaps)
  - Passes all configuration to Helm values

### 2. Triggering Deployment

The workflow uses `workflow_dispatch` for manual triggers:

1. Navigate to **Actions** tab in GitHub
2. Select **Modern Deploy** workflow
3. Click **Run workflow**
4. Select target environment from dropdown
5. Click **Run workflow** to start deployment

### 3. Environment Selection

The workflow accepts an environment input parameter:

```yaml
on:
  workflow_dispatch:
    inputs:
      environment:
        description: 'Environment name (matches GitHub environment)'
        required: true
        default: 'dev'
```

This maps to your configured GitHub environments and applies the appropriate variables/secrets.

## Application Gateway Support (Pre-release - Not Ready for Production)

> ⚠️ **WARNING**: Application Gateway integration is currently in pre-release and disabled by default. It is not ready for production use.

### Enabling Application Gateway (Experimental)

To enable the Application Gateway for testing purposes in your GitHub Actions workflow:

1. Add the following to your environment variables file (e.g., `build/environment-variables-github-dev.yml`):
   ```yaml
   variables:
     ENABLE_APPLICATION_GATEWAY: 'true'
   ```

2. For public deployments, no additional configuration is needed.

3. For private/hybrid deployments, provide subnet configuration:
   ```yaml
   variables:
     ENABLE_APPLICATION_GATEWAY: 'true'
     APPLICATION_GATEWAY_SUBNET_ID: '/subscriptions/.../resourceGroups/.../providers/Microsoft.Network/virtualNetworks/.../subnets/snet-appgw'
   ```

### Known Issues
- Bicep generation may produce incorrect syntax for complex parameters
- Resource deployment ordering issues with role assignments
- Subnet configuration required even for public deployments (being addressed)
- The feature is disabled by default in `AzureDependencies.cs`

### When Ready for Production
Once the Application Gateway integration is stable:
1. Change the default in `src/Microsoft.Greenlight.AppHost/Hosting/Resources/AzureDependencies.cs` from `false` to `true`
2. Update documentation to reflect production readiness
3. Test thoroughly with all deployment models (public/private/hybrid)

## Architecture Details

### Federated Authentication

The workflow uses Azure federated credentials for secure authentication:

```yaml
- name: Azure login (federated)
  uses: azure/login@v2
  with:
    creds: ${{ secrets.AZURE_CREDENTIALS }}
```

### AppHost-Driven Provisioning

The modern approach uses AppHost as the single source of truth:

```csharp
// Kubernetes environment for compute
var k8s = builder.AddKubernetesEnvironment("k8s");

// Azure services modeled in AppHost
var docGenSql = builder.AddAzureSqlServer("sqldocgen").AddDatabase(sqlDatabaseName!);
var redisResource = builder.AddAzureRedis("redis");
var blobStorage = builder.AddAzureStorage("docing").AddBlobs("blob-docing");
```

### Generated Artifacts

`aspire publish` generates a complete deployment package:
- **main.bicep** - Entry point Bicep template (subscription-scoped by default)
- **Module folders** - Each Azure resource as a separate Bicep module
- **Chart.yaml** - Helm chart definition for Kubernetes deployment
- **templates/** - Kubernetes manifests for application containers
- **values.yaml** - Default Helm values with placeholders for Azure connections

### Deployment Flow

The modern deployment follows this sequence:

1. **Azure Resource Deployment** (`build-modern-deploy-azure.sh`)
   - Checks if main.bicep exists (some deployments are Kubernetes-only)
   - Attempts subscription-scoped deployment for full permissions
   - Falls back to resource group-scoped deployment if needed
   - Automatically fixes subscription scope incompatibilities
   - Exports all deployment outputs as `AZURE_OUTPUT_*` environment variables

2. **Kubernetes Application Deployment** (`build-modern-helm-deploy.sh`)
   - Verifies Helm chart exists at `Chart.yaml` (Aspire 9.4 structure)
   - Loads Azure resource connection strings from environment
   - Configures container registry endpoints
   - Injects connection strings into Helm values
   - Deploys applications with proper Azure service bindings

### Connection String Flow

Azure resource connection strings flow automatically from Bicep to Helm:

```bash
# Azure deployment exports outputs
export AZURE_OUTPUT_SQLDOCGEN="Server=tcp:sqldocgen..."
export AZURE_OUTPUT_MANAGED_REDIS="redis.cache.windows.net..."

# Helm deployment consumes them
ConnectionStrings__SQLDOCGEN: "${AZURE_OUTPUT_SQLDOCGEN}"
ConnectionStrings__MANAGED_REDIS: "${AZURE_OUTPUT_MANAGED_REDIS}"
```

### PostgreSQL (kmvectordb) Administrator Password Strategy

Modern deployment scripts automatically provide a deterministic PostgreSQL administrator password unless you explicitly supply one. If you set an environment/secret variable `POSTGRES_ADMIN_PASSWORD`, that value is used. Otherwise the script derives a stable value per subscription + resource group:

`sha256("<subscriptionId>-<resourceGroup>-pgadmin-secret")[0..19] + "Aa1!"`

Rationale:
- Reproducible across re-runs for the same environment
- Unique across subscriptions / resource groups
- Satisfies complexity (upper, lower, digit, special)
- Not committed to source; only computed in the pipeline runner

You can rotate by supplying a new secret `POSTGRES_ADMIN_PASSWORD` (recommended for production).

AppHost local development uses parameter `Parameters:postgresPassword` (see `.devcontainer/devcontainer.json`). Production Bicep templates receive the password via the deployment script which passes `administratorPassword` only to templates that declare it.

### Naming Compatibility

The name patching script ensures compatibility with existing environments:
- Storage: `docing${uniqueString(resourceGroup().id)}` (24 chars)
- Search: `aisearch${uniqueString(resourceGroup().id)}` (60 chars)
- Redis: `redis${uniqueString(resourceGroup().id)}` (63 chars)
- SQL Server: `sqldocgen${uniqueString(resourceGroup().id)}` (63 chars)

## Deployment Models

### Public Mode
- All services publicly accessible
- No private endpoints or VNET integration
- Simplest configuration with internet ingress

### Private Mode
- All PaaS services behind private endpoints
- Application not publicly accessible
- Requires pre-existing VNET and subnets
- Corporate DNS resolution required
- Azure SQL, Storage, Event Hubs, and Azure AI Search automatically set `publicNetworkAccess` to `Disabled` when `DEPLOYMENT_MODEL` is `private` or `hybrid`

**Important**: After deployment completes, private DNS zones must be manually assigned to the private endpoint resources in the target resource group. The deployment creates the private endpoints but cannot automatically link them to existing private DNS zones due to cross-resource-group dependencies.

Required private DNS zones (must exist in your network infrastructure):
- `privatelink.sql.database.azure.com` - For Azure SQL Database
- `privatelink.blob.core.windows.net` - For Azure Storage
- `privatelink.search.windows.net` - For Azure AI Search
- `privatelink.servicebus.windows.net` - For Azure Event Hubs
- `privatelink.postgres.database.azure.com` - For PostgreSQL (if using postgres backend)

To link private endpoints to DNS zones after deployment:
```bash
# List private endpoints in resource group
az network private-endpoint list -g "rg-greenlight-dev" --query "[].{Name:name,Service:privateLinkServiceConnections[0].privateLinkServiceId}" -o table

# Link each private endpoint to appropriate DNS zone
# Note: DNS zones are commonly in a different subscription (hub-spoke network architecture)
az network private-dns link vnet create \
    --resource-group "rg-network-infrastructure" \
    --zone-name "privatelink.sql.database.azure.com" \
    --name "link-greenlight-dev-sql" \
    --virtual-network "/subscriptions/{deployment-sub}/resourceGroups/rg-greenlight-dev/providers/Microsoft.Network/virtualNetworks/{vnet}" \
    --registration-enabled false

# For cross-subscription DNS zones (common in enterprise environments):
az network private-dns link vnet create \
    --subscription "{hub-subscription-id}" \
    --resource-group "rg-network-hub-dns" \
    --zone-name "privatelink.sql.database.azure.com" \
    --name "link-greenlight-dev-sql" \
    --virtual-network "/subscriptions/{deployment-sub}/resourceGroups/rg-greenlight-dev/providers/Microsoft.Network/virtualNetworks/{vnet}" \
    --registration-enabled false

# Example with actual values:
# az network private-dns link vnet create \
#     --subscription "00000000-1111-2222-3333-444444444444" \
#     --resource-group "rg-corp-network-dns" \
#     --zone-name "privatelink.sql.database.azure.com" \
#     --name "link-greenlight-dev-sql" \
#     --virtual-network "/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg-greenlight-dev/providers/Microsoft.Network/virtualNetworks/vnet-greenlight" \
#     --registration-enabled false
```
- Requires `AZURE_SUBNET_PE` and optionally `AZURE_SUBNET_POSTGRES`

Helper script:

```bash
./build/scripts/add-private-endpoints-to-dns.sh
```

- Prompts for private endpoint subscription/resource group plus DNS zone resource IDs; skips links that already exist.
- Provide full zone IDs (supports cross-subscription hubs) to avoid manual CLI repetition.

### Hybrid Mode
- Web/API publicly accessible via ingress
- PaaS services behind private endpoints
- Balance of security and accessibility
- Requires `AZURE_SUBNET_PE` for backend service isolation

## Monitoring and Debugging

### Workflow Monitoring

GitHub provides detailed logging for each workflow run:

1. **Actions** tab shows all workflow runs with status
2. **Job logs** provide step-by-step execution details
3. **Annotations** highlight warnings and errors
4. **Artifacts** contain generated deployment files

### Common Debug Steps

#### View Generated Artifacts
1. Go to completed workflow run
2. Scroll to **Artifacts** section
3. Download `publish` artifact
4. Inspect `azure/` and `k8s/helm/` directories

#### Verify Azure Resources
```bash
# List resources in target resource group
az resource list -g rg-greenlight-dev -o table

# Check deployment history
az deployment group list -g rg-greenlight-dev --query "[].{name:name, state:properties.provisioningState, timestamp:properties.timestamp}" -o table
```

#### Post-Deployment Configuration

After deployment completes, verify the Entra ID app registration redirect URL is correctly configured:

**Automatic Configuration**: The deployment workflow attempts to automatically add the correct redirect URL (`https://{your-deployment-url}/signin-oidc`) to the app registration created by the `sp-create.ps1` script. If the deployment service principal has sufficient permissions, this will be handled automatically.

**Manual Configuration** (required if automatic update fails):
1. Navigate to Azure Portal → Azure Active Directory → App registrations
2. Find your application (search for the ClientId from `PVICO_ENTRA_CREDENTIALS`)
3. Go to Authentication → Platform configurations → Web
4. Add redirect URI: `https://{your-deployment-url}/signin-oidc`
5. Save the configuration

**To determine your deployment URL:**
- **Public/Hybrid Mode**: Check Application Gateway public IP or custom domain from `HOSTNAME_OVERRIDE`
- **Private Mode**: Use internal load balancer DNS name or custom internal domain

**Example redirect URLs:**
- Public: `https://20.124.45.67/signin-oidc` or `https://myapp.example.com/signin-oidc`
- Private: `https://greenlight-internal.contoso.com/signin-oidc`

#### Verify Kubernetes Deployment
```bash
# Check pods in namespace
kubectl get pods -n greenlight-dev

# View pod logs
kubectl logs -n greenlight-dev deployment/api-main

# Check Helm release
helm list -n greenlight-dev
helm status greenlight -n greenlight-dev
```

## Security and Best Practices

### Secret Management
- Use GitHub encrypted secrets for sensitive data
- Environment-specific secrets provide isolation
- Repository secrets for shared configuration
- Consider Azure Key Vault for additional secret management

### Access Control
- Configure environment protection rules for production
- Require reviewer approval for sensitive environments
- Use branch protection rules to control deployment triggers
- Implement least-privilege access patterns

### Monitoring
- Enable workflow failure notifications
- Set up Azure Monitor alerts for deployed resources
- Monitor AKS cluster health and resource utilization
- Implement application-level health checks

## Troubleshooting

### Common Issues

#### Authentication Failures
- Verify `AZURE_CREDENTIALS` secret format and validity
- Check service principal permissions (Owner role required)
- Confirm subscription ID matches credentials

#### Build Failures
- Verify .NET 9.0 availability on GitHub runners
- Check Aspire CLI installation and connectivity
- Validate solution builds locally: `dotnet build src/Microsoft.Greenlight.slnx`

#### Aspire 9.4 Specific Issues
- **Subscription scope error**: "The target scope 'subscription' does not match the deployment scope 'resourceGroup'"
  - This is expected - the workflow automatically handles this by converting to resource group scope
  - The `build-modern-fix-subscription-scope.sh` script fixes this automatically
- **Missing main.bicep**: Some deployments may be Kubernetes-only
  - This is normal if no Azure resources are defined in AppHost
  - The deployment scripts handle this gracefully

#### Azure Deployment Failures
- **Permission denied at subscription level**
  - This is handled automatically - the script falls back to resource group deployment
  - Ensure service principal has at least Owner role on the resource group
- **Resource group doesn't exist**
  - The script attempts to create it but may fail with limited permissions
  - Pre-create the resource group if the service principal lacks subscription access
- **Bicep parameter warnings**
  - Unused parameter warnings are expected due to the modular template structure
  - These warnings don't affect deployment success

#### Kubernetes Deployment Failures
- **Missing Azure connection strings**
  - Verify the Azure deployment completed successfully first
  - Check that environment variables are passed between workflow steps
  - Look for `AZURE_OUTPUT_*` variables in the workflow logs
- **Container image pull errors**
  - Verify ACR login is successful in the workflow
  - Check that container images were pushed to ACR
  - Ensure AKS has permissions to pull from ACR

#### Connectivity Issues (Private/Hybrid Mode)
- Verify private DNS zone configuration and linking
- Check VNET subnet assignments and delegation
- Validate NSG rules allow required traffic
- Ensure GitHub self-hosted runners can reach AKS API server
- Check private endpoint configurations for PaaS services
- Verify DNS resolution from within the VNET

### Debug Commands

```bash
# Test bootstrap script locally
build/scripts/build-modern-bootstrap-github.sh \
    build/environment-variables-github-dev.yml \
    your-org/your-repo

# Validate generated artifacts locally
aspire publish -o out/test
build/scripts/build-modern-patch-azure-names.sh out/test
build/scripts/build-modern-deploy-azure.sh out/test rg-test eastus

# Check GitHub CLI environment
gh auth status
gh repo view
gh environment list
```

## Migration from Legacy Container Apps Deployment

If migrating from the legacy Container Apps deployment:

### Variable Mapping
Map legacy variables to modern equivalents:

| Legacy Variable | Modern Equivalent | Notes |
|-----------------|------------------|-------|
| `AZURE_CAE_WORKLOAD_TYPE` | `KUBERNETES_RESOURCES_CONFIG` | Use Kubernetes resource specifications |
| `AZURE_SUBNET_CAE` | Not needed | Kubernetes uses node pools |
| `HOSTNAMEOVERRIDE` | `HOSTNAME_OVERRIDE` + Ingress | Split between app config and K8s Ingress |
| `ENABLE_AZURE_SIGNALR` | AppHost conditional logic | Requires AppHost enhancement |
| `AZURE_CAE_ENVIRONMENT` | `AKS_NAMESPACE` | Namespace-based isolation |

### Migration Strategy
1. **Backup Configuration** - Export existing secrets and connection strings
2. **Environment Parity** - Create new GitHub environments matching existing configuration
3. **Secret Migration** - Transfer secrets from repository level to environment level as needed
4. **Parallel Deployment** - Deploy to new namespace/resource group initially
5. **Data Migration** - Use standard database and storage migration tools
6. **Validation** - Verify functionality with test traffic before DNS cutover
7. **DNS Cutover** - Update DNS records after successful validation
8. **Cleanup** - Archive legacy workflows and remove Container Apps resources

### Rollback Plan
- Keep legacy deployment running during transition
- Document rollback procedures for each phase
- Test rollback in non-production environment first

## Advanced Configuration

### Custom Values Files
The Helm deployment script accepts custom values files for advanced scenarios:

```bash
build/scripts/build-modern-helm-deploy.sh \
    publish \
    greenlight \
    greenlight-dev \
    deploy/values.custom.yaml
```

Example custom values for Kubernetes resources:
```yaml
# values.custom.yaml
api-main:
  resources:
    requests:
      memory: "512Mi"
      cpu: "250m"
    limits:
      memory: "1Gi"
      cpu: "500m"

web-main:
  resources:
    requests:
      memory: "256Mi"
      cpu: "100m"
    limits:
      memory: "512Mi"
      cpu: "200m"
```

### Custom Domain Configuration
For custom domains, configure `HOSTNAME_OVERRIDE` repository variable:

```json
{
  "WebApplicationUrl": "https://myapp.example.com",
  "ApiBaseUrl": "https://api.myapp.example.com"
}
```

This requires corresponding Ingress configuration in Helm values:

```yaml
# values.custom.yaml
ingress:
  enabled: true
  hosts:
    - host: myapp.example.com
      paths:
        - path: /
          pathType: Prefix
          backend:
            service:
              name: web-main
              port: 80
    - host: api.myapp.example.com
      paths:
        - path: /
          pathType: Prefix
          backend:
            service:
              name: api-main
              port: 80
  tls:
    - secretName: myapp-tls
      hosts:
        - myapp.example.com
        - api.myapp.example.com
```

### Private Networking Configuration
For private/hybrid deployments, AppHost must be enhanced to support:

```csharp
// AppHost enhancement needed for private endpoints
if (builder.Environment.IsProduction() && deploymentModel != "public")
{
    var privateEndpointSubnet = builder.Configuration["AZURE_SUBNET_PE"];
    var postgresSubnet = builder.Configuration["AZURE_SUBNET_POSTGRES"];

    // Configure private endpoints for Azure services
    sqlServer.WithPrivateEndpoint(privateEndpointSubnet);
    redis.WithPrivateEndpoint(privateEndpointSubnet);
    storage.WithPrivateEndpoint(privateEndpointSubnet);

    if (usePostgres)
    {
        postgres.WithSubnetDelegation(postgresSubnet);
    }
}
```

### Service Toggles
Conditional resource creation based on feature flags:

```csharp
// AppHost enhancement needed for service toggles
var enableSignalR = builder.Configuration.GetValue<bool>("ENABLE_AZURE_SIGNALR", true);
if (enableSignalR)
{
    var signalr = builder.AddAzureSignalR("signalr");
    // Wire SignalR to projects
}
```

### Multiple Environments
Configure multiple GitHub environments (dev, staging, prod) with:
- Different variable values per environment
- Appropriate protection rules and approval processes
- Environment-specific secrets for isolation
- Custom values files per environment

### Integration with External Systems
- **Azure Key Vault** - For advanced secret management
- **Azure Monitor** - For centralized logging and alerting
- **External DNS** - For custom domain management
- **cert-manager** - For automatic SSL certificate provisioning

### Migration from Legacy Variables
When migrating from the legacy Container Apps deployment, map variables as follows:

| Legacy Variable | Modern Equivalent | Notes |
|-----------------|------------------|--------|
| `AZURE_CAE_WORKLOAD_TYPE` | `KUBERNETES_RESOURCES_CONFIG` | Use Kubernetes resource specifications |
| `AZURE_SUBNET_CAE` | Not needed | Kubernetes uses node pools |
| `HOSTNAMEOVERRIDE` | `HOSTNAME_OVERRIDE` + Ingress values | Split between app config and Kubernetes Ingress |
| `ENABLE_AZURE_SIGNALR` | AppHost conditional logic | Requires AppHost enhancement |

This modern deployment approach provides a robust, scalable foundation for deploying Microsoft Greenlight across multiple environments with proper governance and security controls while maintaining compatibility with all legacy deployment scenarios.
