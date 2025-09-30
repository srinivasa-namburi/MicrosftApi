#!/usr/bin/env bash
set -euo pipefail

# Build and push Docker images for Aspire projects to ACR - PARALLEL VERSION
# This builds all images in parallel for much faster execution
# Usage: build-modern-build-push-images-parallel.sh <acr-name> <source-dir>

ACR_NAME=${1:?Missing ACR name}
SOURCE_DIR=${2:-$(pwd)}

# Validate required environment variables
if [ -z "${AZURE_RESOURCE_GROUP}" ]; then
    echo "[docker-build] ERROR: AZURE_RESOURCE_GROUP environment variable is not set"
    exit 1
fi

echo "[docker-build] Building and pushing images to ACR: $ACR_NAME (PARALLEL MODE)"
echo "[docker-build] Resource Group: $AZURE_RESOURCE_GROUP"

ACR_LOGIN_SERVER="${ACR_NAME}.azurecr.io"
echo "[docker-build] ACR login server: $ACR_LOGIN_SERVER"

# Ensure ACR exists and create if needed (BEFORE trying to login)
echo "[docker-build] Checking if ACR exists in resource group: ${AZURE_RESOURCE_GROUP}"
az acr show --name "$ACR_NAME" --resource-group "${AZURE_RESOURCE_GROUP}" 2>/dev/null || {
    echo "[docker-build] ACR doesn't exist, creating: $ACR_NAME in resource group: ${AZURE_RESOURCE_GROUP}"
    az acr create --name "$ACR_NAME" \
        --resource-group "${AZURE_RESOURCE_GROUP}" \
        --sku Basic \
        --admin-enabled true || {
        echo "[docker-build] ERROR: Failed to create ACR"
        exit 1
    }
    echo "[docker-build] ACR created successfully"
}

# ACR login (AFTER ensuring it exists)
echo "[docker-build] Logging into ACR..."
az acr login --name "$ACR_NAME" 2>/dev/null || {
    echo "[docker-build] ERROR: Failed to login to ACR $ACR_NAME"
    exit 1
}

# Attach ACR to AKS cluster if AKS exists
AKS_NAME="${AKS_CLUSTER_NAME:-aks-${AZURE_RESOURCE_GROUP}}"
echo "[docker-build] Checking for AKS cluster: $AKS_NAME in resource group: ${AZURE_RESOURCE_GROUP}"
if az aks show --name "$AKS_NAME" --resource-group "${AZURE_RESOURCE_GROUP}" 2>/dev/null; then
    echo "[docker-build] Attaching ACR to AKS cluster..."
    az aks update --name "$AKS_NAME" \
        --resource-group "${AZURE_RESOURCE_GROUP}" \
        --attach-acr "$ACR_NAME" 2>/dev/null || {
        echo "[docker-build] WARNING: Could not attach ACR to AKS (may already be attached)"
    }
else
    echo "[docker-build] AKS cluster not found, skipping ACR attachment"
fi

# Grant workload identity ACR pull permissions if configured
if [ -n "${WORKLOAD_IDENTITY_PRINCIPAL_ID:-}" ]; then
    echo "[docker-build] Granting AcrPull role to workload identity principal: $WORKLOAD_IDENTITY_PRINCIPAL_ID"
    ACR_RESOURCE_ID=$(az acr show --name "$ACR_NAME" --resource-group "${AZURE_RESOURCE_GROUP}" --query id -o tsv)

    az role assignment create \
        --assignee "$WORKLOAD_IDENTITY_PRINCIPAL_ID" \
        --role "AcrPull" \
        --scope "$ACR_RESOURCE_ID" 2>/dev/null || {
        echo "[docker-build] WARNING: Could not grant AcrPull role (may already exist)"
    }

    echo "[docker-build] Workload identity can now pull images from ACR"
else
    echo "[docker-build] No workload identity principal ID provided - skipping workload identity ACR permissions"
    echo "[docker-build] Note: Kubelet identity should still have access via --attach-acr"
fi

# Use build number as tag, fallback to date if not provided
IMAGE_TAG="${BUILD_BUILDNUMBER:-$(date +%Y%m%d-%H%M%S)}"
echo "[docker-build] Using image tag: $IMAGE_TAG"

# Pre-pull base images to ensure they're cached (speeds up builds)
echo "[docker-build] Pre-pulling base runtime images for better cache performance..."
docker pull mcr.microsoft.com/dotnet/sdk:9.0 2>/dev/null &
docker pull mcr.microsoft.com/dotnet/aspnet:9.0 2>/dev/null &
PULL_SDK=$!
PULL_ASPNET=$!

# Continue with setup while images pull in background

