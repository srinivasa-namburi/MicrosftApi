#!/usr/bin/env bash
set -euo pipefail

# Provision Azure Front Door Standard profile with three endpoints (api/web/mcp)
# and routes to AKS LoadBalancer public IPs (HTTP only). Updates HostNameOverride.
# Usage: build-modern-afd-setup.sh <resource_group> <location> <name_prefix> <namespace>

RG=${1:?Missing resource group}
LOC=${2:?Missing location}
PREFIX_RAW=${3:-greenlight}
NS=${4:-greenlight-dev}

if [[ "${DISABLE_EXPOSEDENDPOINTS:-false}" == "true" ]]; then
  echo "[afd] DISABLE_EXPOSEDENDPOINTS=true - skipping AFD setup"
  exit 0
fi

if [[ ! -f exposed-endpoints.env ]]; then
  echo "[afd] ERROR: exposed-endpoints.env not found (run expose-endpoints first)" >&2
  exit 1
fi
source exposed-endpoints.env

DEPLOYMENT_MODEL=${DEPLOYMENT_MODEL:-${DEPLOYMENT_MODE:-public}}
DEPLOYMENT_MODEL=${DEPLOYMENT_MODEL,,}

# Ensure az afd extension
az extension add -n afd -y >/dev/null 2>&1 || true
az extension list -o table | sed -n '1,120p' >/dev/null 2>&1 || true

normalize() {
  echo "$1" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9-]//g'
}

RG_NORM=$(normalize "$RG")
PREFIX=$(normalize "$PREFIX_RAW")

PROFILE_BASE="gl-${RG_NORM}"

if [[ "$DEPLOYMENT_MODEL" == "hybrid" ]]; then
  PROFILE_NAME="${PROFILE_BASE}-hybrid"
  ENDPOINT_PREFIX="${PROFILE_BASE}-hybrid"
  PROFILE_SKU="Premium_AzureFrontDoor"
else
  PROFILE_NAME="${PROFILE_BASE}"
  ENDPOINT_PREFIX="${PROFILE_BASE}"
  PROFILE_SKU="Standard_AzureFrontDoor"
fi

API_EP="${ENDPOINT_PREFIX}-api"
WEB_EP="${ENDPOINT_PREFIX}-web"
MCP_CORE_EP="${ENDPOINT_PREFIX}-mcp-core"
MCP_FLOW_EP="${ENDPOINT_PREFIX}-mcp-flow"

echo "[afd] Ensuring AFD profile $PROFILE_NAME in $RG/$LOC"
if az afd profile show -g "$RG" -n "$PROFILE_NAME" >/dev/null 2>&1; then
  EXISTING_SKU=$(az afd profile show -g "$RG" -n "$PROFILE_NAME" --query "sku.name" -o tsv)
  if [[ "$EXISTING_SKU" != "$PROFILE_SKU" ]]; then
    echo "[afd] Existing profile SKU ($EXISTING_SKU) differs from required $PROFILE_SKU; recreating profile"
    az afd profile delete -g "$RG" -n "$PROFILE_NAME" --yes >/dev/null 2>&1 || true
    az afd profile create -g "$RG" -n "$PROFILE_NAME" --sku "$PROFILE_SKU" >/dev/null
  fi
else
  az afd profile create -g "$RG" -n "$PROFILE_NAME" --sku "$PROFILE_SKU" >/dev/null
fi

