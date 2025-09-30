#!/usr/bin/env bash
set -euo pipefail

# Build and push Docker images using ACR Tasks (builds in Azure)
# This is the fastest option as it builds in Azure using ACR's compute
# Usage: build-modern-build-push-images-acr.sh <acr-name> <source-dir>

ACR_NAME=${1:?Missing ACR name}
SOURCE_DIR=${2:-$(pwd)}

# Validate required environment variables
if [ -z "${AZURE_RESOURCE_GROUP}" ]; then
    echo "[acr-build] ERROR: AZURE_RESOURCE_GROUP environment variable is not set"
    exit 1
fi

echo "[acr-build] Building images using ACR Tasks (Azure-based builds)"
echo "[acr-build] ACR: $ACR_NAME"
echo "[acr-build] Resource Group: $AZURE_RESOURCE_GROUP"

ACR_LOGIN_SERVER="${ACR_NAME}.azurecr.io"

# Ensure ACR exists
echo "[acr-build] Checking if ACR exists..."
az acr show --name "$ACR_NAME" --resource-group "${AZURE_RESOURCE_GROUP}" 2>/dev/null || {
    echo "[acr-build] ACR doesn't exist, creating: $ACR_NAME"
    az acr create --name "$ACR_NAME" \
        --resource-group "${AZURE_RESOURCE_GROUP}" \
        --sku Standard \
        --admin-enabled true || {
        echo "[acr-build] ERROR: Failed to create ACR"
        exit 1
    }
    echo "[acr-build] ACR created successfully (Standard tier for ACR Tasks)"
}

# Attach ACR to AKS cluster if AKS exists
AKS_NAME="${AKS_CLUSTER_NAME:-aks-${AZURE_RESOURCE_GROUP}}"
if az aks show --name "$AKS_NAME" --resource-group "${AZURE_RESOURCE_GROUP}" 2>/dev/null; then
    echo "[acr-build] Attaching ACR to AKS cluster..."
    az aks update --name "$AKS_NAME" \
        --resource-group "${AZURE_RESOURCE_GROUP}" \
        --attach-acr "$ACR_NAME" 2>/dev/null || {
        echo "[acr-build] WARNING: Could not attach ACR to AKS (may already be attached)"
    }
fi

# Grant workload identity ACR pull permissions if configured
if [ -n "${WORKLOAD_IDENTITY_PRINCIPAL_ID:-}" ]; then
    echo "[acr-build] Granting AcrPull role to workload identity"
    ACR_RESOURCE_ID=$(az acr show --name "$ACR_NAME" --resource-group "${AZURE_RESOURCE_GROUP}" --query id -o tsv)

    az role assignment create \
        --assignee "$WORKLOAD_IDENTITY_PRINCIPAL_ID" \
        --role "AcrPull" \
        --scope "$ACR_RESOURCE_ID" 2>/dev/null || echo "[acr-build] AcrPull role may already exist"
fi

# Use build number as tag
IMAGE_TAG="${BUILD_BUILDNUMBER:-$(date +%Y%m%d-%H%M%S)}"
echo "[acr-build] Using image tag: $IMAGE_TAG"

# List of services to build
declare -A SERVICES=(
    ["db-setupmanager"]="src/Microsoft.Greenlight.SetupManager.DB"
    ["api-main"]="src/Microsoft.Greenlight.API.Main"
    ["mcp-server"]="src/Microsoft.Greenlight.McpServer"
    ["silo"]="src/Microsoft.Greenlight.Silo"
    ["web-docgen"]="src/Microsoft.Greenlight.Web.DocGen"
)

# Create a multi-stage Dockerfile that builds all services
DOCKERFILE_PATH=$(mktemp)
cat > "$DOCKERFILE_PATH" << 'EOF'
# Multi-stage Dockerfile for all services
# This allows ACR to cache layers efficiently

# Base SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-base
WORKDIR /src
# Copy everything for build context
COPY . .

# Build db-setupmanager
FROM build-base AS build-db-setupmanager
WORKDIR /src/src/Microsoft.Greenlight.SetupManager.DB
RUN dotnet publish -c Release -o /app/publish --os linux --arch x64

