#!/usr/bin/env bash
set -euo pipefail

# Build and push Docker images for Aspire projects to ACR - PARALLEL VERSION
# This builds all images in parallel for much faster execution
# Usage: build-modern-build-push-images-parallel.sh <acr-name> <source-dir>

cleanup_jobs() {
    local remaining
    remaining=$(jobs -p 2>/dev/null || true)
    if [ -n "$remaining" ]; then
        wait $remaining || true
    fi
    # Kill any child dotnet processes that might be lingering
    pkill -P $$ dotnet 2>/dev/null || true
    # Ensure cleanup always succeeds so it doesn't override the script's exit code
    return 0
}

trap cleanup_jobs EXIT

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

# Ensure docker socket is accessible (ADO/GitHub parity)
if ! docker ps >/dev/null 2>&1; then
    sudo systemctl start docker 2>/dev/null || true
    sudo chmod 666 /var/run/docker.sock 2>/dev/null || true
fi

# ACR login (AFTER ensuring it exists)
echo "[docker-build] Logging into ACR..."
az acr login --name "$ACR_NAME" 2>/dev/null || {
    echo "[docker-build] ERROR: Failed to login to ACR $ACR_NAME"
    exit 1
}

# Attach ACR to AKS cluster if AKS exists (skip if already attached)
AKS_RG="${AKS_RESOURCE_GROUP:-${AZURE_RESOURCE_GROUP}}"
AKS_NAME="${AKS_CLUSTER_NAME:-aks-${AKS_RG}}"
echo "[docker-build] Checking for AKS cluster: $AKS_NAME in resource group: ${AKS_RG}"
if az aks show --name "$AKS_NAME" --resource-group "$AKS_RG" >/dev/null 2>&1; then
    if az aks check-acr --name "$AKS_NAME" --resource-group "$AKS_RG" --acr "${ACR_LOGIN_SERVER}" >/dev/null 2>&1; then
        echo "[docker-build] ACR already attached to AKS cluster"
    else
        echo "[docker-build] Attaching ACR to AKS cluster..."
        az aks update --name "$AKS_NAME" \
            --resource-group "$AKS_RG" \
            --attach-acr "$ACR_NAME" >/dev/null 2>&1 || {
            echo "[docker-build] WARNING: Could not attach ACR to AKS (may already be attached)"
        }
    fi
else
    echo "[docker-build] AKS cluster not found, skipping ACR attachment"
fi

# Verify kubelet identity has ACR pull permissions
# Note: Workload identity is for pod-to-Azure authentication, NOT for pulling container images
# The kubelet identity (used by AKS nodes) needs AcrPull access
echo "[docker-build] Verifying kubelet identity has AcrPull access to ACR..."

AKS_RG="${AKS_RESOURCE_GROUP:-${AZURE_RESOURCE_GROUP}}"
AKS_NAME="${AKS_CLUSTER_NAME:-aks-${AKS_RG}}"

if az aks show --name "$AKS_NAME" --resource-group "$AKS_RG" >/dev/null 2>&1; then
    # Get kubelet identity object ID from AKS cluster
    KUBELET_IDENTITY_OBJECT_ID=$(az aks show --name "$AKS_NAME" --resource-group "$AKS_RG" \
        --query identityProfile.kubeletidentity.objectId -o tsv 2>/dev/null || echo "")

    if [[ -n "$KUBELET_IDENTITY_OBJECT_ID" && "$KUBELET_IDENTITY_OBJECT_ID" != "null" ]]; then
        echo "[docker-build] Found kubelet identity: $KUBELET_IDENTITY_OBJECT_ID"

        # Get ACR resource ID
        ACR_RESOURCE_ID=$(az acr show --name "$ACR_NAME" --resource-group "${AZURE_RESOURCE_GROUP}" \
            --query id -o tsv 2>/dev/null || echo "")

        if [[ -n "$ACR_RESOURCE_ID" ]]; then
            # Check if kubelet identity already has AcrPull role
            EXISTING_ROLE=$(az role assignment list \
                --assignee "$KUBELET_IDENTITY_OBJECT_ID" \
                --scope "$ACR_RESOURCE_ID" \
                --role "AcrPull" \
                --query "[0].id" -o tsv 2>/dev/null || echo "")

            if [[ -n "$EXISTING_ROLE" ]]; then
                echo "[docker-build] ✓ Kubelet identity already has AcrPull access to ACR"
            else
                echo "[docker-build] WARNING: Kubelet identity does not have AcrPull role on ACR"
                echo "[docker-build] WARNING: Attempting to grant AcrPull role..."

                if az role assignment create \
                    --assignee "$KUBELET_IDENTITY_OBJECT_ID" \
                    --role "AcrPull" \
                    --scope "$ACR_RESOURCE_ID" \
                    --output none 2>/dev/null; then
                    echo "[docker-build] ✓ Successfully granted AcrPull to kubelet identity"
                else
                    echo "[docker-build] ERROR: Could not grant AcrPull role to kubelet identity"
                    echo "[docker-build] ERROR: Image pulls from AKS will likely fail!"
                    echo "[docker-build] ERROR: Manual fix required - run this command:"
                    echo "[docker-build]   az role assignment create \\"
                    echo "[docker-build]     --assignee $KUBELET_IDENTITY_OBJECT_ID \\"
                    echo "[docker-build]     --role AcrPull \\"
                    echo "[docker-build]     --scope $ACR_RESOURCE_ID"
                    echo "[docker-build] Continuing anyway..."
                fi
            fi
        else
            echo "[docker-build] WARNING: Could not get ACR resource ID - skipping role verification"
            echo "[docker-build] NOTE: Image pulls may fail if kubelet identity lacks AcrPull access"
        fi
    else
        echo "[docker-build] WARNING: Could not determine kubelet identity from AKS cluster"
        echo "[docker-build] NOTE: Relying on --attach-acr from earlier step to grant access"
    fi
