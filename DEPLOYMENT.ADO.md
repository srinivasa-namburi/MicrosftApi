# Azure DevOps Modern Deployment Guide

This guide provides **complete, self-contained instructions** for deploying Microsoft Greenlight using the modern Kubernetes-first approach with .NET Aspire 9.4.x and Azure DevOps pipelines.

> **Note**: This guide requires copying or syncing the GitHub repository to an Azure DevOps repository. Most customers should use [DEPLOYMENT.GitHub.md](DEPLOYMENT.GitHub.md) instead, as they receive access via GitHub.

## Overview

The modern deployment architecture uses:

- **.NET Aspire AppHost** - Single source of truth for service composition and Azure resource provisioning
- **Kubernetes/AKS** - Container orchestration with Helm charts generated from AppHost
- **Azure PaaS Services** - SQL, Storage, Redis, SignalR, Event Hubs, AI Search via Azure Bicep from AppHost
- **Azure DevOps Pipelines** - Modern two-stage pipeline: Aspire Publish → Azure + Helm Deploy

## Prerequisites

### 1. Azure Resources

Create these resources in your target subscription:

#### Required Services:

- **AKS Cluster** - Target Kubernetes cluster (can be provisioned using our script - see below)

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

### 2. Provision AKS Cluster (Recommended)

We provide scripts to automatically provision an AKS cluster with the correct configuration for your deployment model.

#### PowerShell (Windows) – Supports All Deployment Models

```powershell
# Dev environment – public cluster
pwsh ./build/scripts/provision-aks-cluster.ps1 `
    -ResourceGroup rg-greenlight-dev `
    -Location eastus `
    -ClusterName aks-greenlight-dev

# Demo environment – public cluster with extra capacity
pwsh ./build/scripts/provision-aks-cluster.ps1 `
    -ResourceGroup rg-greenlight-demo `
    -Location eastus `
    -ClusterName aks-greenlight-demo `
    -NodeCount 5 `
    -NodeSize Standard_D4s_v6

# Private cluster (API server reachable only from the VNET)
pwsh ./build/scripts/provision-aks-cluster.ps1 `
    -ResourceGroup rg-greenlight-dev `
    -Location eastus `
    -ClusterName aks-greenlight-dev `
    -DeploymentModel private `
    -SubnetResourceId $env:AZURE_SUBNET_AKS

# Hybrid cluster (VNET integrated; API server still public)
pwsh ./build/scripts/provision-aks-cluster.ps1 `
    -ResourceGroup rg-greenlight-dev `
    -Location eastus `
    -ClusterName aks-greenlight-dev `
    -DeploymentModel hybrid `
    -VnetResourceGroup rg-network `
    -VnetName vnet-greenlight `
    -SubnetName snet-aks
```

#### Bash (Linux/macOS) – Supports All Deployment Models

```bash
# Dev environment - Public cluster (default)
build/scripts/provision-aks-cluster.sh rg-greenlight-dev eastus aks-greenlight-dev \
    --wi-namespace greenlight-dev

# Demo environment - Public cluster
build/scripts/provision-aks-cluster.sh rg-greenlight-demo eastus aks-greenlight-demo \
    --wi-namespace greenlight-demo

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

| Deployment Model | AKS Configuration                | Public Endpoints        | Requirements                                                        |
| ---------------- | -------------------------------- | ----------------------- | ------------------------------------------------------------------- |
| **public**       | Public cluster, Azure CNI        | ✅ Azure Front Door      | None                                                                |
| **private**      | Private cluster, VNET integrated | ❌ No public endpoints   | Pre-existing VNET/Subnet, Private ADO agents or self-hosted runners |
| **hybrid**       | VNET integrated, public API      | ✅ Azure Front Door      | Pre-existing VNET/Subnet, Application Gateway for ingress           |

**Key Differences:**
- **Public**: Full public access with Azure Front Door for global distribution
- **Hybrid**: Azure Front Door for public access, but backend services run in VNET with internal load balancers
- **Private**: No public endpoints at all - no Front Door, no external LoadBalancers

**⚠️ CRITICAL**: You cannot mix deployment models on the same AKS cluster. A cluster configured for public networking cannot host private/hybrid deployments, and vice versa.

**What the scripts do:**

- Create resource group if it doesn't exist
- Provision AKS cluster with managed identity
- Enable monitoring, autoscaling, and availability zones
- Configure networking based on deployment model
- Install NGINX Ingress Controller (public/hybrid only)
- Create standard namespaces (greenlight-dev, greenlight-staging, greenlight-prod)
- Retrieve and configure kubeconfig locally (public/hybrid only)
- **Creates workload identity** `uami-<cluster-name>` with federated credential for `greenlight-app` service account
- **Outputs workload identity values** that MUST be added to the ADO variable group (see below)

**Private/Hybrid Networking Considerations:**

For `DEPLOYMENT_MODEL=private` or `DEPLOYMENT_MODEL=hybrid`, you must:

1. **Pre-create networking resources:**

   - VNET with appropriate address space (e.g., 10.0.0.0/16)
   - **Multiple dedicated subnets** (each serving a different purpose):
     - **AKS subnet** (`snet-aks`): For AKS cluster nodes
       - Size: /20 or larger (supports up to 4096 nodes)
       - Delegation: **None required** (AKS manages its own networking)
       - Cannot overlap with service CIDR (default: 10.0.0.0/16)
     - **Private endpoints subnet** (`snet-pe`): For Azure PaaS service private endpoints
       - Size: /24 (supports up to 256 private endpoints)
       - Delegation: **None required** (private endpoints don't need delegation)
     - **PostgreSQL subnet** (`snet-postgres`): For PostgreSQL Flexible Server (only if using postgres backend)
       - Size: /24 minimum
       - Delegation: **Required** - Must be delegated to `Microsoft.DBforPostgreSQL/flexibleServers`
       - Cannot have any other resources in this subnet
     - **Application Gateway subnet** (`snet-appgw`): For Application Gateway (hybrid mode only)
       - Size: /24 recommended
       - Delegation: **None required** (Application Gateway doesn't use delegation)
       - Must be dedicated to Application Gateway only
   - Network Security Groups with appropriate rules
   - Private DNS zones (can be in a different subscription - common in hub-spoke architectures):
     - `privatelink.database.windows.net` - For Azure SQL Database
     - `privatelink.redis.cache.windows.net` - For Azure Redis Cache
     - `privatelink.blob.core.windows.net` - For Azure Storage
     - `privatelink.search.windows.net` - For Azure AI Search
     - `privatelink.service.signalr.net` - For Azure SignalR Service
     - `privatelink.postgres.database.azure.com` - For PostgreSQL Flexible Server

   **Example subnet creation with delegation:**

   ```bash
   # Create VNET
   az network vnet create --name vnet-greenlight --resource-group rg-network --address-prefix 10.0.0.0/16

   # Create AKS subnet (no delegation needed)
   az network vnet subnet create --name snet-aks --vnet-name vnet-greenlight --resource-group rg-network --address-prefix 10.0.0.0/20

   # Create private endpoints subnet (no delegation needed)
   az network vnet subnet create --name snet-pe --vnet-name vnet-greenlight --resource-group rg-network --address-prefix 10.0.16.0/24

   # Create PostgreSQL subnet WITH delegation (only if using postgres backend)
   az network vnet subnet create --name snet-postgres --vnet-name vnet-greenlight --resource-group rg-network \
       --address-prefix 10.0.17.0/24 \
       --delegations Microsoft.DBforPostgreSQL/flexibleServers

   # Create Application Gateway subnet (no delegation needed)
   az network vnet subnet create --name snet-appgw --vnet-name vnet-greenlight --resource-group rg-network --address-prefix 10.0.18.0/24
   ```

2. **Configure ADO agents:**

   - **Private mode**: Requires self-hosted agents within the VNET or Azure DevOps private agents
   - **Hybrid mode**: Standard hosted agents work but ingress requires Application Gateway

3. **Grant additional permissions:**
   - Service principal needs Network Contributor role on the VNET
   - May need Private Endpoint Contributor for PaaS resources

**⚠️ IMPORTANT: Workload Identity Values**

The modern deployment pipeline creates (or reuses) the per-environment workload identity automatically during the “Deploy Azure Resources” stage. For a fresh environment, leave all `WORKLOAD_IDENTITY_*` variables empty in the ADO variable group—the pipeline writes the GUIDs and name back once the managed identity is provisioned.

If you need to capture the values outside the pipeline (for example, rebuilding a variable group), run the helper script after AKS provisioning:

```bash
build/scripts/restore-workload-identity-variables.sh \
    <resource-group> \
    <aks-cluster-name> \
    <variable-group-name> \
    <ado-org-url> \
    <ado-project>
