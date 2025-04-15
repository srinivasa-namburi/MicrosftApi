#!/bin/bash

# Configuration variables
DEFAULT_DOCUMENT_WORKERS=4

# Read the environment and resource group names from environment variables
envName=$(echo $AZURE_CONTAINER_REGISTRY_ENDPOINT | cut -d'.' -f1 | cut -c4-)
resourceGroup="$AZURE_RESOURCE_GROUP"
workloadProfileType="$AZURE_CAE_WORKLOAD_TYPE"
containerAppEnvName="env-$envName"

if [ "$SKIP_POSTDEPLOY" == "true" ]; then
    # We're not scaling the db-setupmanager container app, but we do need to set the environment variable for it
    echo "Setting environment variable GREENLIGHT_PRODUCTION=true for db-setupmanager..." >&2
    az containerapp update -n db-setupmanager -g $resourceGroup --set-env-vars GREENLIGHT_PRODUCTION=true > /dev/null
    # And then restart the app to apply the environment variable
    echo "Restarting db-setupmanager to apply environment variable..." >&2
    az containerapp revision copy -n db-setupmanager -g $resourceGroup > /dev/null
    echo "Skipping post-deploy script execution as SKIP_POSTDEPLOY is set to true."
    exit 0
fi

# Define the container apps and their desired instance counts (initial values)
declare -A containerApps=(
    ["web-docgen"]=1
    ["api-main"]=1
    ["silo"]=4 # Min replicas for silo
)

echo "Scaling container apps in $resourceGroup..." >&2

# Determine CPU and memory settings based on workload profile
cpuValue=1
memoryValue="1Gi"

if [ "$workloadProfileType" != "consumption" ]; then
    # Query the workload profile details
    echo "Querying workload profile type..." >&2
    workloadProfileSku=$(az containerapp env workload-profile show -g "$resourceGroup" -n "$containerAppEnvName" --workload-profile-name dedicated --query "sku.name" -o tsv 2>/dev/null || echo "Unknown")
    
    echo "Detected workload profile SKU: $workloadProfileSku" >&2
    
    # Set CPU and memory based on workload profile SKU
    if [ "$workloadProfileSku" == "D4" ]; then
        cpuValue=1
        memoryValue="1Gi"
        echo "Using D4 profile settings: 1 CPU, 1GB memory" >&2
    else
        cpuValue=2
        memoryValue="2Gi"
        echo "Using D8+ profile settings: 2 CPU, 2GB memory" >&2
    fi
fi

# Loop through each container app and scale it
for app in "${!containerApps[@]}"
do
    instanceCount=${containerApps[$app]}
    updateArgs=("--name" "$app" "--resource-group" "$resourceGroup" "--set-env-vars" "GREENLIGHT_PRODUCTION=true")

    # Add workload profile and scaling arguments
    if [ "$workloadProfileType" != "consumption" ]; then
        updateArgs+=("--workload-profile-name" "dedicated")
        if [ "$app" == "silo" ]; then
            updateArgs+=("--min-replicas" "$instanceCount" "--max-replicas" "10" "--cpu" "$cpuValue" "--memory" "$memoryValue")
        else
            updateArgs+=("--min-replicas" "$instanceCount" "--max-replicas" "$instanceCount")
        fi
    else
        if [ "$app" == "silo" ]; then
            updateArgs+=("--min-replicas" "$instanceCount" "--max-replicas" "10")
        else
            updateArgs+=("--min-replicas" "$instanceCount" "--max-replicas" "$instanceCount")
        fi
    fi

    # Run the update command in the background
    echo "Updating $app with arguments: ${updateArgs[*]}..." >&2
    az containerapp update "${updateArgs[@]}" > /dev/null &
done

# Wait for all background processes to complete
wait

# Cleanup unnecessary container apps
echo "Checking for container apps to clean up..." >&2

# List of defunct services to remove
defunctServices=(
    "worker-chat"
    "worker-validation"
    "services-setupmanager"
    "worker-documentgeneration"
    "worker-scheduler",
    "worker-documentingestion"
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
