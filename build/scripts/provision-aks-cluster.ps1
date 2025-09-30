#!/usr/bin/env pwsh
# AKS Cluster Provisioning Script for Microsoft Greenlight
# Mirrors build/scripts/provision-aks-cluster.sh functionality for PowerShell users.

param(
    [Parameter(Mandatory = $true)] [string]$ResourceGroup,
    [Parameter(Mandatory = $true)] [string]$Location,
    [Parameter(Mandatory = $true)] [string]$ClusterName,
    [Parameter()] [int]$NodeCount = 3,
    [Parameter()] [string]$NodeSize = "Standard_D4s_v6",
    [Parameter()] [ValidateSet('public', 'private', 'hybrid')] [string]$DeploymentModel = 'public',
    [Parameter()] [string]$VnetResourceGroup,
    [Parameter()] [string]$VnetName,
    [Parameter()] [string]$SubnetName,
    [Parameter()] [string]$SubnetResourceId,
    [Parameter()] [string]$ServiceCidr = '10.0.0.0/16',
    [Parameter()] [string]$DnsServiceIp = '10.0.0.10',
    [Parameter()] [string]$PrivateDnsZone = 'system',
    [Parameter()] [string]$SshKeyPath,
    [Parameter()] [switch]$EnableDiagnosticLogs,
    [Parameter()] [switch]$CreateClusterWorkloadIdentity,
    [Parameter()] [string]$WorkloadIdentityName,
    [Parameter()] [string]$WorkloadIdentityNamespace = 'greenlight-dev',
    [Parameter()] [string]$WorkloadIdentityServiceAccount = 'greenlight-app'
)

$ErrorActionPreference = 'Stop'

function Write-Info    { Write-Host "[INFO] $($args[0])"    -ForegroundColor Cyan }
function Write-Success { Write-Host "[SUCCESS] $($args[0])" -ForegroundColor Green }
function Write-Warning { Write-Host "[WARN] $($args[0])"   -ForegroundColor Yellow }
function Write-ErrorMsg{ Write-Host "[ERROR] $($args[0])"  -ForegroundColor Red }

Write-Host "`nAKS Cluster Provisioning" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Resource Group : $ResourceGroup"
Write-Host "Location       : $Location"
Write-Host "Cluster Name   : $ClusterName"
Write-Host "Node Count     : $NodeCount"
Write-Host "Node Size      : $NodeSize"
Write-Host "Deployment     : $DeploymentModel"

$clusterMode = $DeploymentModel.ToLowerInvariant()
if ($clusterMode -eq 'hybrid') {
    Write-Warning "Hybrid deployment uses a public API server with VNET integration; cluster mode set to 'public'."
    $clusterMode = 'public'
}

if ($clusterMode -eq 'private') {
    if (-not $SubnetResourceId -and (-not $VnetName -or -not $SubnetName)) {
        Write-ErrorMsg "Private clusters require VNET integration. Provide either -SubnetResourceId or both -VnetName and -SubnetName."
        exit 1
    }
}

if ($SubnetResourceId) {
    $targetSubscription = ($SubnetResourceId.Split('/')[2])
    Write-Info "Switching Azure context to subscription from subnet: $targetSubscription"
    az account set --subscription $targetSubscription | Out-Null
} else {
    $acct = az account show --output json | ConvertFrom-Json
    $targetSubscription = $acct.id
    Write-Info "Using current Azure subscription: $targetSubscription"
}

$existingRg = az group exists --name $ResourceGroup
if ($existingRg -eq 'false') {
    Write-Info "Creating resource group $ResourceGroup in $Location"
    az group create --name $ResourceGroup --location $Location --subscription $targetSubscription --output none
    Write-Success "Resource group created"
} else {
    Write-Success "Resource group already exists"
}

if (-not $SshKeyPath) {
    $sshDir = Join-Path $env:USERPROFILE '.ssh'
    if (-not (Test-Path $sshDir)) { New-Item -ItemType Directory -Path $sshDir | Out-Null }
    $SshKeyPath = Join-Path $sshDir "aks-$ClusterName"
}
if (-not (Test-Path "$SshKeyPath.pub")) {
    Write-Info "Generating SSH key at $SshKeyPath"
    ssh-keygen -t rsa -b 4096 -f $SshKeyPath -N '' -C "aks-$ClusterName" | Out-Null
    Write-Success "SSH key generated"
} else {
    Write-Info "Using existing SSH key $SshKeyPath"
}