```

> There is currently no PowerShell equivalent. Use WSL, Azure Cloud Shell, or a container if you need to run the restore script from Windows.

**Manual AKS Creation** (if preferred):
If you prefer to create the AKS cluster manually (without using our scripts), ensure it has:

- Managed identity enabled
- Azure CNI networking
- Appropriate VNET integration for your deployment model
- Monitoring add-on
- Cluster autoscaler (recommended)
- Appropriate node sizing for your workload
- Workload identity enabled with OIDC issuer
- A user-assigned managed identity for the application workload

### 3. Azure Service Connection Setup

**Create an Azure Resource Manager service connection in ADO:**

1. Navigate to your Azure DevOps project
2. Go to **Project Settings** (bottom left) → **Service connections**
3. Click **New service connection** → **Azure Resource Manager** → **Next**
4. Choose authentication method:
   - **Service principal (automatic)** - Recommended, ADO creates and manages the service principal
   - **Service principal (manual)** - Use if you have an existing service principal
   - **Workload identity federation** - For enhanced security without secrets
5. Configure the connection:
   - **Subscription**: Select your Azure subscription
   - **Resource group**: Leave blank for subscription-level access or select specific RG
   - **Service connection name**: Enter a name (e.g., `Azure-Greenlight-Dev`)
   - **Description**: Optional description
   - **Security**: Check "Grant access permission to all pipelines" or manage per-pipeline
6. Click **Verify and save**

**Important**: Note the exact service connection name - you'll need it for the `AZURE_SERVICE_CONNECTION` variable.

**Manual Service Principal Creation (if needed):**

If you choose manual authentication or need to create the service principal outside ADO:

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

Use the output JSON to configure a manual service connection in ADO.

### 3. Application Service Principal

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

## Environment Setup

### 1. Multi-Environment Support

The deployment system supports multiple environments (dev, demo, prod, staging, etc.) with complete isolation:

- **Variable Groups**: Each environment uses its own ADO variable group (e.g., `greenlight-adodev`, `greenlight-adodemo`)
- **Kubernetes Namespaces**: Environment-scoped namespaces (`greenlight-dev`, `greenlight-demo`)
- **Helm Releases**: Environment-scoped release names (`greenlight-dev`, `greenlight-demo`)
- **Azure Resources**: Deployed to separate resource groups per environment
- **Workload Identity**: Per resource group isolation

### 2. Create Environment Configuration

Create environment-specific YAML files based on the sample:

```bash
# For dev environment
cp build/environment-variables-ADO-sample.yml build/environment-variables-ADO-dev.yml

# For demo environment
cp build/environment-variables-ADO-sample.yml build/environment-variables-ADO-demo.yml

# For production environment
cp build/environment-variables-ADO-sample.yml build/environment-variables-ADO-prod.yml
```

Edit each environment file with environment-specific values:

**Example for dev environment (build/environment-variables-ADO-dev.yml):**
```yaml
environment:
  name: dev
variableGroup:
  name: greenlight-adodev  # ADO variable group name
  variables:
    AZURE_SERVICE_CONNECTION: "Azure-Greenlight-Dev"
    AZURE_RESOURCE_GROUP: "rg-greenlight-dev"
    AZURE_LOCATION: "eastus"
    AKS_CLUSTER_NAME: "aks-greenlight-dev"
    AKS_NAMESPACE: "greenlight-dev"
    HELM_RELEASE: "greenlight-dev"  # Environment-scoped release name
    ENVIRONMENT_NAME: "dev"
```

**Example for demo environment (build/environment-variables-ADO-demo.yml):**
```yaml
environment:
  name: demo