else
    echo "[docker-build] INFO: AKS cluster not found - skipping kubelet identity verification"
    echo "[docker-build] NOTE: This is expected if cluster hasn't been provisioned yet"
fi

echo "[docker-build] Kubelet identity verification complete"

# Use build number as tag, fallback to date if not provided
RAW_IMAGE_TAG="${BUILD_BUILDNUMBER:-$(date +%Y%m%d-%H%M%S)}"
IMAGE_TAG=$(echo "$RAW_IMAGE_TAG" | tr '[:upper:]' '[:lower:]' | tr -c 'a-z0-9-' '-' | sed 's/-\+/\-/g' | sed 's/^-//;s/-$//')
if [[ -z "$IMAGE_TAG" ]]; then
    IMAGE_TAG=$(date +%Y%m%d-%H%M%S)
fi
echo "[docker-build] Using image tag: $IMAGE_TAG"

# Handle Azure Artifacts authentication for Docker builds
if [ -z "${INTERNAL_MICROSOFT:-}" ] || [ "${INTERNAL_MICROSOFT}" != "true" ]; then
    echo "[docker-build] INTERNAL_MICROSOFT not set - removing Azure Artifacts config for external/customer builds"
    # Replace Azure Artifacts config with public NuGet.org only
    cat > "$SOURCE_DIR/src/Nuget.ADOArtifacts.config" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF
    NUGET_BUILD_ARGS=""
else
    echo "[docker-build] INTERNAL_MICROSOFT=true - using Azure Artifacts with System.AccessToken authentication"
    # Set build arg for Docker to pass authentication token
    # System.AccessToken is provided by Azure DevOps pipeline
    NUGET_PAT="${SYSTEM_ACCESSTOKEN:-}"
    if [ -z "$NUGET_PAT" ]; then
        echo "[docker-build] ERROR: INTERNAL_MICROSOFT=true but SYSTEM_ACCESSTOKEN not available"
        exit 1
    fi
    NUGET_BUILD_ARGS="--build-arg NUGET_PAT=${NUGET_PAT}"
    echo "[docker-build] Will pass Azure Artifacts credentials to Docker builds"
fi

# Build base MCP execution environment image first (used by multiple services)
echo "[docker-build] Building base MCP execution environment image..."
MCP_BASE_IMAGE="${ACR_LOGIN_SERVER}/dotnet-aspnet-mcp:9.0"
MCP_BASE_DOCKERFILE="$SOURCE_DIR/build/docker/Dockerfile.aspnet-mcp"

