param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$EnvYaml,

    [Parameter(Mandatory = $true, Position = 1)]
    [string]$Repo
)

$ErrorActionPreference = "Stop"

# Check for required tools
if (-not (Get-Command yq -ErrorAction SilentlyContinue)) {
    Write-Error "yq is required"
    exit 1
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "gh is required"
    exit 1
}

$EnvName = yq -r '.environment.name' $EnvYaml

Write-Host "[modern][GH] Ensuring environment: $EnvName"
gh api -X PUT "repos/$Repo/environments/$EnvName" | Out-Null

Write-Host "[modern][GH] Populating vars"
$vars = yq -r '.variables | to_entries[] | "\(.key)=\(.value)"' $EnvYaml
foreach ($line in ($vars -split "`n")) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line -split '=', 2
    if ($parts.Count -eq 2) {
        $key = $parts[0]
        $val = $parts[1]
        gh variable set $key --env $EnvName --repo $Repo --body $val
    }
}

Write-Host "[modern][GH] Populating secrets"
$secs = yq -r '.secrets | to_entries[] | "\(.key)=\(.value)"' $EnvYaml
foreach ($line in ($secs -split "`n")) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line -split '=', 2
    if ($parts.Count -eq 2) {
        $key = $parts[0]
        $val = $parts[1]
        gh secret set $key --env $EnvName --repo $Repo --body $val
    }
}

Write-Host "[modern][GH] Bootstrap complete for $EnvName"

# Extract key variables for display
$ResourceGroup = yq -r '.variables.AZURE_RESOURCE_GROUP // "not-set"' $EnvYaml
$Location = yq -r '.variables.AZURE_LOCATION // "not-set"' $EnvYaml
$AksClusterName = yq -r '.variables.AKS_CLUSTER_NAME // ""' $EnvYaml
$AksResourceGroup = yq -r '.variables.AKS_RESOURCE_GROUP // ""' $EnvYaml
$DeploymentModel = yq -r '.variables.DEPLOYMENT_MODEL // "public"' $EnvYaml

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

    $AzureSubnetAks = yq -r '.variables.AZURE_SUBNET_AKS // ""' $EnvYaml
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
        Write-Host "  • Requires self-hosted GitHub runners within the VNET"
    }
}

Write-Host ""
Write-Host "========================================="
Write-Host "Variable Configuration Summary"
Write-Host "========================================="
Write-Host ""
Write-Host "Key variables configured in $EnvName environment:"
Write-Host "  AZURE_RESOURCE_GROUP: $ResourceGroup"
Write-Host "  AZURE_LOCATION: $Location"
$displayCluster = if ([string]::IsNullOrEmpty($AksClusterName)) { "<using default naming>" } else { $AksClusterName }
Write-Host "  AKS_CLUSTER_NAME: $displayCluster"
$displayAksRg = if ([string]::IsNullOrEmpty($AksResourceGroup)) { "<same as AZURE_RESOURCE_GROUP>" } else { $AksResourceGroup }
Write-Host "  AKS_RESOURCE_GROUP: $displayAksRg"
Write-Host "  DEPLOYMENT_MODEL: $DeploymentModel"

if ($DeploymentModel -ne "public") {
    $AzureSubnetPe = yq -r '.variables.AZURE_SUBNET_PE // ""' $EnvYaml
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
Write-Host "  2. Grant service principal permissions to the cluster"
Write-Host "  3. Configure any missing secrets in GitHub Settings > Secrets > Environment secrets"
Write-Host "  4. Add secrets (PVICO_ENTRA_CREDENTIALS, PVICO_AZUREMAPS_KEY, etc.)"
Write-Host "  5. Run the deployment workflow"