Write-Info "Detecting availability zones for $Location..."
$zonesJson = az vm list-skus --location $Location --size $NodeSize --query "[?resourceType=='virtualMachines'] | [0].locationInfo[0].zones" --output json 2>$null
$zoneArgs = @()
if ($zonesJson -and $zonesJson -ne 'null') {
    $zones = $zonesJson | ConvertFrom-Json
    if ($zones) {
        Write-Success "Availability zones detected: $($zones -join ',')"
        $zoneArgs = @('--zones') + $zones
    } else {
        Write-Warning "No availability zones detected for $NodeSize in $Location"
    }
} else {
    Write-Warning "Could not determine availability zones"
}

# Resolve subnet ID if VNET parameters provided
$resolvedSubnetId = $null
if ($SubnetResourceId) {
    $resolvedSubnetId = $SubnetResourceId
} elseif ($VnetName -and $SubnetName) {
    $vnetRg = if ($VnetResourceGroup) { $VnetResourceGroup } else { $ResourceGroup }
    $resolvedSubnetId = az network vnet subnet show --resource-group $vnetRg --vnet-name $VnetName --name $SubnetName --query id -o tsv
    if (-not $resolvedSubnetId) {
        Write-ErrorMsg "Unable to resolve subnet $SubnetName in VNET $VnetName"
        exit 1
    }
}

$clusterExists = $false
try {
    az aks show --resource-group $ResourceGroup --name $ClusterName --output none
    $clusterExists = $true
} catch {
    $clusterExists = $false
}

if ($clusterExists) {
    Write-Warning "AKS cluster $ClusterName already exists in $ResourceGroup"
    $acceptDefaults = [string]::Equals([Environment]::GetEnvironmentVariable('ACCEPT_DEFAULTS'), 'true', 'OrdinalIgnoreCase')
    $updateCluster = $false
    if ($acceptDefaults) {
        Write-Info "ACCEPT_DEFAULTS=true - skipping cluster modifications"
    } else {
        $response = Read-Host "Update existing cluster configuration? (y/N)"
        if ($response -match '^[Yy]$') { $updateCluster = $true }
    }

    if ($updateCluster) {
        az aks nodepool scale --resource-group $ResourceGroup --cluster-name $ClusterName --name nodepool1 --node-count $NodeCount --output none
        Write-Success "Scaled node pool to $NodeCount node(s)"
    }
} else {
    $aksArgs = @(
        'aks','create',
        '--resource-group', $ResourceGroup,
        '--name', $ClusterName,
        '--location', $Location,
        '--kubernetes-version', '1.31',
        '--node-count', $NodeCount,
        '--node-vm-size', $NodeSize,
        '--enable-managed-identity',
        '--enable-oidc-issuer',
        '--enable-workload-identity',
        '--ssh-key-value', "$SshKeyPath.pub",
        '--enable-cluster-autoscaler',
        '--min-count', '2',
        '--max-count', '10',
        '--node-osdisk-size', '100',
        '--node-osdisk-type', 'Managed',
        '--tier', 'Standard',
        '--tags', "ManagedBy=Aspire", "ClusterMode=$clusterMode", "CostControl=ignore"
    )
    if ($zoneArgs.Count -gt 0) { $aksArgs += $zoneArgs }

    if ($resolvedSubnetId) {
        $aksArgs += @('--vnet-subnet-id', $resolvedSubnetId, '--service-cidr', $ServiceCidr, '--dns-service-ip', $DnsServiceIp)
    }

    switch ($clusterMode) {
        'public' {
            $dnsPrefix = ($ClusterName -replace '[^a-z0-9-]', '').ToLower()
            $aksArgs += @('--network-plugin','azure','--network-policy','azure','--enable-addons','monitoring','--dns-name-prefix',$dnsPrefix)
            if ($resolvedSubnetId) {
                Write-Info "Configuring public cluster with VNET integration"
            } else {
                Write-Info "Configuring public cluster without VNET integration"
            }
        }
        'private' {
            if (-not $resolvedSubnetId) {
                Write-ErrorMsg "Private clusters require a VNET subnet"
                exit 1
            }
            $privateDnsParam = @('--private-dns-zone','system')
            if ($PrivateDnsZone -eq 'none') {
                Write-Warning "No private DNS zone will be linked automatically"
                $privateDnsParam = @('--private-dns-zone','none')
            } elseif ($PrivateDnsZone -ne 'system') {
                Write-Info "Using custom private DNS zone: $PrivateDnsZone"
                $privateDnsParam = @('--private-dns-zone',"$PrivateDnsZone")
            }
            $aksArgs += @('--network-plugin','azure','--network-policy','azure','--enable-private-cluster')
            $aksArgs += $privateDnsParam
            $aksArgs += @('--enable-addons','monitoring')
            Write-Warning "Private cluster API server will only be accessible within the VNET"
        }
    }

    Write-Info "Creating AKS cluster (this can take 10-15 minutes)..."
    $aksArgs += @('--output','none')
    az @aksArgs
    Write-Success "AKS cluster created"
}

