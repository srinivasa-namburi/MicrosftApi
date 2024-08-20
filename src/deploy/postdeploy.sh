#!/bin/bash

# Read the environment and resource group names from environment variables
envName=$(echo $AZURE_CONTAINER_REGISTRY_ENDPOINT | cut -d'.' -f1 | cut -c4-)
resourceGroup="rg-$AZURE_ENV_NAME"
workloadProfileType="$AZURE_CAE_WORKLOAD_TYPE"

# Define the container apps and their desired instance counts
declare -A containerApps=(
    ["worker-documentgeneration"]=8
    ["worker-chat"]=4
    ["worker-documentingestion"]=4
    ["web-docgen"]=2
    ["api-main"]=2
    ["worker-scheduler"]=1
    ["worker-setupmanager"]=1
)

echo "Scaling container apps in $resourceGroup..."

# Loop through each container app and scale it
for app in "${!containerApps[@]}"
do
    instanceCount=${containerApps[$app]}

    # Only update the workload profile if the type is not 'consumption'
    if [ "$workloadProfileType" != "consumption" ]; then
        echo "Updating workload profile for $app... (setting to dedicated)"
        az containerapp update -n $app -g $resourceGroup --workload-profile-name dedicated > /dev/null
    else
        echo "Skipping moving $app to dedicated workload profile as workload type is set to consumption"
    fi

    echo "Scaling $app to $instanceCount instances..."
    az containerapp update -n $app -g $resourceGroup --min-replicas $instanceCount --max-replicas $instanceCount > /dev/null
done
