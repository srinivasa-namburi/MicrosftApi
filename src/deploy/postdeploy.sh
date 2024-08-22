#!/bin/bash

# Configuration variables
TOKENS_PER_WORKER=30000

# Read the environment and resource group names from environment variables
envName=$(echo $AZURE_CONTAINER_REGISTRY_ENDPOINT | cut -d'.' -f1 | cut -c4-)
resourceGroup="rg-$AZURE_ENV_NAME"
workloadProfileType="$AZURE_CAE_WORKLOAD_TYPE"
openai_subscription_id="$AZURE_SUBSCRIPTION_ID"

# Check if PVICO_OPENAI_CONNECTIONSTRING is set
if [ -z "$PVICO_OPENAI_CONNECTIONSTRING" ]; then
    echo "PVICO_OPENAI_CONNECTIONSTRING is not set. Using default number of workers." >&2
    use_default_workers=true
else
    # Parse the OpenAI connection string
    openai_endpoint=$(echo $PVICO_OPENAI_CONNECTIONSTRING | sed 's/.*Endpoint=\([^;]*\);.*/\1/' | sed 's#https://##')
    openai_key=$(echo $PVICO_OPENAI_CONNECTIONSTRING | sed 's/.*Key=\([^;]*\);.*/\1/')
    openai_instance_name=$(echo $openai_endpoint | cut -d'.' -f1)
fi

# Check if PVICO_OPENAI_RESOURCEGROUP is set
if [ -z "$PVICO_OPENAI_RESOURCEGROUP" ]; then
    echo "PVICO_OPENAI_RESOURCEGROUP is not set. Using default number of workers." >&2
    use_default_workers=true
else
    openaiResourceGroup="$PVICO_OPENAI_RESOURCEGROUP"
fi

# Define the container apps and their desired instance counts (initial values)
declare -A containerApps=(
    ["worker-documentgeneration"]=8  # Default value
    ["worker-chat"]=4
    ["worker-documentingestion"]=4
    ["web-docgen"]=2
    ["api-main"]=2
    ["worker-scheduler"]=1
    ["worker-setupmanager"]=1
)

# Function to get the TPM for a specific deployment
get_tpm() {
    local deploymentName=$1
    local url="https://management.azure.com/subscriptions/$openai_subscription_id/resourceGroups/$openaiResourceGroup/providers/Microsoft.CognitiveServices/accounts/$openai_instance_name/deployments/$deploymentName?api-version=2023-10-01-preview"
    
    echo "Calling URL: $url" >&2
    
    local response=$(az rest --method get --url "$url")

    local tpm_count=$(echo "$response" | jq -r '.properties.rateLimits[] | select(.key=="token").count')

    if [[ "$tpm_count" =~ ^[0-9]+$ ]]; then
        echo "$tpm_count"
    else
        echo "Error parsing the TPM count or no TPM count found. Defaulting to 0." >&2
        echo "0"
    fi
}

# Determine the number of workers based on TPM
if [ "$use_default_workers" = true ]; then
    documentGenerationWorkers=8
else
    # Try to get TPM for gpt-4o first, then gpt-4-128k if gpt-4o is not found
    tpm=$(get_tpm "gpt-4o")
    if [ -z "$tpm" ] || [ "$tpm" -eq 0 ]; then
        echo "gpt-4o not found or no tokens available, trying gpt-4-128k..." >&2
        tpm=$(get_tpm "gpt-4-128k")
    fi

    # Display the number of tokens available
    if [ -z "$tpm" ] || [ "$tpm" -eq 0 ]; then
        echo "No tokens available for either gpt-4o or gpt-4-128k. Using default number of workers (8)." >&2
        documentGenerationWorkers=8
    else
        # Calculate the number of document generation workers (rounded down)
        documentGenerationWorkers=$((tpm / TOKENS_PER_WORKER))
        if [ "$documentGenerationWorkers" -lt 1 ]; then
            documentGenerationWorkers=1
        fi
        echo "Available tokens per minute: $tpm" >&2
        echo "Calculated number of document generation workers: $documentGenerationWorkers (based on TPM / $TOKENS_PER_WORKER)" >&2
    fi
fi

# Assign the calculated number of workers
containerApps["worker-documentgeneration"]=$documentGenerationWorkers

echo "Scaling container apps in $resourceGroup..." >&2

# Loop through each container app and scale it
for app in "${!containerApps[@]}"
do
    instanceCount=${containerApps[$app]}

    # Only update the workload profile if the type is not 'consumption'
    if [ "$workloadProfileType" != "consumption" ]; then
        echo "Updating workload profile for $app... (setting to dedicated)" >&2
        az containerapp update -n $app -g $resourceGroup --workload-profile-name dedicated > /dev/null
    else
        echo "Skipping moving $app to dedicated workload profile as workload type is set to consumption" >&2
    fi

    echo "Scaling $app to $instanceCount instances..." >&2
    az containerapp update -n $app -g $resourceGroup --min-replicas $instanceCount --max-replicas $instanceCount > /dev/null
done