# Extract IPs from exposed endpoints
API_IP=${EXPOSED_API_MAIN#http://}
WEB_IP=${EXPOSED_WEB_DOCGEN#http://}
MCP_CORE_IP=${EXPOSED_MCP_CORE#http://}
MCP_FLOW_IP=${EXPOSED_MCP_FLOW#http://}

if [[ "$DEPLOYMENT_MODEL" == "hybrid" ]]; then
  AKS_NODE_RG=${EXPOSED_AKS_NODE_RESOURCE_GROUP:-${AKS_NODE_RESOURCE_GROUP:-}}
  AKS_LOCATION=${EXPOSED_AKS_CLUSTER_LOCATION:-$LOC}
  if [[ -n "${EXPOSED_AZURE_SUBSCRIPTION_ID:-}" ]]; then
    SUBSCRIPTION_ID="$EXPOSED_AZURE_SUBSCRIPTION_ID"
  else
    SUBSCRIPTION_ID=$(az account show --query id -o tsv 2>/dev/null || echo "")
  fi

  if [[ -z "$AKS_NODE_RG" || -z "$AKS_LOCATION" || -z "$SUBSCRIPTION_ID" ]]; then
    echo "[afd] ERROR: Missing AKS metadata for hybrid deployment (node RG/location/subscription)" >&2
    exit 1
  fi

  API_FRONTEND_ID=${EXPOSED_API_MAIN_FRONTEND_ID:-}
  WEB_FRONTEND_ID=${EXPOSED_WEB_DOCGEN_FRONTEND_ID:-}
  MCP_CORE_FRONTEND_ID=${EXPOSED_MCP_CORE_FRONTEND_ID:-}
  MCP_FLOW_FRONTEND_ID=${EXPOSED_MCP_FLOW_FRONTEND_ID:-}

  if [[ -z "$API_FRONTEND_ID" || -z "$WEB_FRONTEND_ID" || -z "$MCP_CORE_FRONTEND_ID" || -z "$MCP_FLOW_FRONTEND_ID" ]]; then
    echo "[afd] ERROR: Hybrid deployment requires frontend IDs from expose script" >&2
    exit 1
  fi

ensure_private_link_service() {
  local svc_key=$1
  local frontend_id=$2

  if [[ -z "$frontend_id" || "$frontend_id" == "null" ]]; then
    echo ""
    return 1
  fi

  local subnet_id
  subnet_id=$(az resource show --ids "$frontend_id" --query "properties.subnet.id" -o tsv 2>/dev/null || true)
  if [[ -z "$subnet_id" ]]; then
    echo "[afd] ERROR: Unable to resolve subnet for frontend $frontend_id" >&2
    exit 1
  fi

  local frontend_name=$(basename "$frontend_id")
  local lb_name=$(basename "$(dirname "$frontend_id")")

  # Log to stderr to avoid polluting function output
  echo "[afd] Preparing Private Link service for $svc_key (LB: $lb_name, frontend: $frontend_name)" >&2

  az network vnet subnet update --ids "$subnet_id" --disable-private-link-service-network-policies true >/dev/null

  local pls_name="pls-${PREFIX}-${svc_key}"
  local pls_rg="$AKS_NODE_RG"
  local pls_id="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${pls_rg}/providers/Microsoft.Network/privateLinkServices/${pls_name}"

  # Check if a PLS with the desired name already exists
  local pls_exists=false
  if az network private-link-service show --ids "$pls_id" >/dev/null 2>&1; then
    pls_exists=true
    echo "[afd] Found existing private link service: $pls_name" >&2
  fi

  # If PLS doesn't exist with our name, check if another PLS already references this frontend IP
  if [[ "$pls_exists" == "false" ]]; then
    echo "[afd] Checking for existing PLS that references frontend IP: $frontend_id" >&2

    # List all PLS in the AKS node resource group
    local existing_pls_list
    existing_pls_list=$(az network private-link-service list -g "$pls_rg" --query "[].id" -o tsv 2>/dev/null || true)

    if [[ -n "$existing_pls_list" ]]; then
      while IFS= read -r existing_pls_id; do
        if [[ -z "$existing_pls_id" ]]; then continue; fi

        # Check if this PLS references our frontend IP
        local pls_frontend
        pls_frontend=$(az network private-link-service show --ids "$existing_pls_id" --query "loadBalancerFrontendIpConfigurations[0].id" -o tsv 2>/dev/null || true)

        if [[ "$pls_frontend" == "$frontend_id" ]]; then
          # Found an existing PLS that references our frontend IP - reuse it
          local existing_pls_name=$(basename "$existing_pls_id")
          echo "[afd] Found existing PLS '$existing_pls_name' already referencing frontend IP - reusing it instead of creating '$pls_name'" >&2
          pls_id="$existing_pls_id"
          pls_name="$existing_pls_name"
          pls_exists=true
          break
        fi
      done <<< "$existing_pls_list"
    fi
  fi

  # Create or update the PLS
  if [[ "$pls_exists" == "true" ]]; then
    echo "[afd] Updating existing private link service: $pls_name" >&2
    az network private-link-service update --ids "$pls_id" --lb-frontend-ip-configs "$frontend_id" --visibility "$SUBSCRIPTION_ID" --auto-approval "$SUBSCRIPTION_ID" >/dev/null
  else
    echo "[afd] Creating private link service: $pls_name" >&2
    az network private-link-service create -g "$pls_rg" -n "$pls_name" \
      --lb-frontend-ip-configs "$frontend_id" \
      --subnet "$subnet_id" \
      --location "$AKS_LOCATION" \
      --visibility "$SUBSCRIPTION_ID" \
      --auto-approval "$SUBSCRIPTION_ID" >/dev/null
  fi

  # Don't return group ID - it's not needed for third-party Private Link services (AKS LB)
  # Azure Front Door rejects group IDs when connecting to custom Private Link services
  echo "$pls_id|"
}

approve_private_endpoint_connections() {
  local pls_id=$1
  if [[ -z "$pls_id" || "$pls_id" == "null" ]]; then
    return 0
  fi

  local pending_connections
  pending_connections=$(az network private-link-service show --ids "$pls_id" \
    --query "privateEndpointConnections[?privateLinkServiceConnectionState.status!='Approved'].id" \
    -o tsv 2>/dev/null || true)

  if [[ -z "$pending_connections" ]]; then
    echo "[afd] No pending private endpoint approvals for $pls_id" >&2
    return 0
  fi

  while IFS= read -r connection_id; do
    if [[ -z "$connection_id" ]]; then
      continue
    fi

    echo "[afd] Approving private endpoint connection: $connection_id" >&2
    if ! az network private-endpoint-connection approve --id "$connection_id" \
      --description "Approved by build-modern-afd-setup.sh" >/dev/null; then
      echo "[afd] WARNING: Failed to approve connection $connection_id (manual approval may be required)" >&2
    fi
  done <<< "$pending_connections"
}

  IFS='|' read -r API_PLS_ID API_PLS_GROUP <<< "$(ensure_private_link_service api "$API_FRONTEND_ID")"
  IFS='|' read -r WEB_PLS_ID WEB_PLS_GROUP <<< "$(ensure_private_link_service web "$WEB_FRONTEND_ID")"
  IFS='|' read -r MCP_CORE_PLS_ID MCP_CORE_PLS_GROUP <<< "$(ensure_private_link_service mcp-core "$MCP_CORE_FRONTEND_ID")"
  IFS='|' read -r MCP_FLOW_PLS_ID MCP_FLOW_PLS_GROUP <<< "$(ensure_private_link_service mcp-flow "$MCP_FLOW_FRONTEND_ID")"

  approve_private_endpoint_connections "$API_PLS_ID"
  approve_private_endpoint_connections "$WEB_PLS_ID"
  approve_private_endpoint_connections "$MCP_CORE_PLS_ID"
  approve_private_endpoint_connections "$MCP_FLOW_PLS_ID"
else
  AKS_NODE_RG=""
  AKS_LOCATION="$LOC"
  SUBSCRIPTION_ID=$(az account show --query id -o tsv 2>/dev/null || echo "")
  API_PLS_ID=""
  WEB_PLS_ID=""
  MCP_CORE_PLS_ID=""
  MCP_FLOW_PLS_ID=""
  API_PLS_GROUP=""
  WEB_PLS_GROUP=""
  MCP_CORE_PLS_GROUP=""
  MCP_FLOW_PLS_GROUP=""
fi

# Check if origin-group needs update based on actual configuration
create_or_update_origin_group() {
  local name=$1
  local needs_recreate=false

  if az afd origin-group show -g "$RG" --profile-name "$PROFILE_NAME" -n "$name" >/dev/null 2>&1; then
    # Check if existing config matches desired state
    local health_probe_enabled=$(az afd origin-group show -g "$RG" --profile-name "$PROFILE_NAME" -n "$name" --query "healthProbeSettings.probeProtocol" -o tsv 2>/dev/null || echo "disabled")
    local session_affinity=$(az afd origin-group show -g "$RG" --profile-name "$PROFILE_NAME" -n "$name" --query "sessionAffinityState" -o tsv 2>/dev/null || echo "")

    # Only recreate if configuration differs
    if [[ "$health_probe_enabled" != "NotSet" ]] || [[ "$session_affinity" != "Enabled" ]]; then
      echo "[afd] Origin-group $name config differs (health probe or session affinity) - recreating"
      needs_recreate=true
    else
      echo "[afd] Origin-group $name exists with correct config - skipping"
      return 0
    fi
  else
    needs_recreate=true
  fi

  if [[ "$needs_recreate" == "true" ]]; then
    # Delete if exists
    if az afd origin-group show -g "$RG" --profile-name "$PROFILE_NAME" -n "$name" >/dev/null 2>&1; then
      echo "[afd] Deleting origin-group: $name"
      az afd origin-group delete -g "$RG" --profile-name "$PROFILE_NAME" -n "$name" -y >/dev/null 2>&1 || true
    fi

    echo "[afd] Creating origin-group (probes disabled, session affinity enabled): $name"
    # LoadBalancingSettings subobject is required by ARM; provide minimal valid values
    # Session affinity enabled for SignalR sticky sessions support
    if ! az afd origin-group create -g "$RG" --profile-name "$PROFILE_NAME" -n "$name" \
      --enable-health-probe false \
      --session-affinity-state Enabled \
      --sample-size 4 --successful-samples-required 3 --additional-latency-in-milliseconds 0 >/dev/null; then
      echo "[afd] ERROR: Failed to create origin-group $name"
      exit 1
    fi
  fi
}

create_or_update_origin() {
  local group=$1; local name=$2; local host=$3; local pl_resource=${4:-}; local pl_location=${5:-}; local pl_subresource=${6:-}
  local pl_args=()
  if [[ -n "$pl_resource" ]]; then
    pl_args=(--enable-private-link true --private-link-resource "$pl_resource" --private-link-location "$pl_location" --private-link-request-message "Azure Front Door access")
    # Only include sub-resource type if explicitly provided (not for custom Private Link services)
    if [[ -n "$pl_subresource" ]]; then
      pl_args+=(--private-link-sub-resource-type "$pl_subresource")
    fi
  fi

  if az afd origin show -g "$RG" --profile-name "$PROFILE_NAME" --origin-group-name "$group" -n "$name" >/dev/null 2>&1; then
    # Check if existing origin points to the same host and Private Link resource
    local existing_host=$(az afd origin show -g "$RG" --profile-name "$PROFILE_NAME" --origin-group-name "$group" -n "$name" --query "hostName" -o tsv 2>/dev/null || echo "")
    local existing_pl=$(az afd origin show -g "$RG" --profile-name "$PROFILE_NAME" --origin-group-name "$group" -n "$name" --query "sharedPrivateLinkResource.privateLink.id" -o tsv 2>/dev/null || echo "")

    # Compare with desired state
    local needs_update=false
    if [[ "$existing_host" != "$host" ]]; then
      echo "[afd] Origin $name host changed: $existing_host -> $host"
      needs_update=true
    elif [[ -n "$pl_resource" && "$existing_pl" != "$pl_resource" ]]; then
      echo "[afd] Origin $name private link changed: $existing_pl -> $pl_resource"
      needs_update=true
    fi

    if [[ "$needs_update" == "true" ]]; then
      echo "[afd] Updating origin: $name ($host)"
      if ! az afd origin update -g "$RG" --profile-name "$PROFILE_NAME" --origin-group-name "$group" -n "$name" \
        --host-name "$host" --http-port 80 --enabled-state Enabled --origin-host-header "$host" \
        "${pl_args[@]}" >/dev/null; then
        echo "[afd] origin update failed; recreating $name"
        az afd origin delete -g "$RG" --profile-name "$PROFILE_NAME" --origin-group-name "$group" -n "$name" -y >/dev/null 2>&1 || true
        az afd origin create -g "$RG" --profile-name "$PROFILE_NAME" --origin-group-name "$group" -n "$name" \
          --host-name "$host" --http-port 80 --enabled-state Enabled --origin-host-header "$host" \
          "${pl_args[@]}" >/dev/null
      fi
    else
      echo "[afd] Origin $name already configured correctly - skipping"
    fi
  else
    echo "[afd] Creating origin: $name ($host)"
    az afd origin create -g "$RG" --profile-name "$PROFILE_NAME" --origin-group-name "$group" -n "$name" \
      --host-name "$host" --http-port 80 --enabled-state Enabled --origin-host-header "$host" \
      "${pl_args[@]}" >/dev/null
  fi
}

create_or_update_endpoint() {
  local ep=$1
  if az afd endpoint show -g "$RG" --profile-name "$PROFILE_NAME" -n "$ep" >/dev/null 2>&1; then
    echo "[afd] Updating endpoint: $ep"
    az afd endpoint update -g "$RG" --profile-name "$PROFILE_NAME" -n "$ep" --enabled-state Enabled >/dev/null
  else
    echo "[afd] Creating endpoint: $ep"
    az afd endpoint create -g "$RG" --profile-name "$PROFILE_NAME" -n "$ep" --enabled-state Enabled >/dev/null
  fi
}

create_or_update_route() {
  local ep=$1; local route=$2; local group=$3
  if az afd route show -g "$RG" --profile-name "$PROFILE_NAME" --endpoint-name "$ep" -n "$route" >/dev/null 2>&1; then
    # Check if route already points to correct origin group
    local existing_og=$(az afd route show -g "$RG" --profile-name "$PROFILE_NAME" --endpoint-name "$ep" -n "$route" --query "originGroup.id" -o tsv 2>/dev/null | sed 's|.*/||' || echo "")

    if [[ "$existing_og" == "$group" ]]; then
      echo "[afd] Route $route already points to $group - skipping"
    else
      echo "[afd] Updating route: $route on $ep (origin group changed: $existing_og -> $group)"
      if ! az afd route update -g "$RG" --profile-name "$PROFILE_NAME" --endpoint-name "$ep" -n "$route" \
        --origin-group "$group" --https-redirect Enabled --supported-protocols Http Https --forwarding-protocol HttpOnly \
        --link-to-default-domain Enabled --patterns-to-match "/*" --enable-caching false >/dev/null; then
        echo "[afd] route update failed; recreating route $route"
        az afd route delete -g "$RG" --profile-name "$PROFILE_NAME" --endpoint-name "$ep" -n "$route" -y >/dev/null 2>&1 || true
        az afd route create -g "$RG" --profile-name "$PROFILE_NAME" --endpoint-name "$ep" -n "$route" \
          --origin-group "$group" --https-redirect Enabled --supported-protocols Http Https --forwarding-protocol HttpOnly \
          --link-to-default-domain Enabled --patterns-to-match "/*" --enable-caching false >/dev/null
      fi
    fi
  else
    echo "[afd] Creating route: $route on $ep"
    az afd route create -g "$RG" --profile-name "$PROFILE_NAME" --endpoint-name "$ep" -n "$route" \
      --origin-group "$group" --https-redirect Enabled --supported-protocols Http Https --forwarding-protocol HttpOnly \
      --link-to-default-domain Enabled --patterns-to-match "/*" --enable-caching false >/dev/null
  fi
}

wait_for_private_endpoint_connection() {
  local pls_id=$1
  local retries=${2:-24}
  local delay=${3:-5}

  # First check if there's already an approved connection (common on re-deployments)
  local initial_status
  initial_status=$(az network private-link-service show --ids "$pls_id" --query "privateEndpointConnections[?properties.connectionState.status=='Approved'].length(@)" -o tsv 2>/dev/null || echo "0")

  if [[ -n "$initial_status" && "$initial_status" -gt 0 ]]; then
    echo "[afd] Private Link $pls_id already has $initial_status approved connection(s) - skipping wait" >&2
    return 0
  fi

  local total_connections
  total_connections=$(az network private-link-service show --ids "$pls_id" --query "length(privateEndpointConnections)" -o tsv 2>/dev/null || echo "")
  if [[ -z "$total_connections" ]]; then
    total_connections="0"
  fi

  if [[ "$total_connections" == "0" ]]; then
    echo "[afd] Private Link $pls_id has no private endpoint connections - skipping wait" >&2
    return 0
  fi

  local pending_connections
  pending_connections=$(az network private-link-service show --ids "$pls_id" --query "privateEndpointConnections[?properties.connectionState.status=='PendingApproval' || properties.connectionState.status=='Pending'].length(@)" -o tsv 2>/dev/null || echo "")
  if [[ -z "$pending_connections" ]]; then
    pending_connections="0"
  fi

  if [[ "$pending_connections" == "0" ]]; then
    echo "[afd] Private Link $pls_id has $total_connections connection(s) but none pending approval - skipping wait" >&2
    return 0
  fi

  # If we have pending connections but none approved yet, wait for first approval (new deployment scenario)
  echo "[afd] Waiting for Private Link connection approval on $pls_id ($pending_connections pending)..." >&2
  local attempt=0
  while [[ $attempt -lt $retries ]]; do
    local status
    status=$(az network private-link-service show --ids "$pls_id" --query "privateEndpointConnections[?properties.connectionState.status=='Approved'].length(@)" -o tsv 2>/dev/null || echo "0")
    if [[ -n "$status" && "$status" -gt 0 ]]; then
      echo "[afd] Private Link connection approved after $attempt attempt(s)" >&2
      return 0
    fi

    local pending_remaining
    pending_remaining=$(az network private-link-service show --ids "$pls_id" --query "privateEndpointConnections[?properties.connectionState.status=='PendingApproval' || properties.connectionState.status=='Pending'].length(@)" -o tsv 2>/dev/null || echo "")
    if [[ -z "$pending_remaining" ]]; then
      pending_remaining="0"
    fi
    if [[ "$pending_remaining" == "0" ]]; then
      echo "[afd] Pending Private Link approvals cleared on $pls_id without an approval event - skipping remaining wait" >&2
      return 0
    fi

    sleep "$delay"
    attempt=$((attempt+1))
  done
  echo "[afd] WARNING: Private Link service $pls_id has no approved connections after ${retries} attempts (may require manual approval)" >&2
  return 1
}

# API
create_or_update_origin_group api-origins
if [[ "$DEPLOYMENT_MODEL" == "hybrid" ]]; then
  create_or_update_origin api-origins api-origin "$API_IP" "$API_PLS_ID" "$AKS_LOCATION" "$API_PLS_GROUP"
else
  create_or_update_origin api-origins api-origin "$API_IP"
fi
create_or_update_endpoint "$API_EP"
create_or_update_route "$API_EP" api-route api-origins

# WEB
create_or_update_origin_group web-origins
if [[ "$DEPLOYMENT_MODEL" == "hybrid" ]]; then
  create_or_update_origin web-origins web-origin "$WEB_IP" "$WEB_PLS_ID" "$AKS_LOCATION" "$WEB_PLS_GROUP"
else
  create_or_update_origin web-origins web-origin "$WEB_IP"
fi
create_or_update_endpoint "$WEB_EP"
create_or_update_route "$WEB_EP" web-route web-origins

# MCP Core
create_or_update_origin_group mcp-core-origins
if [[ "$DEPLOYMENT_MODEL" == "hybrid" ]]; then
  create_or_update_origin mcp-core-origins mcp-core-origin "$MCP_CORE_IP" "$MCP_CORE_PLS_ID" "$AKS_LOCATION" "$MCP_CORE_PLS_GROUP"
else
  create_or_update_origin mcp-core-origins mcp-core-origin "$MCP_CORE_IP"
fi
create_or_update_endpoint "$MCP_CORE_EP"
create_or_update_route "$MCP_CORE_EP" mcp-core-route mcp-core-origins

# MCP Flow
create_or_update_origin_group mcp-flow-origins
if [[ "$DEPLOYMENT_MODEL" == "hybrid" ]]; then
  create_or_update_origin mcp-flow-origins mcp-flow-origin "$MCP_FLOW_IP" "$MCP_FLOW_PLS_ID" "$AKS_LOCATION" "$MCP_FLOW_PLS_GROUP"
else
  create_or_update_origin mcp-flow-origins mcp-flow-origin "$MCP_FLOW_IP"
fi
create_or_update_endpoint "$MCP_FLOW_EP"
create_or_update_route "$MCP_FLOW_EP" mcp-flow-route mcp-flow-origins

if [[ "$DEPLOYMENT_MODEL" == "hybrid" ]]; then
  wait_for_private_endpoint_connection "$API_PLS_ID" || true
  wait_for_private_endpoint_connection "$WEB_PLS_ID" || true
  wait_for_private_endpoint_connection "$MCP_CORE_PLS_ID" || true
  wait_for_private_endpoint_connection "$MCP_FLOW_PLS_ID" || true
fi

# Resolve actual default hostnames assigned by Azure Front Door (include regional suffix)
API_HOST=$(az afd endpoint show -g "$RG" --profile-name "$PROFILE_NAME" -n "$API_EP" --query "hostName" -o tsv)
WEB_HOST=$(az afd endpoint show -g "$RG" --profile-name "$PROFILE_NAME" -n "$WEB_EP" --query "hostName" -o tsv)
MCP_CORE_HOST=$(az afd endpoint show -g "$RG" --profile-name "$PROFILE_NAME" -n "$MCP_CORE_EP" --query "hostName" -o tsv)
MCP_FLOW_HOST=$(az afd endpoint show -g "$RG" --profile-name "$PROFILE_NAME" -n "$MCP_FLOW_EP" --query "hostName" -o tsv)

echo "[afd] Endpoints:"
echo "  API: https://${API_HOST}"
echo "  WEB: https://${WEB_HOST}"
echo "  MCP Core: https://${MCP_CORE_HOST}"
echo "  MCP Flow: https://${MCP_FLOW_HOST}"

# Patch HostNameOverride into configmaps (host only)
kubectl -n "$NS" patch configmap api-main-config --type='merge' -p "{\"data\":{\"ServiceConfiguration__HostNameOverride__Api\":\"$API_HOST\",\"ServiceConfiguration__HostNameOverride__Web\":\"$WEB_HOST\"}}" >/dev/null 2>&1 || true
kubectl -n "$NS" patch configmap web-docgen-config --type='merge' -p "{\"data\":{\"ServiceConfiguration__HostNameOverride__Api\":\"$API_HOST\",\"ServiceConfiguration__HostNameOverride__Web\":\"$WEB_HOST\"}}" >/dev/null 2>&1 || true

echo "[afd] HostNameOverride applied: API=$API_HOST, WEB=$WEB_HOST"

# Ensure API/WEB/MCP pods pick up updated HostNameOverride immediately
TS=$(date +%Y%m%d%H%M%S)
echo "[afd] Bumping config versions and restarting deployments to pick up HostNameOverride"

# Annotate ConfigMaps to force change detection
for cm in api-main-config web-docgen-config mcpserver-core-config mcpserver-flow-config; do
  kubectl -n "$NS" annotate configmap "$cm" "greenlight-config-version=$TS" --overwrite >/dev/null 2>&1 || true
done

# Roll deployments and wait
for dep in api-main-deployment web-docgen-deployment mcpserver-core-deployment mcpserver-flow-deployment; do
  echo "[afd] Rolling restart: $dep"
  kubectl -n "$NS" rollout restart deployment "$dep" >/dev/null 2>&1 || true
  kubectl -n "$NS" rollout status deployment "$dep" --timeout=180s >/dev/null 2>&1 || true
done

echo "[afd] API/WEB/MCP deployments restarted to apply HostNameOverride"