$enableDiag = $EnableDiagnosticLogs.IsPresent -or ([Environment]::GetEnvironmentVariable('AKS_DIAGNOSTICLOGS') -eq 'true')
if ($enableDiag) {
    Write-Info "Enabling diagnostic logs for AKS cluster"
    $workspaceId = az monitor log-analytics workspace list --resource-group $ResourceGroup --query "[0].id" -o tsv 2>$null
    if (-not $workspaceId) {
        $workspaceName = "log-$ClusterName-$([System.Guid]::NewGuid().ToString('N').Substring(0,6))"
        $workspaceId = az monitor log-analytics workspace create --resource-group $ResourceGroup --workspace-name $workspaceName --location $Location --query id -o tsv
        Write-Success "Created Log Analytics workspace $workspaceName"
    }
    az monitor diagnostic-settings create `
        --name 'aks-diagnostics' `
        --resource "/subscriptions/$targetSubscription/resourceGroups/$ResourceGroup/providers/Microsoft.ContainerService/managedClusters/$ClusterName" `
        --workspace $workspaceId `
        --logs '[{"category":"kube-apiserver","enabled":true},{"category":"kube-controller-manager","enabled":true},{"category":"kube-scheduler","enabled":true},{"category":"cluster-autoscaler","enabled":true}]' `
        --metrics '[{"category":"AllMetrics","enabled":true}]' `
        --only-show-errors | Out-Null
    Write-Success "Diagnostic settings configured"
} else {
    Write-Info "Diagnostic logs not enabled (set -EnableDiagnosticLogs or AKS_DIAGNOSTICLOGS=true to enable)"
}

$uamiClientId = $null
$uamiPrincipalId = $null
$uamiResourceId = $null
if ($CreateClusterWorkloadIdentity) {
    $identityName = if ($WorkloadIdentityName) { $WorkloadIdentityName } else { "uami-$ClusterName" }
    $identityName = [System.Text.RegularExpressions.Regex]::Replace($identityName.ToLowerInvariant(), "[^a-z0-9-]", "-")
    $identityName = $identityName.Trim('-')
    Write-Info "Ensuring user-assigned managed identity: $identityName"
    $identityJson = az identity show --resource-group $ResourceGroup --name $identityName --output json 2>$null
    if (-not $identityJson) {
        $identityJson = az identity create --resource-group $ResourceGroup --name $identityName --location $Location --output json
        Write-Success "Created managed identity $identityName"
    } else {
        Write-Info "Managed identity already exists"
    }
    $identity = $identityJson | ConvertFrom-Json
    $uamiClientId = $identity.clientId
    $uamiPrincipalId = $identity.principalId
    $uamiResourceId = $identity.id
    Write-Warning "Cluster-level workload identity created. Deployments normally create per-environment identities."
}

