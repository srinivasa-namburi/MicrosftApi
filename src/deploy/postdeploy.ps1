# Read the environment and resource group names from environment variables
$envName = $env:AZURE_CONTAINER_REGISTRY_ENDPOINT.Split('.')[0].Substring(3)
$resourceGroup = "rg-"+$env:AZURE_ENV_NAME
$workloadProfileType = $env:AZURE_CAE_WORKLOAD_TYPE

# Define the container apps and their desired instance counts
$containerApps = @{
    "worker-documentgeneration" = 8
    "worker-chat" = 4
    "web-docgen" = 2
    "api-main" = 2
    "worker-documentingestion" = 4
    "worker-scheduler" = 1
    "worker-setupmanager" = 1
}

Write-Host "Scaling container apps in $resourceGroup..."

# Loop through each container app and scale it
foreach ($app in $containerApps.Keys) {
    $instanceCount = $containerApps[$app]

    # Only update the workload profile if the type is not 'consumption'
    if ($workloadProfileType -ne "consumption") {
        Write-Host "Moving $app to dedicated workload profile from consumption profile"
        az containerapp update -n $app -g $resourceGroup --workload-profile-name dedicated > $null
    } else {
        Write-Host "Skipping moving $app to dedicated workload profile as workload type is set to consumption"
    }

    Write-Host "Scaling $app to $instanceCount instances..."
    az containerapp update -n $app -g $resourceGroup --min-replicas $instanceCount --max-replicas $instanceCount > $null
}
