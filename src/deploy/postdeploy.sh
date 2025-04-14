#!/bin/bash

# Configuration variables
DEFAULT_DOCUMENT_WORKERS=4

# Read the environment and resource group names from environment variables
envName=$(echo $AZURE_CONTAINER_REGISTRY_ENDPOINT | cut -d'.' -f1 | cut -c4-)
resourceGroup="$AZURE_RESOURCE_GROUP"
workloadProfileType="$AZURE_CAE_WORKLOAD_TYPE"

if [ "$SKIP_POSTDEPLOY" == "true" ]; then
    echo "Skipping post-deploy script execution as SKIP_POSTDEPLOY is set to true."
    exit 0
fi

# Define the container apps and their desired instance counts (initial values)
declare -A containerApps=(
    ["worker-documentingestion"]=4
    ["web-docgen"]=1
    ["api-main"]=1
    ["worker-scheduler"]=1
    ["silo"]=4 # Min replicas for silo
)

echo "Scaling container apps in $resourceGroup..." >&2

# Loop through each container app and scale it
for app in "${!containerApps[@]}"
do
    instanceCount=${containerApps[$app]}

    # Add the environment variable GREENLIGHT_PRODUCTION=true
    echo "Setting environment variable GREENLIGHT_PRODUCTION=true for $app..." >&2
    az containerapp update -n $app -g $resourceGroup --set-env-vars GREENLIGHT_PRODUCTION=true > /dev/null

    # Only update the workload profile if the type is not 'consumption'
    if [ "$workloadProfileType" != "consumption" ]; then
        echo "Updating workload profile for $app... (setting to dedicated)" >&2
        az containerapp update -n $app -g $resourceGroup --workload-profile-name dedicated > /dev/null
    else
        echo "Skipping moving $app to dedicated workload profile as workload type is set to consumption" >&2
    fi

    # Set min and max replicas for the silo container
    if [ "$app" == "silo" ]; then
        echo "Scaling $app to min replicas $instanceCount and max replicas 10..." >&2
        az containerapp update -n $app -g $resourceGroup --min-replicas $instanceCount --max-replicas 10 > /dev/null
    else
        echo "Scaling $app to $instanceCount instances..." >&2
        az containerapp update -n $app -g $resourceGroup --min-replicas $instanceCount --max-replicas $instanceCount > /dev/null
    fi
done

# Cleanup unnecessary container apps
echo "Checking for container apps to clean up..." >&2

# List of defunct services to remove
defunctServices=(
    "worker-chat"
    "worker-validation"
    "services-setupmanager"
    "worker-documentgeneration" # Newly added defunct service
)

# Loop through defunct services and remove them if they exist
for service in "${defunctServices[@]}"
do
    if az containerapp show --name "$service" --resource-group "$resourceGroup" &>/dev/null; then
        echo "Removing $service container app as it is no longer used..." >&2
        az containerapp delete --name "$service" --resource-group "$resourceGroup" --yes
        echo "$service container app removed successfully" >&2
    fi
done