variableGroup:
  name: greenlight-adodemo  # Different variable group per environment
  variables:
    AZURE_SERVICE_CONNECTION: "Azure-Greenlight-Demo"
    AZURE_RESOURCE_GROUP: "rg-greenlight-demo"
    AZURE_LOCATION: "eastus"
    AKS_CLUSTER_NAME: "aks-greenlight-shared"  # Can use same cluster
    AKS_NAMESPACE: "greenlight-demo"           # But different namespace
    HELM_RELEASE: "greenlight-demo"            # Environment-scoped release name
    ENVIRONMENT_NAME: "demo"
```

> **Subscription overrides:** Only add `AKS_SUBSCRIPTION_ID` if your AKS cluster lives in a *different* subscription than the resources defined by `AZURE_SUBSCRIPTION_ID`. Leave it out (or blank) for the common case where everything shares a single subscription—the pipeline will automatically fall back to `AZURE_SUBSCRIPTION_ID` and the bootstrap script will not create the override variable.

### 2. Bootstrap ADO Environment

#### Prerequisites for Bootstrap Script

Before running the bootstrap script, ensure you have:

**Required Tools:**

- **Azure CLI** - `az --version` should show 2.50.0 or later
- **Azure DevOps CLI Extension** - Install with `az extension add --name azure-devops`
- **jq** - JSON processor for parsing configuration files
  - **Windows**: Download from [jq releases](https://github.com/stedolan/jq/releases) or use `choco install jq`
  - **macOS**: `brew install jq`
  - **Linux**: `sudo apt-get install jq` or `yum install jq`

**Authentication Requirements:**

```bash
# 1. Authenticate with Azure (for service principal operations)
az login --tenant "your-tenant-id"

# 2. Verify correct subscription is selected
az account show
# If wrong subscription: az account set --subscription "your-subscription-id"

# 3. Authenticate with Azure DevOps
az devops login
# When prompted, provide your Azure DevOps personal access token (PAT)

# 4. Verify ADO authentication and project access
az devops project list --org https://dev.azure.com/your-org
```

**Personal Access Token (PAT) Requirements:**
Your Azure DevOps PAT must have these permissions:

- **Environment**: Read & manage
- **Variable Groups**: Read, create & manage
- **Project and Team**: Read
- **Build**: Read (for pipeline integration)

#### Running the Bootstrap Script

Once prerequisites are met, run the bootstrap script for each environment:

**Bootstrap dev environment:**
```bash
build/scripts/build-modern-bootstrap-ado.sh \
    build/environment-variables-ADO-dev.yml \
    https://dev.azure.com/your-org \
    your-project-name
```

**Bootstrap demo environment:**
```bash
build/scripts/build-modern-bootstrap-ado.sh \
    build/environment-variables-ADO-demo.yml \
    https://dev.azure.com/your-org \
    your-project-name
```

**Bootstrap production environment:**
```bash
build/scripts/build-modern-bootstrap-ado.sh \
    build/environment-variables-ADO-prod.yml \
    https://dev.azure.com/your-org \
    your-project-name
```

**What the script does:**

1. **Validates authentication** - Checks Azure CLI and ADO CLI access
2. **Parses configuration** - Reads environment variables from YAML file
3. **Creates ADO Environment** - Sets up deployment approval/gates
4. **Creates Variable Group** - Configures deployment variables
5. **Sets permissions** - Grants pipeline access to resources

**Expected output:**

```
✓ Azure CLI authenticated
✓ Azure DevOps CLI authenticated
✓ Project 'your-project-name' accessible
✓ Environment 'dev' created
✓ Variable group 'greenlight-modern-dev' created
✓ Permissions configured
Bootstrap completed successfully!
```

**Troubleshooting:**

- **"az: command not found"** - Install Azure CLI from [Microsoft docs](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- **"jq: command not found"** - Install jq JSON processor for your platform
- **"Please run 'az devops login'"** - Your PAT has expired or insufficient permissions
- **"Project not found"** - Verify organization URL and project name are correct

### 3. Configure Secrets (Per Environment)

**IMPORTANT**: Each environment requires its own secrets configuration in its respective variable group.

Navigate to **Library → Variable Groups** and configure secrets for each environment:

- **Dev**: `greenlight-adodev` variable group
- **Demo**: `greenlight-adodemo` variable group
- **Prod**: `greenlight-adoprod` variable group

Add these secrets to **each** environment's variable group:

#### Required Secrets:

**Note**: When using an Azure service connection (recommended), you don't need to manually configure AZURE_CREDENTIALS. The service connection handles authentication automatically.

- **PVICO_ENTRA_CREDENTIALS** - JSON output from sp-create.ps1 script (REQUIRED for authentication)
  ```json
  {
    "TenantId": "87654321-4321-4321-4321-210987654321",
    "ClientId": "12345678-1234-1234-1234-123456789012",
    "Scopes": "https://graph.microsoft.com/.default",
    "ClientSecret": "your-app-secret-value"
  }
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
  Endpoint=https://your-resource.openai.azure.com/;Key=your-api-key
  ```
  **Note**: Can be left empty initially and configured later via Configuration UI at `/admin/configuration?section=secrets`.
  The application will start without AI features until configured.

#### Variable Configuration:

- **PVICO_OPENAI_RESOURCEGROUP** - Resource group containing Azure OpenAI
- **MEMORY_BACKEND** - `aisearch` (default) or `postgres`
- **DEPLOYMENT_MODEL** - `public`, `private`, or `hybrid`

For private/hybrid networking mode, also add:

- **DEPLOYMENT_MODEL** - Set to `hybrid` or `private`
- **AZURE_SUBNET_AKS** - Subnet resource ID for AKS cluster nodes (dedicated subnet)
  ```
  /subscriptions/{sub-id}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/snet-aks
  ```
  **⚠️ IMPORTANT**: This subnet must NOT have any subnet delegation. AKS requires an undelegated subnet.

- **AZURE_SUBNET_PE** - Subnet resource ID for private endpoints of Azure PaaS services
  ```
  /subscriptions/{sub-id}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/snet-pe
  ```
- **AZURE_SUBNET_POSTGRES** - Delegated subnet resource ID for PostgreSQL Flexible Server (if using postgres backend)
  ```
  /subscriptions/{sub-id}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/snet-postgres
  ```
  **Note**: This subnet MUST be delegated to `Microsoft.DBforPostgreSQL/flexibleServers`

- **POSTGRES_DNSZONE_RESOURCEID** - Resource ID of private DNS zone for PostgreSQL (can be in different subscription)
  ```
  /subscriptions/{hub-sub-id}/resourceGroups/{dns-rg}/providers/Microsoft.Network/privateDnsZones/privatelink.postgres.database.azure.com
  ```

For custom domain support, add:

- **HOSTNAME_OVERRIDE** - JSON configuration for custom domains and ingress settings
  ```json
  {
    "WebApplicationUrl": "https://myapp.example.com",
    "ApiBaseUrl": "https://api.myapp.example.com"
  }
  ```

For service toggles, add:

- **ENABLE_AZURE_SIGNALR** - `true` (default) or `false` to disable SignalR service
- **AKS_DIAGNOSTICLOGS** - `false` (default) or `true` to enable diagnostic logs for AKS cluster (helps with Azure Security Center compliance)

For Kubernetes resource sizing, add:

- **KUBERNETES_RESOURCES_CONFIG** - JSON configuration for pod resource requests/limits
  ```json
  {
    "api-main": {
      "resources": {
        "requests": { "memory": "512Mi", "cpu": "500m" },
        "limits": { "memory": "1Gi", "cpu": "1" }
      }
    },
    "web-main": {
      "resources": {
        "requests": { "memory": "256Mi", "cpu": "250m" },
        "limits": { "memory": "512Mi", "cpu": "500m" }
      }
    },
    "silo": {
      "resources": {
        "requests": { "memory": "1Gi", "cpu": "1" },
        "limits": { "memory": "2Gi", "cpu": "2" }
      },
      "autoscaling": { "minReplicas": 4, "maxReplicas": 10 }
    },
    "mcpserver-core":{"requests":{"cpu":"100m","memory":"128Mi"},"limits":{"cpu":"500m","memory":"512Mi"},"replicas":{"min":1,"max":5}},"mcpserver-flow": {
      "resources": {
        "requests": { "memory": "256Mi", "cpu": "250m" },
        "limits": { "memory": "512Mi", "cpu": "500m" }
      }
    }
  }
  ```
  **Note**: The sample configuration file (`build/environment-variables-ADO-sample.yml`) includes a recommended configuration based on current Container Apps production sizing.

## Hybrid Networking Deployment Example

For a demo environment using hybrid networking, here's the complete variable group configuration:

### Prerequisites

1. **Create networking resources:**
```bash
# Create resource group and VNET
az group create -n rg-network-demo -l eastus
az network vnet create -n vnet-greenlight-demo -g rg-network-demo --address-prefix 10.1.0.0/16

