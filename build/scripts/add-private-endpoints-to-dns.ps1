#!/usr/bin/env pwsh
# Helper script to link private endpoints with existing Private DNS zones.

$ErrorActionPreference = 'Stop'

function Prompt-Value($message, $default = $null) {
    if ($null -ne $default -and $default -ne '') {
        return Read-Host "$message [$default]"
    }
    return Read-Host $message
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI (az) is required"
}

$peSubscription = Prompt-Value "Subscription ID for private endpoints (leave blank for current context)"
if ([string]::IsNullOrWhiteSpace($peSubscription)) {
    $peSubscription = (az account show --query id -o tsv 2>$null)
    if (-not $peSubscription) {
        Write-Error "Unable to determine current Azure subscription."
    }
}
az account set --subscription $peSubscription | Out-Null
Write-Host "[dns-link] Using private endpoint subscription: $peSubscription"

$peResourceGroup = Prompt-Value "Resource group containing the private endpoints"
if ([string]::IsNullOrWhiteSpace($peResourceGroup)) { Write-Error "Resource group is required." }

$dnsSubscription = Prompt-Value "Subscription ID for private DNS zones (leave blank to reuse $peSubscription)"
if ([string]::IsNullOrWhiteSpace($dnsSubscription)) { $dnsSubscription = $peSubscription }

$dnsResourceGroup = Prompt-Value "Resource group containing the private DNS zones"
if ([string]::IsNullOrWhiteSpace($dnsResourceGroup)) { Write-Error "Private DNS zone resource group is required." }

Write-Host "[dns-link] Discovering private DNS zones in $dnsResourceGroup (subscription $dnsSubscription)..."
$zonesJson = az network private-dns zone list --subscription $dnsSubscription --resource-group $dnsResourceGroup -o json
$zones = $zonesJson | ConvertFrom-Json
if (-not $zones -or $zones.Count -eq 0) {
    Write-Host "[dns-link] No private DNS zones found. Nothing to link."
    exit 0
}

$expectedZones = [ordered]@{
    sqlServer     = 'privatelink.database.windows.net'
    blob          = 'privatelink.blob.core.windows.net'
    table         = 'privatelink.table.core.windows.net'
    queue         = 'privatelink.queue.core.windows.net'
    searchService = 'privatelink.search.windows.net'
    namespace     = 'privatelink.servicebus.windows.net'
}

$zoneIdMap = @{}
foreach ($key in $expectedZones.Keys) {
    $zoneName = $expectedZones[$key]
    $match = $zones | Where-Object { $_.name -eq $zoneName }
    if ($match) {
        $zoneIdMap[$key] = $match.id
        Write-Host "[dns-link] Found private DNS zone for $key: $zoneName"
    } else {
        Write-Host "[dns-link] No private DNS zone named $zoneName found; links for $key will be skipped."
    }
}

Write-Host "[dns-link] Discovering private endpoints in $peResourceGroup..."
$peJson = az network private-endpoint list --subscription $peSubscription --resource-group $peResourceGroup -o json
$privateEndpoints = $peJson | ConvertFrom-Json
if (-not $privateEndpoints -or $privateEndpoints.Count -eq 0) {
    Write-Host "[dns-link] No private endpoints found in resource group $peResourceGroup."
    exit 0
}

$linkCount = 0
$skipCount = 0

function Ensure-Link($endpointName, $groupId, $zoneId, $peResourceGroup, $peSubscription) {
    $zoneName = ($zoneId.Split('/') | Select-Object -Last 1)
    $groupLabel = $zoneName -replace '[^a-z0-9-]', '-' 
    $zoneGroupName = "manual-$groupId-$groupLabel"

    $existingJson = az network private-endpoint dns-zone-group list `
        --subscription $peSubscription `
        --resource-group $peResourceGroup `
        --endpoint-name $endpointName `
        -o json 2>$null
    $existingGroups = $existingJson | ConvertFrom-Json
    if ($existingGroups) {
        foreach ($grp in $existingGroups) {
            foreach ($cfg in $grp.privateDnsZoneConfigs) {
                if ($cfg.privateDnsZoneId -and ($cfg.privateDnsZoneId.ToLower() -eq $zoneId.ToLower())) {
                    Write-Host "[dns-link] Private endpoint $endpointName already linked to $zoneName via ${($grp.name)}"
                    return $false
                }
            }
        }
    }

    $result = az network private-endpoint dns-zone-group create `
        --subscription $peSubscription `
        --resource-group $peResourceGroup `
        --endpoint-name $endpointName `
        --name $zoneGroupName `
        --private-dns-zone $zoneId `
        --zone-name $zoneName 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[dns-link] Linked $endpointName -> $zoneName (zone group $zoneGroupName)"
        return $true
    }

    Write-Warning "Failed to link $zoneName for $endpointName"
    return $false
}

foreach ($endpoint in $privateEndpoints) {
    $peName = $endpoint.name
    $connections = $endpoint.privateLinkServiceConnections
    if (-not $connections -or $connections.Count -eq 0) {
        Write-Host "[dns-link] Skipping $peName: no service connections found"
        $skipCount++
        continue
    }

    $groupIds = $connections[0].groupIds
    if (-not $groupIds -or $groupIds.Count -eq 0) {
        Write-Host "[dns-link] Skipping $peName: no groupIds present"
        $skipCount++
        continue
    }

    $groupId = $groupIds[0]
    $zoneId = $zoneIdMap[$groupId]
    if (-not $zoneId) {
        $expected = $expectedZones[$groupId]
        Write-Host "[dns-link] Skipping $peName: DNS zone for $groupId ($expected) not configured"
        $skipCount++
        continue
    }

    if (Ensure-Link -endpointName $peName -groupId $groupId -zoneId $zoneId -peResourceGroup $peResourceGroup -peSubscription $peSubscription) {
        $linkCount++
    } else {
        $skipCount++
    }
}

Write-Host "[dns-link] Summary: created $linkCount DNS zone group(s); skipped $skipCount endpoint(s)."
