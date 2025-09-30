param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$EnvYaml,

        [Parameter(Mandatory = $true, Position = 1)]
        [string]$AdoOrg,

        [Parameter(Mandatory = $true, Position = 2)]
        [string]$AdoProject
)

<#
----------------------------------------------------------------------------------
Bootstrap Azure DevOps variable GROUP *for a single environment* plus the ADO
Environment resource, based on a repo-local YAML descriptor.

IMPORTANT:
    • Each environment (dev / staging / prod, etc.) MUST have its own YAML and its
        own variable group. This script never shares a variable group across environments.
    • We persist three metadata variables in the group so later automation (e.g.,
        workload identity sync) can locate the correct group without re-supplying org
        and project:
                VARIABLE_GROUP_NAME, ADO_ORG_URL, ADO_PROJECT
        These being duplicated across groups is expected; they are environment-scoped.
    • Placeholder workload identity variables are created empty (clientId, principalId,
        resourceId, OIDC issuer, federated subject) so later pipeline steps can fill
        them idempotently after AKS/UAMI provisioning.
    • DEPLOY_AKS is created (default: false) if absent to allow opt-in, per-environment.
----------------------------------------------------------------------------------
#>

$ErrorActionPreference = "Stop"

# Check for yq
if (-not (Get-Command yq -ErrorAction SilentlyContinue)) {
    Write-Error "yq is required"
    exit 1
}

Write-Host "[modern][ADO] Using org=$AdoOrg project=$AdoProject"
az devops configure --defaults organization=$AdoOrg project=$AdoProject

$EnvName = yq -r '.environment.name' $EnvYaml
$VgName = yq -r '.variableGroup.name' $EnvYaml

Write-Host "[modern][ADO] Ensuring environment: $EnvName"
az devops invoke --route-parameters "project=$AdoProject" --area distributedtask --resource environments --http-method GET 2>$null | Out-Null
az pipelines env create --name $EnvName 2>$null | Out-Null

Write-Host "[modern][ADO] Ensuring variable group: $VgName"
$VgId = az pipelines variable-group list --query "[?name=='$VgName'].id | [0]" -o tsv 2>$null
if ([string]::IsNullOrEmpty($VgId)) {
    $VgId = az pipelines variable-group create --name $VgName --authorize true --variables placeholder=1 --query id -o tsv
}

Write-Host "[modern][ADO] Populating variables"
$vars = yq -r '.variableGroup.variables | to_entries[] | "\(.key)=\(.value)"' $EnvYaml
foreach ($line in ($vars -split "`n")) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line -split '=', 2
    if ($parts.Count -eq 2) {
        $key = $parts[0]
        $val = $parts[1]
        az pipelines variable-group variable create --group-id $VgId --name $key --value $val | Out-Null
    }
}

<# Ensure placeholder variables for workload identity if missing #>
$wiVars = @('WORKLOAD_IDENTITY_CLIENT_ID','WORKLOAD_IDENTITY_RESOURCE_ID','WORKLOAD_IDENTITY_PRINCIPAL_ID','AKS_OIDC_ISSUER','WORKLOAD_IDENTITY_FEDERATED_SUBJECT')
foreach ($v in $wiVars) {
    $exists = az pipelines variable-group variable list --group-id $VgId --query $v -o tsv 2>$null
    if (-not $exists) { az pipelines variable-group variable create --group-id $VgId --name $v --value "" | Out-Null }
}

<# Ensure DEPLOY_AKS toggle exists (default false) #>
$deployAks = az pipelines variable-group variable list --group-id $VgId --query 'DEPLOY_AKS' -o tsv 2>$null
if (-not $deployAks) { az pipelines variable-group variable create --group-id $VgId --name DEPLOY_AKS --value 'false' | Out-Null }

<# Persist metadata variables (idempotent) #>
$metaMap = @{
    'VARIABLE_GROUP_NAME' = $VgName
    'ADO_ORG_URL'         = $AdoOrg
    'ADO_PROJECT'         = $AdoProject
}
foreach ($kvp in $metaMap.GetEnumerator()) {
    $exists = az pipelines variable-group variable list --group-id $VgId --query $kvp.Key -o tsv 2>$null
    if (-not $exists) { az pipelines variable-group variable create --group-id $VgId --name $kvp.Key --value $kvp.Value | Out-Null }
}