# Create subnets
az network vnet subnet create -n snet-aks -g rg-network-demo --vnet-name vnet-greenlight-demo --address-prefix 10.1.0.0/20
az network vnet subnet create -n snet-pe -g rg-network-demo --vnet-name vnet-greenlight-demo --address-prefix 10.1.16.0/24

# Optional: PostgreSQL subnet (if using postgres backend)
az network vnet subnet create -n snet-postgres -g rg-network-demo --vnet-name vnet-greenlight-demo \
  --address-prefix 10.1.17.0/24 \
  --delegations Microsoft.DBforPostgreSQL/flexibleServers
```

2. **Provision AKS cluster in hybrid mode:**
```bash
./build/scripts/provision-aks-cluster.sh rg-greenlight-demo eastus aks-greenlight-demo \
  --deployment-model hybrid \
  --subnet-id "/subscriptions/{sub}/resourceGroups/rg-network-demo/providers/Microsoft.Network/virtualNetworks/vnet-greenlight-demo/subnets/snet-aks" \
  --wi-namespace greenlight-demo
```

3. **Configure variable group `greenlight-adodemo`:**
```yaml
AZURE_RESOURCE_GROUP: "rg-greenlight-demo"
AZURE_LOCATION: "eastus"
AKS_CLUSTER_NAME: "aks-greenlight-demo"
AKS_RESOURCE_GROUP: "rg-greenlight-demo"
AKS_NAMESPACE: "greenlight-demo"
HELM_RELEASE: "greenlight-demo"
DEPLOYMENT_MODEL: "hybrid"

# Networking configuration for hybrid mode
AZURE_SUBNET_AKS: "/subscriptions/{sub}/resourceGroups/rg-network-demo/providers/Microsoft.Network/virtualNetworks/vnet-greenlight-demo/subnets/snet-aks"
AZURE_SUBNET_PE: "/subscriptions/{sub}/resourceGroups/rg-network-demo/providers/Microsoft.Network/virtualNetworks/vnet-greenlight-demo/subnets/snet-pe"

