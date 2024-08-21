# Configuration variables
$TOKENS_PER_WORKER = 25000

# Read the environment and resource group names from environment variables
$envName = $env:AZURE_CONTAINER_REGISTRY_ENDPOINT.Split('.')[0].Substring(3)
$resourceGroup = "rg-$($env:AZURE_ENV_NAME)"
$workloadProfileType = $env:AZURE_CAE_WORKLOAD_TYPE
$openai_subscription_id = $env:AZURE_SUBSCRIPTION_ID

# Check if PVICO_OPENAI_CONNECTIONSTRING is set
if ([string]::IsNullOrEmpty($env:PVICO_OPENAI_CONNECTIONSTRING)) {
    Write-Warning "PVICO_OPENAI_CONNECTIONSTRING is not set. Using default number of workers."
    $use_default_workers = $true
} else {
    # Parse the OpenAI connection string
    $openai_endpoint = $env:PVICO_OPENAI_CONNECTIONSTRING -replace '.*Endpoint=([^;]*);.*', '$1'
    $openai_key = $env:PVICO_OPENAI_CONNECTIONSTRING -replace '.*Key=([^;]*);.*', '$1'
}

# Check if PVICO_OPENAI_RESOURCEGROUP is set
if ([string]::IsNullOrEmpty($env:PVICO_OPENAI_RESOURCEGROUP)) {
    Write-Warning "PVICO_OPENAI_RESOURCEGROUP is not set. Using default number of workers."
    $use_default_workers = $true
} else {
    $openaiResourceGroup = $env:PVICO_OPENAI_RESOURCEGROUP
}

# Define the container apps and their desired instance counts (initial values)
$containerApps = @{
    "worker-documentgeneration" = 8  # Default value
    "worker-chat" = 4
    "worker-documentingestion" = 4
    "web-docgen" = 2
    "api-main" = 2
    "worker-scheduler" = 1
    "worker-setupmanager" = 1
}

# Function to get the TPM for a specific deployment
function Get-TPM {
    param (
        [string]$deploymentName
    )
    
    $url = "https://management.azure.com/subscriptions/$openai_subscription_id/resourceGroups/$openaiResourceGroup/providers/Microsoft.CognitiveServices/accounts/oai-projectvico-eastus2/deployments/$deploymentName`?api-version=2023-10-01-preview"
    
    Write-Host "Calling URL: $url"
    
    $response = az rest --method get --url $url | ConvertFrom-Json

    $tpm_count = $response.properties.rateLimits | Where-Object { $_.key -eq "token" } | Select-Object -ExpandProperty count

    if ($tpm_count -match '^\d+$') {
        return [int]$tpm_count
    } else {
        Write-Warning "Error parsing the TPM count or no TPM count found. Defaulting to 0."
        return 0
    }
}

# Determine the number of workers based on TPM
if ($use_default_workers) {
    $documentGenerationWorkers = 8
} else {
    # Try to get TPM for gpt-4o first, then gpt-4-128k if gpt-4o is not found
    $tpm = Get-TPM "gpt-4o"
    if ($tpm -eq 0) {
        Write-Host "gpt-4o not found or no tokens available, trying gpt-4-128k..."
        $tpm = Get-TPM "gpt-4-128k"
    }

    # Display the number of tokens available
    if ($tpm -eq 0) {
        Write-Host "No tokens available for either gpt-4o or gpt-4-128k. Using default number of workers (8)."
        $documentGenerationWorkers = 8
    } else {
        # Calculate the number of document generation workers (rounded down)
        $documentGenerationWorkers = [math]::Floor($tpm / $TOKENS_PER_WORKER)
        if ($documentGenerationWorkers -lt 1) {
            $documentGenerationWorkers = 1
        }
        Write-Host "Available tokens per minute: $tpm"
        Write-Host "Calculated number of document generation workers: $documentGenerationWorkers (based on TPM / $TOKENS_PER_WORKER)"
    }
}

# Assign the calculated number of workers
$containerApps["worker-documentgeneration"] = $documentGenerationWorkers

Write-Host "Scaling container apps in $resourceGroup..."

# Loop through each container app and scale it
foreach ($app in $containerApps.Keys) {
    $instanceCount = $containerApps[$app]

    # Only update the workload profile if the type is not 'consumption'
    if ($workloadProfileType -ne "consumption") {
        Write-Host "Updating workload profile for $app... (setting to dedicated)"
        az containerapp update -n $app -g $resourceGroup --workload-profile-name dedicated | Out-Null
    } else {
        Write-Host "Skipping moving $app to dedicated workload profile as workload type is set to consumption"
    }

    Write-Host "Scaling $app to $instanceCount instances..."
    az containerapp update -n $app -g $resourceGroup --min-replicas $instanceCount --max-replicas $instanceCount | Out-Null
}