# List of services to build
SERVICES=(
    "db-setupmanager:src/Microsoft.Greenlight.SetupManager.DB"
    "api-main:src/Microsoft.Greenlight.API.Main"
    "mcp-server:src/Microsoft.Greenlight.McpServer"
    "silo:src/Microsoft.Greenlight.Silo"
    "web-docgen:src/Microsoft.Greenlight.Web.DocGen"
)

# Wait for base image pulls to complete (if not already done)
echo "[docker-build] Waiting for base image pulls to complete..."
wait $PULL_SDK 2>/dev/null || true
wait $PULL_ASPNET 2>/dev/null || true
echo "[docker-build] Base images ready"

# Create a temporary directory for build logs
LOG_DIR=$(mktemp -d)
echo "[docker-build] Build logs directory: $LOG_DIR"

# Function to build a single image
build_image() {
    local IMAGE_NAME=$1
    local PROJECT_PATH=$2
    local LOG_FILE="$LOG_DIR/${IMAGE_NAME}.log"

    echo "[docker-build] Starting build: $IMAGE_NAME"

    # Full project path
    FULL_PROJECT_PATH="$SOURCE_DIR/$PROJECT_PATH"

    if [ ! -d "$FULL_PROJECT_PATH" ]; then
        echo "[docker-build] ERROR: Project directory not found: $FULL_PROJECT_PATH" | tee "$LOG_FILE"
        return 1
    fi

    # Build using dotnet publish with container
    {
        echo "[docker-build] Publishing container for $IMAGE_NAME at $(date +%H:%M:%S)..."
        dotnet publish "$FULL_PROJECT_PATH" \
            --os linux \
            --arch x64 \
            /t:PublishContainer \
            /p:ContainerRegistry="$ACR_LOGIN_SERVER" \
            /p:ContainerImageName="$IMAGE_NAME" \
            /p:ContainerImageTag="$IMAGE_TAG" \
            /p:ContainerImageTag="latest" \
            -c Release \
            --verbosity minimal 2>&1

        local EXIT_CODE=$?
        if [ $EXIT_CODE -eq 0 ]; then
            echo "[docker-build] SUCCESS: $IMAGE_NAME pushed to $ACR_LOGIN_SERVER at $(date +%H:%M:%S)"
        else
            echo "[docker-build] FAILED: $IMAGE_NAME build failed with exit code $EXIT_CODE"
        fi
        return $EXIT_CODE
    } | tee "$LOG_FILE"
}

# Export functions and variables for parallel execution
export -f build_image
export SOURCE_DIR ACR_LOGIN_SERVER IMAGE_TAG LOG_DIR

# Build all images in parallel
echo "[docker-build] Starting parallel builds for ${#SERVICES[@]} images..."
echo "[docker-build] ============================================"

# Track PIDs for all background jobs
PIDS=()

# Start all builds in background
for SERVICE_ENTRY in "${SERVICES[@]}"; do
    IFS=':' read -r IMAGE_NAME PROJECT_PATH <<< "$SERVICE_ENTRY"

    # Run build in background
    build_image "$IMAGE_NAME" "$PROJECT_PATH" &
    PIDS+=($!)
done

# Wait for all builds to complete and check their status
echo "[docker-build] Waiting for all builds to complete..."
FAILED_BUILDS=()
SUCCESS_COUNT=0

for i in "${!PIDS[@]}"; do
    PID=${PIDS[$i]}
    SERVICE_ENTRY=${SERVICES[$i]}
    IFS=':' read -r IMAGE_NAME PROJECT_PATH <<< "$SERVICE_ENTRY"

    if wait $PID; then
        echo "[docker-build] ✓ $IMAGE_NAME completed successfully"
        ((SUCCESS_COUNT++))
    else
        echo "[docker-build] ✗ $IMAGE_NAME failed"
        FAILED_BUILDS+=("$IMAGE_NAME")
        # Show last 10 lines of the failed build log
        if [ -f "$LOG_DIR/${IMAGE_NAME}.log" ]; then
            echo "[docker-build] Last 10 lines of $IMAGE_NAME build log:"
            tail -10 "$LOG_DIR/${IMAGE_NAME}.log" | sed 's/^/  /'
        fi
    fi
done

echo "[docker-build] ============================================"
echo "[docker-build] Build summary: $SUCCESS_COUNT/${#SERVICES[@]} succeeded"

# Clean up log directory
rm -rf "$LOG_DIR"

# Exit with error if any builds failed
if [ ${#FAILED_BUILDS[@]} -gt 0 ]; then
    echo "[docker-build] ERROR: The following builds failed: ${FAILED_BUILDS[*]}"
    exit 1
fi

# Export ACR endpoint for Helm deployment
export AZURE_CONTAINER_REGISTRY_ENDPOINT="$ACR_LOGIN_SERVER"
echo "[docker-build] Exported AZURE_CONTAINER_REGISTRY_ENDPOINT=$ACR_LOGIN_SERVER"

echo "[docker-build] All images built and pushed successfully (parallel execution complete)"