# Optional if using PostgreSQL
AZURE_SUBNET_POSTGRES: "/subscriptions/{sub}/resourceGroups/rg-network-demo/providers/Microsoft.Network/virtualNetworks/vnet-greenlight-demo/subnets/snet-postgres"
```

4. **Run the pipeline:**
```yaml
# Select:
environment: demo
variableGroup: auto  # Will use greenlight-adodemo
```

**Result:**
- ✅ Azure Front Door created for public access
- ✅ Backend services run in VNET with internal load balancers
- ✅ Azure PaaS services use private endpoints
- ✅ Public users access via Front Door → Internal LBs → Services

## Pipeline Configuration

### 1. Pipeline Setup

The modern pipeline is located at `build/azure-pipelines-modern.yml`. It consists of two stages:

#### Stage 1: Aspire Publish

- Builds the solution using .NET 9.0
- Installs Aspire CLI
- Runs `aspire publish` to generate Azure Bicep and Kubernetes Helm artifacts
- Applies legacy naming patches for compatibility
- Fixes subscription-scoped Bicep templates for resource group deployment compatibility
- Publishes artifacts for deployment stage

#### Stage 2: Deploy

- Downloads publish artifacts
- **Step 1: Build and Push Docker Images to ACR**
  - **Uses parallel builds for 5x faster execution** (2 min vs 10 min)
  - Builds all service images simultaneously instead of sequentially
  - Creates and pushes images: db-setupmanager, api-main, mcp-server, silo, web-docgen
  - Automatically attaches ACR to AKS cluster for image pulls
- **Step 2: Deploy Azure Resources**
  - Attempts subscription-scoped deployment first (for users with full permissions)
  - Falls back to resource group-scoped deployment if subscription access denied
  - Exports connection strings and endpoints as environment variables
  - Captures outputs for use in Helm deployment
- **Step 3: Deploy Kubernetes Applications**
  - Loads Azure resource connection strings from previous step
  - Configures ACR login for container image pulls
  - Deploys application to AKS using generated Helm charts
  - Passes all configuration and connection strings to Helm values
- Leverages ADO environment for approval gates

### 1.1 Optional Automated AKS Provisioning & Workload Identity

The pipeline can (optionally) provision or reconcile the AKS cluster automatically before deploying Azure resources. This is disabled by default to keep initial customer setup explicit.

Enable by setting the following variable group values (created automatically by the bootstrap script if missing):

| Variable             | Default                       | Purpose                                                                                                  |
| -------------------- | ----------------------------- | -------------------------------------------------------------------------------------------------------- |
| `DEPLOY_AKS`         | `false`                       | When `true`, the deploy stage ensures the AKS cluster exists (idempotent) using the provisioning script. |
| `DEPLOYMENT_MODEL`   | `public`                      | Controls networking flags passed to the provisioning script (`public`, `private`, `hybrid`).             |
| `AKS_CLUSTER_NAME`   | `aks-<AZURE_RESOURCE_GROUP>`  | Override cluster name.                                                                                   |
| `AKS_RESOURCE_GROUP` | `<AZURE_RESOURCE_GROUP>`      | Resource group containing / targeted for the AKS cluster.                                                |
| `AZURE_SUBNET_AKS`   | (required for private/hybrid) | Subnet resource ID for AKS nodes.                                                                        |
| `AKS_DIAGNOSTICLOGS` | `false`                       | When `true`, enables diagnostic logs for AKS cluster to comply with security policies.                   |

When `DEPLOY_AKS=true` the pipeline:

1. Runs `provision-aks-cluster.sh` in non‑interactive mode (`ACCEPT_DEFAULTS=true`).
2. Creates (if absent) a user-assigned managed identity (UAMI) and federated credential for Kubernetes workload identity.
3. If `AKS_DIAGNOSTICLOGS=true`, configures diagnostic logs to a Log Analytics workspace for Azure Security Center compliance.
4. Emits an `aks-provision-summary.json` file consumed by the pipeline.
5. Writes workload identity values (clientId, principalId, resourceId, OIDC issuer, federated subject) back into pipeline variables and synchronizes them into the Azure DevOps variable group for future runs.

> **Note**: AKS diagnostic logs are disabled by default to minimize costs. Enable them by setting `AKS_DIAGNOSTICLOGS=true` if required by your organization's security policies. This helps comply with Azure Security Center's "Resource logs in Azure Kubernetes Service should be enabled" policy.

### 1.2 Workload Identity Role Alignment

Role assignment Bicep modules generated by Aspire originally target a service principal. The pipeline now prefers the workload identity principal (`WORKLOAD_IDENTITY_PRINCIPAL_ID`) if present. A post‑publish script (`build-modern-align-role-principals.sh`) patches role modules to embed the workload identity principalId, ensuring resource role assignments (e.g., Storage Blob Data Reader, SQL, Redis) grant access to the pod identity, not the deployment service principal.

If workload identity variables are not yet set (first run with `DEPLOY_AKS=false`), the pipeline falls back to the deployment service principal. After enabling workload identity, re-running the pipeline will realign role assignments so that data-plane access matches the runtime pod identity.

### 1.3 Automatic Service Account Injection (Workload Identity)

Immediately before Helm deployment the pipeline injects (idempotently) a ServiceAccount and annotations using `build-modern-add-wi-serviceaccount.sh` when `WORKLOAD_IDENTITY_CLIENT_ID` is available:

1. Adds `templates/serviceaccount-workload-identity.yaml` with required annotations:

- `azure.workload.identity/client-id: <WORKLOAD_IDENTITY_CLIENT_ID>`
- `azure.workload.identity/use: "true"`

2. Appends a minimal patch to each Deployment template (if `serviceAccountName` absent) to set `serviceAccountName` to the configured workload identity service account (default: `greenlight-app`).
3. Pods then exchange a projected token with AKS OIDC issuer to obtain Microsoft Entra tokens for Azure SDK calls without secrets.

Environment/customization variables (optional):

| Variable                              | Purpose                                                                                  |
| ------------------------------------- | ---------------------------------------------------------------------------------------- |
| `WORKLOAD_IDENTITY_SERVICE_ACCOUNT`   | Override default service account name (`greenlight-app`).                                |
| `WORKLOAD_IDENTITY_FEDERATED_SUBJECT` | Federated subject value stored for reference (`system:serviceaccount:<namespace>:<sa>`). |

### 1.4 Variable Group Synchronization

The pipeline updates the Azure DevOps variable group (using `build-modern-update-wi-variable-group.sh`) so that once workload identity is established, subsequent runs reuse the same identity values even if `DEPLOY_AKS` later returns to `false`. This prevents role assignment churn and maintains consistent principal IDs across environments.

If you rotate or recreate the UAMI (rare), re-run with `DEPLOY_AKS=true` to resync variables. The script only overwrites changed or empty values.

### 1.5 First-Time Enablement Flow

1. Bootstrap variable group (human-driven) → `DEPLOY_AKS=false` (default).
2. Manually set `DEPLOY_AKS=true` and (if needed) `DEPLOYMENT_MODEL` + networking variables.
3. Run pipeline → AKS + UAMI + federated credential created → variables synchronized.
4. (Optional) Set `DEPLOY_AKS=false` for later runs; workload identity values remain and are applied to role assignments & pods.

### 1.6 Security Notes

- Separation of duties: Deployment service principal no longer requires broad data-plane roles; workload identity principal receives least-privilege assignments via Bicep modules.
- Rotation: Regenerating tokens is not required—workload identity is secretless. Recreating UAMI changes principalId (forces role re-evaluation).
- Audit: Role alignment script creates backups (`*.bak`) before in-place modifications for traceability.

### 2. Triggering Deployment

The modern pipeline supports multi-environment deployment through parameters:

1. Navigate to Pipelines in ADO
2. Select the modern pipeline (`build/azure-pipelines-modern.yml`)
3. Click **"Run pipeline"**
4. **Select target environment**:
   - **Environment**: Choose from `dev`, `demo`, `prod`
   - **Variable Group Name**: (optional) Override auto-computed group name
   - Pipeline automatically uses: `greenlight-ado{environment}` (e.g., `greenlight-adodev`)

**Environment Parameter Examples:**
- **Dev Deployment**: `environment: dev` → Uses `greenlight-adodev` variable group → Deploys to `greenlight-dev` namespace
- **Demo Deployment**: `environment: demo` → Uses `greenlight-adodemo` variable group → Deploys to `greenlight-demo` namespace
- **Prod Deployment**: `environment: prod` → Uses `greenlight-adoprod` variable group → Deploys to `greenlight-prod` namespace

**Advanced Usage:**
```yaml
# Custom variable group override
environment: demo
variableGroup: greenlight-custom-demo
```

### 3. Monitoring Deployment

#### Azure Resources Stage:

- Resource group creation/validation
- Bicep template deployment for each service
- Connection string and configuration output

#### Helm Deployment Stage:

- Namespace creation in AKS
- Helm chart installation/upgrade
- Pod readiness and health checks
- Service endpoint verification
- Automatic app registration redirect URL update (if permissions allow)

#### Post-Deployment Configuration:

After deployment completes, verify the Entra ID app registration redirect URL is correctly configured:

**Automatic Configuration**: The deployment attempts to automatically add the correct redirect URL (`https://{your-deployment-url}/signin-oidc`) to the app registration created by the `sp-create.ps1` script. If the deployment service principal has sufficient permissions, this will be handled automatically.

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