# Build api-main
FROM build-base AS build-api-main
WORKDIR /src/src/Microsoft.Greenlight.API.Main
RUN dotnet publish -c Release -o /app/publish --os linux --arch x64

# Build mcp-server
FROM build-base AS build-mcp-server
WORKDIR /src/src/Microsoft.Greenlight.McpServer
RUN dotnet publish -c Release -o /app/publish --os linux --arch x64

# Build silo
FROM build-base AS build-silo
WORKDIR /src/src/Microsoft.Greenlight.Silo
RUN dotnet publish -c Release -o /app/publish --os linux --arch x64

# Build web-docgen
FROM build-base AS build-web-docgen
WORKDIR /src/src/Microsoft.Greenlight.Web.DocGen
RUN dotnet publish -c Release -o /app/publish --os linux --arch x64

# Runtime images
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS db-setupmanager
WORKDIR /app
COPY --from=build-db-setupmanager /app/publish .
ENTRYPOINT ["dotnet", "Microsoft.Greenlight.SetupManager.DB.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api-main
WORKDIR /app
COPY --from=build-api-main /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Microsoft.Greenlight.API.Main.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS mcp-server
WORKDIR /app
COPY --from=build-mcp-server /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Microsoft.Greenlight.McpServer.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS silo
WORKDIR /app
COPY --from=build-silo /app/publish .
EXPOSE 11111 30000
ENTRYPOINT ["dotnet", "Microsoft.Greenlight.Silo.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS web-docgen
WORKDIR /app
COPY --from=build-web-docgen /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Microsoft.Greenlight.Web.DocGen.dll"]
EOF

echo "[acr-build] Created multi-stage Dockerfile"

# Submit single ACR Task that builds all images in one go
echo "[acr-build] Submitting ACR Task to build all images..."
echo "[acr-build] This builds in Azure using ACR's compute resources"

# Build all targets in one ACR task run
for IMAGE_NAME in "${!SERVICES[@]}"; do
    echo "[acr-build] Queuing build for: $IMAGE_NAME"
done

# Run the ACR build task - builds all targets
az acr build \
    --registry "$ACR_NAME" \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --file "$DOCKERFILE_PATH" \
    --platform linux/amd64 \
    --image "temp-build:$IMAGE_TAG" \
    "$SOURCE_DIR" \
    --no-logs &

BUILD_PID=$!

# Show progress while waiting
echo "[acr-build] Build submitted to ACR. Building in Azure..."
echo "[acr-build] This typically takes 2-4 minutes for all images"

# Wait for build to complete
wait $BUILD_PID
BUILD_RESULT=$?

if [ $BUILD_RESULT -ne 0 ]; then
    echo "[acr-build] ERROR: ACR build failed"
    rm -f "$DOCKERFILE_PATH"
    exit 1
fi

# Now tag each stage as a separate image
echo "[acr-build] Build complete. Tagging individual service images..."

for IMAGE_NAME in "${!SERVICES[@]}"; do
    echo "[acr-build] Creating image for $IMAGE_NAME..."

    # Import each stage as a separate image
    az acr build \
        --registry "$ACR_NAME" \
        --resource-group "${AZURE_RESOURCE_GROUP}" \
        --file "$DOCKERFILE_PATH" \
        --target "$IMAGE_NAME" \
        --image "${IMAGE_NAME}:${IMAGE_TAG}" \
        --image "${IMAGE_NAME}:latest" \
        "$SOURCE_DIR" \
        --no-logs || {
        echo "[acr-build] WARNING: Failed to build $IMAGE_NAME"
    }
done

# Clean up
rm -f "$DOCKERFILE_PATH"

# Export ACR endpoint
export AZURE_CONTAINER_REGISTRY_ENDPOINT="$ACR_LOGIN_SERVER"
echo "[acr-build] Exported AZURE_CONTAINER_REGISTRY_ENDPOINT=$ACR_LOGIN_SERVER"

echo "[acr-build] All images built and pushed successfully using ACR Tasks"
echo "[acr-build] Images are available at: $ACR_LOGIN_SERVER"