if [ -f "$MCP_BASE_DOCKERFILE" ]; then
    # Try to pull existing image from ACR for layer caching (speeds up builds significantly)
    echo "[docker-build] Attempting to pull existing MCP base image for cache..."
    if docker pull "$MCP_BASE_IMAGE" 2>/dev/null; then
        echo "[docker-build] ✓ Pulled existing image, will use cached layers"
    else
        echo "[docker-build] No existing image found in ACR, will build from scratch"
    fi

    echo "[docker-build] Building $MCP_BASE_IMAGE from $MCP_BASE_DOCKERFILE"
    docker build \
        -t "$MCP_BASE_IMAGE" \
        -f "$MCP_BASE_DOCKERFILE" \
        --cache-from "$MCP_BASE_IMAGE" \
        "$SOURCE_DIR/build/docker/" || {
        echo "[docker-build] ERROR: Failed to build MCP base image"
        exit 1
    }

    echo "[docker-build] Pushing MCP base image to ACR..."
    docker push "$MCP_BASE_IMAGE" || {
        echo "[docker-build] ERROR: Failed to push MCP base image"
        exit 1
    }
    echo "[docker-build] MCP base image ready: $MCP_BASE_IMAGE"
else
    echo "[docker-build] WARNING: MCP base Dockerfile not found at $MCP_BASE_DOCKERFILE"
    echo "[docker-build] Will use standard aspnet:9.0 base image instead"
    MCP_BASE_IMAGE="mcr.microsoft.com/dotnet/aspnet:9.0"
fi

# Pre-pull base SDK image to ensure it's cached (speeds up builds)
echo "[docker-build] Pre-pulling base SDK image for better cache performance..."
docker pull mcr.microsoft.com/dotnet/sdk:9.0 2>/dev/null &
PULL_SDK=$!

# Continue with setup while images pull in background

# List of services to build
SERVICES=(
    "db-setupmanager:src/Microsoft.Greenlight.SetupManager.DB"
    "api-main:src/Microsoft.Greenlight.API.Main"
    "mcpserver-core:src/Microsoft.Greenlight.McpServer.Core"
    "mcpserver-flow:src/Microsoft.Greenlight.McpServer.Flow"
    "silo:src/Microsoft.Greenlight.Silo"
    "web-docgen:src/Microsoft.Greenlight.Web.DocGen"
)

# Wait for base SDK image pull to complete (if not already done)
echo "[docker-build] Waiting for base SDK image pull to complete..."
wait $PULL_SDK 2>/dev/null || true
echo "[docker-build] Base SDK image ready"

# Create a temporary directory for build logs
LOG_DIR=$(mktemp -d)
echo "[docker-build] Build logs directory: $LOG_DIR"

# Function to build a single image
build_image() {
    local IMAGE_NAME=$1
    local PROJECT_PATH=$2
    local LOG_FILE="$LOG_DIR/${IMAGE_NAME}.log"
    local STATUS_FILE="$LOG_DIR/${IMAGE_NAME}.status"

    echo "[docker-build] Starting build: $IMAGE_NAME"

    # Full project path
    FULL_PROJECT_PATH="$SOURCE_DIR/$PROJECT_PATH"

    if [ ! -d "$FULL_PROJECT_PATH" ]; then
        echo "[docker-build] ERROR: Project directory not found: $FULL_PROJECT_PATH"
        echo "1" > "$STATUS_FILE"
        return 1
    fi

    # Check for Dockerfile
    DOCKERFILE="$FULL_PROJECT_PATH/Dockerfile"
    if [ ! -f "$DOCKERFILE" ]; then
        echo "[docker-build] ERROR: Dockerfile not found at $DOCKERFILE"
        echo "1" > "$STATUS_FILE"
        return 1
    fi

    echo "[docker-build] Building container for $IMAGE_NAME at $(date +%H:%M:%S)..."
    echo "[docker-build]   Using Dockerfile: $PROJECT_PATH/Dockerfile"

    # Build image with Docker
    set +e
    docker build \
        -t "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG}" \
        -t "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:latest" \
        -f "$DOCKERFILE" \
        --build-arg MCP_BASE_IMAGE="${MCP_BASE_IMAGE}" \
        ${NUGET_BUILD_ARGS} \
        "$SOURCE_DIR" \
        2>&1 | tee -a "$LOG_FILE"

    local BUILD_EXIT_CODE=${PIPESTATUS[0]}
    set -e

    if [ $BUILD_EXIT_CODE -ne 0 ]; then
        echo "$BUILD_EXIT_CODE" > "$STATUS_FILE"
        echo "[docker-build] FAILED: $IMAGE_NAME build failed with exit code $BUILD_EXIT_CODE"
        return $BUILD_EXIT_CODE
    fi

    # Push both tags
    echo "[docker-build]   Pushing $IMAGE_NAME:${IMAGE_TAG}..."
    set +e
    docker push "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG}" 2>&1 | tee -a "$LOG_FILE"
    local PUSH1_EXIT_CODE=${PIPESTATUS[0]}

    docker push "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:latest" 2>&1 | tee -a "$LOG_FILE"
    local PUSH2_EXIT_CODE=${PIPESTATUS[0]}
    set -e

    if [ $PUSH1_EXIT_CODE -ne 0 ] || [ $PUSH2_EXIT_CODE -ne 0 ]; then
        local PUSH_EXIT_CODE=$((PUSH1_EXIT_CODE + PUSH2_EXIT_CODE))
        echo "$PUSH_EXIT_CODE" > "$STATUS_FILE"
        echo "[docker-build] FAILED: $IMAGE_NAME push failed"
        return $PUSH_EXIT_CODE
    fi

    echo "0" > "$STATUS_FILE"
    echo "[docker-build] SUCCESS: $IMAGE_NAME pushed to $ACR_LOGIN_SERVER at $(date +%H:%M:%S)"
    return 0
}