## Application Gateway Support (Pre-release - Not Ready for Production)

> ⚠️ **WARNING**: Application Gateway integration is currently in pre-release and disabled by default. It is not ready for production use.

### Enabling Application Gateway (Experimental)

To enable the Application Gateway for testing purposes in your Azure DevOps pipeline:

1. Add the following to your pipeline variables:

   ```yaml
   variables:
     ENABLE_APPLICATION_GATEWAY: true
   ```

2. For public deployments, no additional configuration is needed.

3. For private/hybrid deployments, provide subnet configuration:
   ```yaml
   variables:
     ENABLE_APPLICATION_GATEWAY: true
     APPLICATION_GATEWAY_SUBNET_ID: /subscriptions/.../resourceGroups/.../providers/Microsoft.Network/virtualNetworks/.../subnets/snet-appgw
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

## AKS Networking Requirements

### Subnet Configuration

When deploying AKS with private or hybrid networking, proper subnet configuration is critical:

#### AKS Node Subnet Requirements

The subnet used for AKS cluster nodes has specific requirements:

- **NO Subnet Delegation**: The AKS node subnet must NOT have any delegation configured
- **Sufficient IP addresses**: Reserve enough IPs for nodes, pods, and services
- **Network Contributor permissions**: The cluster identity needs Network Contributor role on the subnet
- **No reserved ranges**: Cannot use 169.254.0.0/16, 172.30.0.0/16, 172.31.0.0/16, or 192.0.2.0/24

**Common Error**: If you see `SubnetIsDelegated` error during deployment, remove any delegation from the AKS subnet:
```bash
az network vnet subnet update \
  --resource-group <your-rg> \
  --vnet-name <your-vnet> \
  --name <aks-subnet> \
  --remove delegations 0
```

#### PostgreSQL Flexible Server Subnet

If using PostgreSQL as the memory backend, this subnet requires:

- **MUST have delegation**: Delegated to `Microsoft.DBforPostgreSQL/flexibleServers`
- **Dedicated subnet**: Cannot be shared with other resources
- **Minimum /29 size**: PostgreSQL requires at least 8 IP addresses

Configure delegation:
```bash
az network vnet subnet update \
  --resource-group <your-rg> \
  --vnet-name <your-vnet> \
  --name <postgres-subnet> \
  --delegations Microsoft.DBforPostgreSQL/flexibleServers
```

#### Private Endpoints Subnet

For Azure PaaS services (Storage, Key Vault, etc.):

- **No delegation required**: Can be a regular subnet
- **Sufficient IPs**: Each private endpoint consumes one IP
- **Can be shared**: Multiple private endpoints can use the same subnet

### Private DNS Zones for AKS

For private AKS clusters, you need to handle DNS resolution for the API server:

#### DNS Zone Pattern
- **Format**: `privatelink.<region>.azmk8s.io`
- **Example**: `privatelink.swedencentral.azmk8s.io`
- **One zone per region** where you deploy AKS clusters

#### Configuration Options

1. **System-Managed** (Default - good for testing):
   ```bash
   --private-dns-zone system
   ```
   - Azure creates and manages the zone automatically
   - Zone created in the MC_ resource group
   - Not suitable for enterprise with centralized DNS

2. **Custom Private DNS Zone** (Enterprise pattern):
   ```bash
   --private-dns-zone /subscriptions/{hub-sub}/resourceGroups/{dns-rg}/providers/Microsoft.Network/privateDnsZones/privatelink.{region}.azmk8s.io
   ```
   - Pre-create zones in your hub subscription
   - Link zones to all spoke VNETs that need cluster access
   - Centralized DNS management

3. **No DNS Zone** (BYO DNS):
   ```bash
   --private-dns-zone none
   ```
   - You manage DNS resolution completely
   - Requires custom DNS configuration

#### For Hub-Spoke Architecture

In your hub subscription, create regional DNS zones:
```bash
# Create DNS zone for each region
az network private-dns zone create \
  --resource-group rg-dns-hub \
  --name privatelink.swedencentral.azmk8s.io

# Link to spoke VNETs
az network private-dns link vnet create \
  --resource-group rg-dns-hub \
  --zone-name privatelink.swedencentral.azmk8s.io \
  --name link-spoke-greenlight \
  --virtual-network /subscriptions/{spoke-sub}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet} \
  --registration-enabled false