$oidcIssuer = $null
for ($attempt = 0; $attempt -lt 3 -and -not $oidcIssuer; $attempt++) {
    $oidcIssuer = az aks show --resource-group $ResourceGroup --name $ClusterName --query "oidcIssuerProfile.issuerUrl" -o tsv 2>$null
    if (-not $oidcIssuer) { Start-Sleep -Seconds 10 }
}
if ($oidcIssuer) {
    Write-Success "OIDC issuer: $oidcIssuer"
    if ($CreateClusterWorkloadIdentity -and $uamiClientId) {
        $federatedSubject = "system:serviceaccount:$WorkloadIdentityNamespace:$WorkloadIdentityServiceAccount"
        $fcName = "fc-$WorkloadIdentityNamespace-$WorkloadIdentityServiceAccount"
        $existingFc = az identity federated-credential list --identity-name $identityName --resource-group $ResourceGroup -o json 2>$null | ConvertFrom-Json | Where-Object { $_.name -eq $fcName }
        if (-not $existingFc) {
            az identity federated-credential create `
                --identity-name $identityName `
                --resource-group $ResourceGroup `
                --name $fcName `
                --issuer $oidcIssuer `
                --subject $federatedSubject `
                --audiences 'api://AzureADTokenExchange' | Out-Null
            Write-Success "Created federated credential $fcName"
        } else {
            Write-Info "Federated credential $fcName already exists"
        }
    }
} else {
    Write-Warning "Could not retrieve OIDC issuer yet. It will become available shortly."
}

if ($clusterMode -ne 'private') {
    Write-Info "Retrieving kubectl credentials"
    az aks get-credentials --resource-group $ResourceGroup --name $ClusterName --overwrite-existing --output none
    try {
        kubectl cluster-info | Out-Null
        Write-Success "kubectl connectivity verified"
    } catch {
        Write-Warning "kubectl connectivity verification failed"
    }
    foreach ($ns in @('greenlight-dev','greenlight-staging','greenlight-prod')) {
        $exists = kubectl get namespace $ns 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Info "Namespace $ns already exists"
        } else {
            kubectl create namespace $ns | Out-Null
            Write-Success "Created namespace $ns"
        }
    }
}

$summary = [ordered]@{
    clusterName     = $ClusterName
    resourceGroup   = $ResourceGroup
    location        = $Location
    deploymentModel = $DeploymentModel
    workloadIdentity = [ordered]@{
        clientId        = $uamiClientId
        principalId     = $uamiPrincipalId
        resourceId      = $uamiResourceId
        oidcIssuer      = $oidcIssuer
        federatedSubject = if ($CreateClusterWorkloadIdentity -and $uamiClientId) { "system:serviceaccount:$WorkloadIdentityNamespace:$WorkloadIdentityServiceAccount" } else { $null }
    }
} | ConvertTo-Json -Depth 5
Set-Content -Path 'aks-provision-summary.json' -Value $summary -Encoding UTF8
Write-Success "Wrote provisioning summary to aks-provision-summary.json"

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "AKS Cluster Provisioning Complete" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host "AKS_CLUSTER_NAME: $ClusterName"
Write-Host "AKS_RESOURCE_GROUP: $ResourceGroup"
if ($resolvedSubnetId) {
    Write-Host "AZURE_SUBNET_AKS: $resolvedSubnetId"
}
if ($oidcIssuer) {
    Write-Host "AKS_OIDC_ISSUER: $oidcIssuer"
}
if ($CreateClusterWorkloadIdentity -and $uamiClientId) {
    Write-Host "WORKLOAD_IDENTITY_NAME: $identityName"
    Write-Host "WORKLOAD_IDENTITY_CLIENT_ID: $uamiClientId"
    Write-Host "WORKLOAD_IDENTITY_PRINCIPAL_ID: $uamiPrincipalId"
    Write-Host "WORKLOAD_IDENTITY_RESOURCE_ID: $uamiResourceId"
    Write-Host "WORKLOAD_IDENTITY_FEDERATED_SUBJECT: system:serviceaccount:$WorkloadIdentityNamespace:$WorkloadIdentityServiceAccount"
} else {
    Write-Info "Deployment pipelines will create per-environment workload identities automatically."
}

Write-Host "`nSSH Key:" -ForegroundColor Yellow
Write-Host "  Private key : $SshKeyPath"
Write-Host "  Public key  : $SshKeyPath.pub"
Write-Host "Keep these keys secure; they grant SSH access to cluster nodes." -ForegroundColor Yellow