# Export functions and variables for parallel execution
export -f build_image
export SOURCE_DIR ACR_LOGIN_SERVER IMAGE_TAG LOG_DIR NUGET_BUILD_ARGS MCP_BASE_IMAGE

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

    # Disable exit-on-error for the wait check to handle failures gracefully
    set +e
    wait $PID
    WAIT_EXIT_CODE=$?
    set -e

    if [ $WAIT_EXIT_CODE -eq 0 ]; then
        echo "[docker-build] ✓ $IMAGE_NAME completed successfully (PID $PID)"
        SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
    else
        echo "[docker-build] ✗ $IMAGE_NAME failed with exit code $WAIT_EXIT_CODE (PID $PID)"
        FAILED_BUILDS+=("$IMAGE_NAME")

        # Check status file as backup verification
        if [ -f "$LOG_DIR/${IMAGE_NAME}.status" ]; then
            STATUS_CODE=$(cat "$LOG_DIR/${IMAGE_NAME}.status" 2>/dev/null || echo "unknown")
            echo "[docker-build]   Status file reports: $STATUS_CODE"
        fi

        # Show last 10 lines of the failed build log
        if [ -f "$LOG_DIR/${IMAGE_NAME}.log" ]; then
            echo "[docker-build]   Last 10 lines of build log:"
            tail -10 "$LOG_DIR/${IMAGE_NAME}.log" 2>/dev/null | sed 's/^/    /' || echo "    (could not read log)"
        else
            echo "[docker-build]   No log file found at $LOG_DIR/${IMAGE_NAME}.log"
        fi
    fi
done

# Ensure no stray background jobs keep STDIO open after the main loop completes
REMAINING_JOBS=$(jobs -p 2>/dev/null || true)
if [ -n "$REMAINING_JOBS" ]; then
    echo "[docker-build] Waiting for remaining background jobs to finish..."
    wait $REMAINING_JOBS || true
fi

# Force close any dotnet background processes that may be lingering
# This prevents Azure DevOps STDIO timeout issues
echo "[docker-build] Ensuring all dotnet background processes are terminated..."
pkill -P $$ dotnet 2>/dev/null || true
sleep 1

echo "[docker-build] ============================================"
echo "[docker-build] Build summary: $SUCCESS_COUNT/${#SERVICES[@]} succeeded"

# Clean up log directory (use || true to prevent exit if already deleted)
rm -rf "$LOG_DIR" 2>/dev/null || true

# Verify no background jobs remain before final exit
FINAL_CHECK=$(jobs -p 2>/dev/null || true)
if [ -n "$FINAL_CHECK" ]; then
    echo "[docker-build] WARNING: Background jobs still active, forcing cleanup..."
    wait $FINAL_CHECK 2>/dev/null || true
    pkill -P $$ 2>/dev/null || true
fi

echo "[docker-build] ============================================"

# Exit with error if any builds failed
if [ ${#FAILED_BUILDS[@]} -gt 0 ]; then
    echo "[docker-build] ERROR: The following builds failed: ${FAILED_BUILDS[*]}"
    exit 1
fi

# Export ACR endpoint for Helm deployment
export AZURE_CONTAINER_REGISTRY_ENDPOINT="$ACR_LOGIN_SERVER"
echo "[docker-build] Exported AZURE_CONTAINER_REGISTRY_ENDPOINT=$ACR_LOGIN_SERVER"

echo "[docker-build] All images built and pushed successfully (parallel execution complete)"
exit 0