```

### Network Security Groups (NSG)

If using NSGs on your subnets, ensure these rules are configured:

- **AKS Subnet**: Allow all traffic within the node CIDR range
- **Private Endpoints**: Allow traffic from AKS subnet to private endpoint subnet
- **Outbound Internet**: AKS requires outbound internet connectivity for pulling images and updates

## Workload Identity Architecture

### Important: Per-Deployment Identity Isolation

Each deployment (dev, demo, prod) maintains its own workload identity for proper security isolation:

#### What Happens During Deployment

1. **Managed Identity Creation**: Each deployment creates its own managed identity in its resource group:
   - `rg-greenlight-dev` → `uami-greenlight-dev` (or `uami-aks-{cluster-name}`)
   - `rg-greenlight-demo` → `uami-greenlight-demo`
   - `rg-greenlight-prod` → `uami-greenlight-prod`

2. **Federated Credentials**: Links the identity to its specific namespace:
   - `uami-greenlight-dev` trusts `system:serviceaccount:greenlight-dev:greenlight-app`
   - `uami-greenlight-demo` trusts `system:serviceaccount:greenlight-demo:greenlight-app`

3. **Resource Permissions**: Each identity gets permissions only to its own resources:
   - Storage accounts in the same resource group
   - SQL servers in the same resource group
   - No cross-environment access

#### What NOT to Do

- **Don't** create a cluster-wide workload identity during AKS provisioning
- **Don't** share identities between environments
- **Don't** grant cross-resource-group permissions

#### Cluster Provisioning vs Deployment

- **Cluster Provisioning** (`provision-aks-cluster.sh`):
  - Creates the AKS cluster with OIDC issuer enabled
  - Does NOT create workload identities (despite what the script currently does)
  - Sets up namespaces for different environments

- **Deployment Pipeline** (`build-modern-setup-workload-identity.sh`):
  - Creates managed identity in the deployment's resource group
  - Configures federated credentials for that specific namespace
  - Grants permissions to resources in that resource group only

This ensures proper security boundaries between environments sharing the same cluster.

## Deploying to Existing AKS Clusters

### Common Scenario: DEPLOY_AKS=false

In production, clusters are typically pre-provisioned. The pipeline handles this seamlessly:

#### Required Variables

When `DEPLOY_AKS=false` (default), you need:

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `AKS_CLUSTER_NAME` | No | `aks-{AZURE_RESOURCE_GROUP}` | Name of existing AKS cluster |
| `AKS_RESOURCE_GROUP` | No | `{AZURE_RESOURCE_GROUP}` | Resource group containing the cluster |
| `AKS_NAMESPACE` | No | `greenlight-{environment}` | Kubernetes namespace for deployment |

**Note**: If your cluster follows the naming convention `aks-{resource-group}` and is in the same resource group as your resources, you don't need to set these explicitly!

#### Service Principal Permission Requirements

The Azure service principal/connection needs:

**Minimum for Deployment**:
- `Azure Kubernetes Service Cluster User Role` on the AKS cluster
  ```bash
  az role assignment create \
    --assignee {sp-object-id} \
    --role "Azure Kubernetes Service Cluster User Role" \
    --scope /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.ContainerService/managedClusters/{cluster}
  ```

**Or for Full Management** (includes RBAC operations):
- `Azure Kubernetes Service RBAC Cluster Admin`
  ```bash
  az role assignment create \
    --assignee {sp-object-id} \
    --role "Azure Kubernetes Service RBAC Cluster Admin" \
    --scope /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.ContainerService/managedClusters/{cluster}
  ```

**For Workload Identity Setup**:
- `Managed Identity Operator` on the resource group (to create/manage identities)
- `Role Assignment Write` permissions (to grant storage/SQL access)

#### How Authentication Works

1. Pipeline uses `az aks get-credentials` with the service principal
2. This fetches kubeconfig with short-lived tokens
3. kubectl commands use these tokens automatically
4. No permanent credentials are stored

#### Pipeline Behavior

When `DEPLOY_AKS=false`:
1. **Validates** existing cluster compatibility with deployment model
2. **Retrieves** cluster credentials via `az aks get-credentials`
3. **Creates** namespace if it doesn't exist
4. **Deploys** using Helm to the specified namespace
5. **Configures** workload identity in the deployment's resource group

#### For Private Clusters

If deploying to a private cluster:
- Pipeline agents must have network connectivity to the cluster
- Use self-hosted agents in the same VNET, or
- Use Azure DevOps Microsoft-hosted agents with private endpoint connection

#### Example: Multiple Environments, One Cluster

```yaml
# greenlight-adodev variable group
AKS_CLUSTER_NAME: aks-shared-prod
AKS_RESOURCE_GROUP: rg-aks-shared
AKS_NAMESPACE: greenlight-dev
DEPLOY_AKS: false

# greenlight-adodemo variable group
AKS_CLUSTER_NAME: aks-shared-prod
AKS_RESOURCE_GROUP: rg-aks-shared
AKS_NAMESPACE: greenlight-demo
DEPLOY_AKS: false
```

Both deployments use the same cluster but different namespaces, with separate workload identities in their respective resource groups.

## Architecture Details

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

The modern ADO and GitHub deployment scripts align on a deterministic PostgreSQL admin password approach. Unless you explicitly provide `POSTGRES_ADMIN_PASSWORD` (variable/secret), the script derives a stable password per environment:

`sha256("<subscriptionId>-<resourceGroup>-pgadmin-secret")[0..19] + "Aa1!"`

Why:

- Stable across re-deployments (idempotent infra)
- Unique per subscription + resource group
- Meets complexity requirements
- Keeps secrets out of source control

Override by setting a secure variable `POSTGRES_ADMIN_PASSWORD` for rotation. The AppHost uses a parameter `postgresPassword` for local development; production Bicep templates receive the value via the Azure deployment step which injects `administratorPassword` only into templates declaring that parameter.

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
- Requires `AZURE_SUBNET_PE` and optionally `AZURE_SUBNET_POSTGRES`
- Azure SQL, Storage, Event Hubs, and Azure AI Search automatically emit Bicep with `publicNetworkAccess` set to `Disabled` when `DEPLOYMENT_MODEL` is `private` or `hybrid`

**Important**: After deployment completes, private DNS zones must be manually assigned to the private endpoint resources in the target resource group. The deployment creates the private endpoints but cannot automatically link them to existing private DNS zones due to cross-resource-group dependencies.

Required private DNS zones (must exist in your network infrastructure):

- `privatelink.database.windows.net` – Azure SQL Database
- `privatelink.blob.core.windows.net` – Azure Storage (docing/orleans)
- `privatelink.table.core.windows.net` – Azure Storage tables (Orleans state)
- `privatelink.queue.core.windows.net` – Azure Storage queues (if used)
- `privatelink.search.windows.net` – Azure AI Search
- `privatelink.servicebus.windows.net` – Azure Event Hubs

To link the deployed private endpoints to those zones, use either helper:

```bash
# Bash (Linux/macOS/WSL/Azure Cloud Shell)
build/scripts/add-private-endpoints-to-dns.sh

