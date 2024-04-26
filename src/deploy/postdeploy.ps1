# Read the environment and resource group names from environment variables
$envName = $env:AZURE_CONTAINER_REGISTRY_ENDPOINT.Split('.')[0].Substring(3)
$resourceGroup = "rg-"+$env:AZURE_ENV_NAME

# Define the container apps and their desired instance counts
$containerApps = @{
    "worker-documentgeneration" = 8
    "worker-chat" = 4
    "web-docgen" = 2
    "api-main" = 2
    "redis" = 1
    "worker-documentingestion" = 1
    "worker-scheduler" = 1
    "worker-setupmanager" = 1
}


Write-Host "Scaling container apps in $resourceGroup..."

# Loop through each container app and scale it
foreach ($app in $containerApps.Keys) {
    $instanceCount = $containerApps[$app]
    Write-Host "Moving $app to dedicated workload profile from consumption profile"
    az containerapp update -n $app -g $resourceGroup --workload-profile-name dedicated > $null
    Write-Host "Scaling $app to $instanceCount instances..."
    az containerapp update -n $app -g $resourceGroup --min-replicas $instanceCount --max-replicas $instanceCount > $null
}

Write-Host "Scaling REDIS CPU to 1 core and 4Gi memory..."
az containerapp update -n redis -g $resourceGroup --cpu 1 --memory 4Gi > $null