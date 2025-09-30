#!/usr/bin/env bash
set -euo pipefail

# Build and push Docker images using cached base image for maximum speed
# This combines parallel builds with a pre-cached dependency layer
# Usage: build-modern-build-push-images-optimized.sh <acr-name> <source-dir>

ACR_NAME=${1:?Missing ACR name}
SOURCE_DIR=${2:-$(pwd)}

# Validate required environment variables
if [ -z "${AZURE_RESOURCE_GROUP}" ]; then
    echo "[docker-opt] ERROR: AZURE_RESOURCE_GROUP environment variable is not set"
    exit 1
fi

echo "[docker-opt] Optimized build with cached base image + parallel execution"
echo "[docker-opt] Resource Group: $AZURE_RESOURCE_GROUP"

ACR_LOGIN_SERVER="${ACR_NAME}.azurecr.io"
echo "[docker-opt] ACR login server: $ACR_LOGIN_SERVER"

# Ensure ACR exists
echo "[docker-opt] Checking if ACR exists..."
az acr show --name "$ACR_NAME" --resource-group "${AZURE_RESOURCE_GROUP}" 2>/dev/null || {
    echo "[docker-opt] ACR doesn't exist, creating: $ACR_NAME"
    az acr create --name "$ACR_NAME" \
        --resource-group "${AZURE_RESOURCE_GROUP}" \
        --sku Basic \
        --admin-enabled true || {
        echo "[docker-opt] ERROR: Failed to create ACR"
        exit 1
    }
}

# ACR login
echo "[docker-opt] Logging into ACR..."
az acr login --name "$ACR_NAME" 2>/dev/null || {
    echo "[docker-opt] ERROR: Failed to login to ACR"
    exit 1
}

# Attach ACR to AKS if exists
AKS_NAME="${AKS_CLUSTER_NAME:-aks-${AZURE_RESOURCE_GROUP}}"
if az aks show --name "$AKS_NAME" --resource-group "${AZURE_RESOURCE_GROUP}" 2>/dev/null; then
    echo "[docker-opt] Attaching ACR to AKS..."
    az aks update --name "$AKS_NAME" \
        --resource-group "${AZURE_RESOURCE_GROUP}" \
        --attach-acr "$ACR_NAME" 2>/dev/null || true
fi

# Grant workload identity permissions
if [ -n "${WORKLOAD_IDENTITY_PRINCIPAL_ID:-}" ]; then
    echo "[docker-opt] Granting AcrPull role to workload identity"
    ACR_RESOURCE_ID=$(az acr show --name "$ACR_NAME" --resource-group "${AZURE_RESOURCE_GROUP}" --query id -o tsv)
    az role assignment create \
        --assignee "$WORKLOAD_IDENTITY_PRINCIPAL_ID" \
        --role "AcrPull" \
        --scope "$ACR_RESOURCE_ID" 2>/dev/null || true
fi

# Image tag
IMAGE_TAG="${BUILD_BUILDNUMBER:-$(date +%Y%m%d-%H%M%S)}"
echo "[docker-opt] Using image tag: $IMAGE_TAG"

# Check when base image was last built
BASE_IMAGE="${ACR_LOGIN_SERVER}/greenlight-base:latest"
BASE_IMAGE_AGE_DAYS=7  # Rebuild base if older than 7 days

echo "[docker-opt] Checking base image age..."
REBUILD_BASE=false

# Check if base image exists and its age
if ! az acr repository show --name "$ACR_NAME" --image "greenlight-base:latest" 2>/dev/null; then
    echo "[docker-opt] Base image doesn't exist, will build it"
    REBUILD_BASE=true
else
    # Get the last update time of the base image
    LAST_UPDATE=$(az acr repository show --name "$ACR_NAME" --image "greenlight-base:latest" --query "lastUpdateTime" -o tsv)
    if [ -n "$LAST_UPDATE" ]; then
        LAST_UPDATE_EPOCH=$(date -d "$LAST_UPDATE" +%s 2>/dev/null || date -j -f "%Y-%m-%dT%H:%M:%S" "$LAST_UPDATE" +%s 2>/dev/null || echo 0)
        CURRENT_EPOCH=$(date +%s)
        AGE_DAYS=$(( ($CURRENT_EPOCH - $LAST_UPDATE_EPOCH) / 86400 ))

        if [ $AGE_DAYS -gt $BASE_IMAGE_AGE_DAYS ]; then
            echo "[docker-opt] Base image is $AGE_DAYS days old, rebuilding"
            REBUILD_BASE=true
        else
            echo "[docker-opt] Base image is $AGE_DAYS days old, using cached version"
        fi
    fi
fi

# Build or pull base image
if [ "$REBUILD_BASE" = true ]; then
    echo "[docker-opt] Building base image with all dependencies..."

    # Use BuildKit for better caching
    export DOCKER_BUILDKIT=1

    # Build base image
    docker build \
        -f "$SOURCE_DIR/build/docker/Dockerfile.base" \
        -t "$BASE_IMAGE" \
        --build-arg BUILD_DATE=$(date -u +'%Y-%m-%dT%H:%M:%SZ') \
        --cache-from "$BASE_IMAGE" \
        "$SOURCE_DIR" || {
        echo "[docker-opt] ERROR: Failed to build base image"
        exit 1
    }

    # Push base image
    echo "[docker-opt] Pushing base image to ACR..."
    docker push "$BASE_IMAGE"
    echo "[docker-opt] Base image updated and pushed"