Write-Host "[modern][ADO] Bootstrap complete for $EnvName"

# Extract key variables for display
$ResourceGroup = yq -r '.variableGroup.variables.AZURE_RESOURCE_GROUP // "not-set"' $EnvYaml
$Location = yq -r '.variableGroup.variables.AZURE_LOCATION // "not-set"' $EnvYaml
$AksClusterName = yq -r '.variableGroup.variables.AKS_CLUSTER_NAME // ""' $EnvYaml
$AksResourceGroup = yq -r '.variableGroup.variables.AKS_RESOURCE_GROUP // ""' $EnvYaml
$DeploymentModel = yq -r '.variableGroup.variables.DEPLOYMENT_MODEL // "public"' $EnvYaml

# Display AKS requirements and next steps
Write-Host ""
Write-Host "========================================="
Write-Host "AKS Cluster Requirements"
Write-Host "========================================="
Write-Host ""

if ([string]::IsNullOrEmpty($AksClusterName)) {
    # Use default naming convention
    $AksClusterName = "aks-$ResourceGroup"
    $AksResourceGroup = $ResourceGroup
    Write-Host "⚠️  AKS_CLUSTER_NAME not configured. Using default naming convention:"
    Write-Host "   Cluster Name: $AksClusterName"
    Write-Host "   Resource Group: $AksResourceGroup"
} else {
    Write-Host "✓ AKS cluster configuration detected:"
    Write-Host "   Cluster Name: $AksClusterName"
    $displayRg = if ([string]::IsNullOrEmpty($AksResourceGroup)) { $ResourceGroup } else { $AksResourceGroup }
    Write-Host "   Resource Group: $displayRg"
}

Write-Host ""
Write-Host "Deployment Model: $DeploymentModel"
Write-Host ""

if ($DeploymentModel -eq "public") {
    Write-Host "To provision the AKS cluster for PUBLIC deployment, run:"
    Write-Host ""
    Write-Host "  # Bash (Linux/macOS):"
    Write-Host "  ./build/scripts/provision-aks-cluster.sh \"
    Write-Host "    $ResourceGroup \"
    Write-Host "    $Location \"
    Write-Host "    $AksClusterName"
    Write-Host ""
    Write-Host "  # PowerShell (Windows):"
    Write-Host "  .\\build\\scripts\\provision-aks-cluster.ps1 ``"
    Write-Host "    -ResourceGroup $ResourceGroup ``"
    Write-Host "    -Location $Location ``"
    Write-Host "    -ClusterName $AksClusterName"
} elseif ($DeploymentModel -eq "hybrid" -or $DeploymentModel -eq "private") {
    Write-Host "⚠️  $DeploymentModel deployment requires pre-existing networking resources:"
    Write-Host ""
    Write-Host "Required subnets:"
    Write-Host "  • AKS subnet (snet-aks): /20 or larger, no delegation"
    Write-Host "  • Private endpoints subnet (snet-pe): /24, no delegation"
    Write-Host "  • PostgreSQL subnet (snet-postgres): /24, delegated to Microsoft.DBforPostgreSQL/flexibleServers (if using postgres)"
    if ($DeploymentModel -eq "hybrid") {
        Write-Host "  • Application Gateway subnet (snet-appgw): /24, no delegation"
    }
    Write-Host ""
    Write-Host "Required DNS zones (can be in different subscription):"
    Write-Host "  • privatelink.database.windows.net"
    Write-Host "  • privatelink.redis.cache.windows.net"
    Write-Host "  • privatelink.blob.core.windows.net"
    Write-Host "  • privatelink.search.windows.net"
    Write-Host "  • privatelink.service.signalr.net (if using SignalR)"
    Write-Host "  • privatelink.postgres.database.azure.com (if using postgres)"
    Write-Host ""

    $AzureSubnetAks = yq -r '.variableGroup.variables.AZURE_SUBNET_AKS // ""' $EnvYaml
    if ([string]::IsNullOrEmpty($AzureSubnetAks)) {
        Write-Host "❌ AZURE_SUBNET_AKS not configured. To provision AKS cluster:"
        Write-Host ""
        Write-Host "  1. Create the required networking resources (see above)"
        Write-Host "  2. Add AZURE_SUBNET_AKS variable to your environment YAML"
        Write-Host "  3. Run PowerShell provisioning script:"
        Write-Host ""
        Write-Host "  .\\build\\scripts\\provision-aks-cluster.ps1 ``"
        Write-Host "    -ResourceGroup $ResourceGroup ``"
        Write-Host "    -Location $Location ``"
        Write-Host "    -ClusterName $AksClusterName ``"
        Write-Host "    -DeploymentModel $DeploymentModel ``"
        Write-Host "    -SubnetResourceId `$env:AZURE_SUBNET_AKS"
    } else {
        Write-Host "✓ AZURE_SUBNET_AKS configured"
        Write-Host ""
        Write-Host "To provision the AKS cluster for $DeploymentModel deployment:"
        Write-Host ""
        Write-Host "  .\\build\\scripts\\provision-aks-cluster.ps1 ``"
        Write-Host "    -ResourceGroup $ResourceGroup ``"
        Write-Host "    -Location $Location ``"
        Write-Host "    -ClusterName $AksClusterName ``"
        Write-Host "    -DeploymentModel $DeploymentModel ``"
        Write-Host "    -SubnetResourceId ""$AzureSubnetAks"""
    }

    if ($DeploymentModel -eq "private") {
        Write-Host ""
        Write-Host "⚠️  IMPORTANT for private deployment:"
        Write-Host "  • API server won't be accessible from public internet"
        Write-Host "  • Requires self-hosted ADO agents within the VNET"
        Write-Host "  • Or Azure DevOps private agents with VNET connectivity"
    }
}

