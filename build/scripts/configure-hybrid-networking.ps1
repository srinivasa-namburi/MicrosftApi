# Post-deployment script to configure private endpoints for hybrid mode
# Run this AFTER the Azure resources are deployed but BEFORE Helm deployment

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$SubnetPEResourceId,

    [Parameter(Mandatory=$false)]
    [string]$DNSZoneResourceGroup = "rg-network-hub"
)

Write-Host "Configuring private endpoints for hybrid deployment..." -ForegroundColor Cyan

# Get all resources that need private endpoints
$resources = @(
    @{Type="Microsoft.Sql/servers"; SubResource="sqlServer"; DNSZone="privatelink.database.windows.net"},
    @{Type="Microsoft.Cache/redis"; SubResource="redisCache"; DNSZone="privatelink.redis.cache.windows.net"},
    @{Type="Microsoft.Storage/storageAccounts"; SubResource="blob"; DNSZone="privatelink.blob.core.windows.net"},
    @{Type="Microsoft.Search/searchServices"; SubResource="searchService"; DNSZone="privatelink.search.windows.net"},
    @{Type="Microsoft.SignalRService/signalR"; SubResource="signalr"; DNSZone="privatelink.service.signalr.net"}
)

foreach ($resource in $resources) {
    Write-Host "Processing $($resource.Type)..." -ForegroundColor Yellow

    # Get resources of this type
    $items = az resource list --resource-group $ResourceGroup --resource-type $resource.Type --query "[].{name:name, id:id}" | ConvertFrom-Json

    foreach ($item in $items) {
        $peName = "pe-$($item.name)"
        Write-Host "  Creating private endpoint: $peName"

        # Create private endpoint
        az network private-endpoint create `
            --name $peName `
            --resource-group $ResourceGroup `
            --subnet $SubnetPEResourceId `
            --private-connection-resource-id $item.id `
            --group-id $resource.SubResource `
            --connection-name "conn-$($item.name)" `
            --output none

        # Get private endpoint IP
        $peIP = az network private-endpoint show `
            --name $peName `
            --resource-group $ResourceGroup `
            --query "customDnsConfigs[0].ipAddresses[0]" -o tsv

        # Create DNS A record
        if ($peIP) {
            Write-Host "  Creating DNS record for IP: $peIP"

            # Extract the resource name for DNS
            $dnsRecordName = $item.name

            try {
                az network private-dns record-set a add-record `
                    --resource-group $DNSZoneResourceGroup `
                    --zone-name $resource.DNSZone `
                    --record-set-name $dnsRecordName `
                    --ipv4-address $peIP `
                    --output none

                Write-Host "  ✓ DNS record created" -ForegroundColor Green
            } catch {
                Write-Warning "  Could not create DNS record. May need manual configuration."
            }
        }
    }
}

Write-Host "`nPrivate endpoint configuration complete!" -ForegroundColor Green
Write-Host "Note: You may need to manually verify DNS resolution from within the VNET" -ForegroundColor Yellow