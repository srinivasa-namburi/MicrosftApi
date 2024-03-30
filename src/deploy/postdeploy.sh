#!/bin/bash
# Read the environment and resource group names from environment variables
envName=$(echo $AZURE_CONTAINER_REGISTRY_ENDPOINT | cut -d'.' -f1 | cut -c4-)
resourceGroup="rg-$AZURE_ENV_NAME"

# Define the container apps and their desired instance counts
declare -A containerApps=(
    ["worker-documentgeneration"]=8
    ["worker-chat"]=4
    ["worker-documentingestion"]=4
)

echo "Scaling container apps in $resourceGroup..."

# Loop through each container app and scale it
for app in "${!containerApps[@]}"
do
    instanceCount=${containerApps[$app]}
    echo "Scaling $app to $instanceCount instances..."
    az containerapp update -n $app -g $resourceGroup --min-replicas $instanceCount --max-replicas $instanceCount > /dev/null
done