Write-Host ""
Write-Host "========================================="
Write-Host "Variable Configuration Summary"
Write-Host "========================================="
Write-Host ""
Write-Host "Key variables configured in ${VgName}:"
Write-Host "  AZURE_RESOURCE_GROUP: $ResourceGroup"
Write-Host "  AZURE_LOCATION: $Location"
$displayCluster = if ([string]::IsNullOrEmpty($AksClusterName)) { "<using default naming>" } else { $AksClusterName }
Write-Host "  AKS_CLUSTER_NAME: $displayCluster"
$displayAksRg = if ([string]::IsNullOrEmpty($AksResourceGroup)) { "<same as AZURE_RESOURCE_GROUP>" } else { $AksResourceGroup }
Write-Host "  AKS_RESOURCE_GROUP: $displayAksRg"
Write-Host "  DEPLOYMENT_MODEL: $DeploymentModel"

if ($DeploymentModel -ne "public") {
    $AzureSubnetPe = yq -r '.variableGroup.variables.AZURE_SUBNET_PE // ""' $EnvYaml
    Write-Host ""
    Write-Host "Networking variables:"
    $aksDisplay = if ([string]::IsNullOrEmpty($AzureSubnetAks)) { "❌ NOT SET - Required for $DeploymentModel" } else { $AzureSubnetAks }
    Write-Host "  AZURE_SUBNET_AKS: $aksDisplay"
    $peDisplay = if ([string]::IsNullOrEmpty($AzureSubnetPe)) { "❌ NOT SET - Required for $DeploymentModel" } else { $AzureSubnetPe }
    Write-Host "  AZURE_SUBNET_PE: $peDisplay"
}

Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Provision AKS cluster using the commands above"
Write-Host "  2. Grant service connection permissions to the cluster"
Write-Host "  3. Configure any missing variables in ADO Library > Variable Groups > $VgName"
Write-Host "  4. Add secrets (PVICO_ENTRA_CREDENTIALS, PVICO_AZUREMAPS_KEY, etc.)"
Write-Host "  5. Run the deployment pipeline"