else
    # Pull base image to ensure it's cached locally
    echo "[docker-opt] Pulling base image from ACR..."
    docker pull "$BASE_IMAGE" || {
        echo "[docker-opt] WARNING: Could not pull base image, will rebuild"
        REBUILD_BASE=true
    }
fi

# Services to build
SERVICES=(
    "db-setupmanager:src/Microsoft.Greenlight.SetupManager.DB:Microsoft.Greenlight.SetupManager.DB.dll"
    "api-main:src/Microsoft.Greenlight.API.Main:Microsoft.Greenlight.API.Main.dll"
    "mcp-server:src/Microsoft.Greenlight.McpServer:Microsoft.Greenlight.McpServer.dll"
    "silo:src/Microsoft.Greenlight.Silo:Microsoft.Greenlight.Silo.dll"
    "web-docgen:src/Microsoft.Greenlight.Web.DocGen:Microsoft.Greenlight.Web.DocGen.dll"
)

# Create temporary directory for Dockerfiles
DOCKER_DIR=$(mktemp -d)
echo "[docker-opt] Temporary Docker directory: $DOCKER_DIR"

# Function to create optimized Dockerfile for each service
create_service_dockerfile() {
    local IMAGE_NAME=$1
    local PROJECT_PATH=$2
    local DLL_NAME=$3
    local DOCKERFILE="$DOCKER_DIR/Dockerfile.$IMAGE_NAME"

    # Determine exposed ports based on service
    local EXPOSE_PORTS="8080"
    if [ "$IMAGE_NAME" = "silo" ]; then
        EXPOSE_PORTS="11111 30000"
    fi

    cat > "$DOCKERFILE" << EOF
# Optimized build using cached base image
FROM ${BASE_IMAGE} AS build

# Copy only the specific project needed (rest is in base)
COPY ${PROJECT_PATH} ${PROJECT_PATH}/

# Build the specific service (dependencies already restored)
WORKDIR /src/$(basename $PROJECT_PATH)
RUN dotnet publish -c Release -o /app/publish --no-restore --verbosity minimal

# Runtime image (minimal size)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Install globalization support for Alpine
RUN apk add --no-cache icu-libs

# Copy published app
COPY --from=build /app/publish .

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE $EXPOSE_PORTS

ENTRYPOINT ["dotnet", "${DLL_NAME}"]
EOF

    echo "$DOCKERFILE"
}

# Create all Dockerfiles
echo "[docker-opt] Creating optimized Dockerfiles..."
for SERVICE_ENTRY in "${SERVICES[@]}"; do
    IFS=':' read -r IMAGE_NAME PROJECT_PATH DLL_NAME <<< "$SERVICE_ENTRY"
    create_service_dockerfile "$IMAGE_NAME" "$PROJECT_PATH" "$DLL_NAME"
done

# Function to build and push a single image
build_and_push_image() {
    local IMAGE_NAME=$1
    local PROJECT_PATH=$2
    local DLL_NAME=$3
    local DOCKERFILE="$DOCKER_DIR/Dockerfile.$IMAGE_NAME"

    echo "[docker-opt] Building $IMAGE_NAME..."

    # Build with BuildKit for better performance
    DOCKER_BUILDKIT=1 docker build \
        -f "$DOCKERFILE" \
        -t "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG}" \
        -t "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:latest" \
        --cache-from "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:latest" \
        "$SOURCE_DIR" || return 1

    # Push both tags
    docker push "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG}" || return 1
    docker push "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:latest" || return 1

    echo "[docker-opt] ✓ $IMAGE_NAME pushed successfully"
    return 0
}

# Export for parallel execution
export -f build_and_push_image
export DOCKER_DIR ACR_LOGIN_SERVER IMAGE_TAG SOURCE_DIR BASE_IMAGE

# Build all images in parallel
echo "[docker-opt] Starting parallel builds for ${#SERVICES[@]} services..."
echo "[docker-opt] ============================================"

PIDS=()
for SERVICE_ENTRY in "${SERVICES[@]}"; do
    IFS=':' read -r IMAGE_NAME PROJECT_PATH DLL_NAME <<< "$SERVICE_ENTRY"
    build_and_push_image "$IMAGE_NAME" "$PROJECT_PATH" "$DLL_NAME" &
    PIDS+=($!)
done

# Wait for all builds
FAILED_BUILDS=()
SUCCESS_COUNT=0

for i in "${!PIDS[@]}"; do
    PID=${PIDS[$i]}
    SERVICE_ENTRY=${SERVICES[$i]}
    IFS=':' read -r IMAGE_NAME PROJECT_PATH DLL_NAME <<< "$SERVICE_ENTRY"

    if wait $PID; then
        ((SUCCESS_COUNT++))
    else
        FAILED_BUILDS+=("$IMAGE_NAME")
    fi
done

# Clean up
rm -rf "$DOCKER_DIR"

echo "[docker-opt] ============================================"
echo "[docker-opt] Build summary: $SUCCESS_COUNT/${#SERVICES[@]} succeeded"

if [ ${#FAILED_BUILDS[@]} -gt 0 ]; then
    echo "[docker-opt] ERROR: Failed builds: ${FAILED_BUILDS[*]}"
    exit 1
fi

# Export ACR endpoint
export AZURE_CONTAINER_REGISTRY_ENDPOINT="$ACR_LOGIN_SERVER"
echo "[docker-opt] All images built and pushed successfully"
echo "[docker-opt] Optimized build complete (base + parallel)"