# PowerShell (Windows/Azure Cloud Shell)
pwsh ./build/scripts/add-private-endpoints-to-dns.ps1
```

The scripts prompt for:

1. The subscription and resource group that contain the private endpoints (typically your deployment RG).
2. The subscription and resource group that contain the private DNS zones (often a shared “hub” networking subscription).

They automatically match the standard zone names above, only create DNS zone groups when a link is missing, and support cross-subscription linking without requiring you to paste full resource IDs.

After linking DNS zones for a private or hybrid deployment, rerun the DB SetupManager once to initialize the database with private networking:

```bash
# Bash
build/scripts/run-db-setupmanager.sh \
    <aks-resource-group> \
    <aks-cluster-name> \
    <namespace>

# PowerShell
pwsh ./build/scripts/run-db-setupmanager.ps1 `\
    -ResourceGroup <aks-resource-group> `\
    -ClusterName <aks-cluster-name> `\
    -Namespace <namespace>
```

The helpers prompt for missing values (or default to AZURE_RESOURCE_GROUP / AKS_CLUSTER_NAME / AKS_NAMESPACE) and automatically fetch AKS credentials before launching the one-shot job. Subsequent pipeline runs will execute migrations/SetupManager as part of the post-deploy stage once DNS is in place.

### Hybrid Mode

- Web/API publicly accessible via ingress
- PaaS services behind private endpoints
- Balance of security and accessibility
- Requires `AZURE_SUBNET_PE` for backend service isolation

## Troubleshooting

### Common Issues

#### Build Failures

- Verify .NET 9.0 SDK availability on build agents
- Check Aspire CLI installation and PATH configuration
- Validate solution builds locally: `dotnet build src/Microsoft.Greenlight.slnx`

#### Aspire 9.4 Specific Issues

- **Subscription scope error**: "The target scope 'subscription' does not match the deployment scope 'resourceGroup'"
  - This is expected - the pipeline automatically handles this by converting to resource group scope
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

#### Helm Deployment Failures

- **Missing Azure connection strings**
  - Verify the Azure deployment completed successfully first
  - Check that environment variables are passed between pipeline stages
  - Look for `AZURE_OUTPUT_*` variables in the pipeline logs
- **Container image pull errors**
  - Verify ACR login is successful in the pipeline
  - Check that container images were pushed to ACR
  - Ensure AKS has permissions to pull from ACR

#### Connectivity Issues

- Verify private DNS zone configuration for private mode
- Check VNET subnet assignments and delegation
- Validate security group and firewall rules

### Debugging Commands

```bash
# Verify environment setup
az devops project list
az pipelines variable-group list

# Test local publishing
aspire publish -o out/test
build/scripts/build-modern-patch-azure-names.sh out/test

# Validate Kubernetes access
kubectl cluster-info
kubectl get nodes

# Check Helm deployment
helm list -A
kubectl get pods -n greenlight-dev
```

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

For custom domains, configure `HOSTNAME_OVERRIDE` variable group setting:

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

The deployment system supports multiple approaches for environment isolation:

**Option 1: Shared AKS Cluster (Recommended)**
- **Single AKS cluster** shared across environments (cost-effective)
- **Namespace isolation**: `greenlight-dev`, `greenlight-demo`, `greenlight-prod`
- **Separate Azure resources**: Different resource groups per environment
- **Environment-scoped Helm releases**: `greenlight-dev`, `greenlight-demo`, etc.

**Option 2: Dedicated AKS Clusters**
- **Separate AKS cluster** per environment (maximum isolation)
- **Dedicated infrastructure** per environment
- **Higher cost** but complete compute isolation

**Configuration per environment:**
- Different ADO variable groups for secrets isolation
- Environment-specific approval processes and deployment gates
- Separate Azure Front Door endpoints (if enabled)
- Custom resource sizing per environment via `KUBERNETES_RESOURCES_CONFIG`

**Example multi-environment setup:**
```bash
# Shared cluster approach
AKS_CLUSTER_NAME: "aks-greenlight-shared"
# Dev namespace: greenlight-dev, Helm release: greenlight-dev
# Demo namespace: greenlight-demo, Helm release: greenlight-demo
# Prod namespace: greenlight-prod, Helm release: greenlight-prod

# Dedicated cluster approach
# Dev: aks-greenlight-dev → greenlight-dev namespace
# Demo: aks-greenlight-demo → greenlight-demo namespace
# Prod: aks-greenlight-prod → greenlight-prod namespace
```

### Migration from Legacy Variables

When migrating from the legacy Container Apps deployment, map variables as follows:

| Legacy Variable           | Modern Equivalent                    | Notes                                           |
| ------------------------- | ------------------------------------ | ----------------------------------------------- |
| `AZURE_CAE_WORKLOAD_TYPE` | `KUBERNETES_RESOURCES_CONFIG`        | Use Kubernetes resource specifications          |
| `AZURE_SUBNET_CAE`        | Not needed                           | Kubernetes uses node pools                      |
| `HOSTNAMEOVERRIDE`        | `HOSTNAME_OVERRIDE` + Ingress values | Split between app config and Kubernetes Ingress |
| `ENABLE_AZURE_SIGNALR`    | AppHost conditional logic            | Requires AppHost enhancement                    |

## Migration from Legacy Deployment

If migrating from the legacy Container Apps deployment:

1. **Backup Configuration** - Export existing secrets and connection strings
2. **Parallel Deployment** - Deploy to new namespace/resource group initially
3. **Variable Mapping** - Use migration table above to map legacy variables
4. **Data Migration** - Use standard database and storage migration tools
5. **DNS Cutover** - Update DNS records after validation
6. **Cleanup** - Remove legacy Container Apps resources after successful migration

## Security Considerations

- Service principal credentials are stored securely in ADO variable groups
- Kubernetes secrets are managed through Helm templates
- Private networking isolates PaaS services when required
- RBAC and network policies should be configured per organizational requirements

For additional security hardening, consider:

- Azure Key Vault integration for secret management
- Network policies for pod-to-pod communication
- Private container registry usage
- Regular security scanning and updates
