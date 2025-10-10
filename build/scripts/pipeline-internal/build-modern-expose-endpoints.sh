#!/usr/bin/env bash
set -euo pipefail

# Expose AKS services via public LoadBalancer and emit their external IPs.
# Usage: build-modern-expose-endpoints.sh <namespace> [deployment_model]
# Env toggles:
#   DISABLE_EXPOSEDENDPOINTS=true  -> no-op

NS=${1:-greenlight-dev}
DEPLOYMENT_MODEL=${2:-public}

if [[ "${DISABLE_EXPOSEDENDPOINTS:-false}" == "true" ]]; then
  echo "[expose] DISABLE_EXPOSEDENDPOINTS=true - skipping LB exposure"
  exit 0
fi

echo "[expose] Ensuring Services are type LoadBalancer (public mode) in namespace: $NS"

ensure_lb() {
  local svc=$1
  local port=${2:-8080}

  echo "[expose] Ensuring LoadBalancer service for $svc HTTP-only access"

  # Create/update a separate LoadBalancer service for HTTP only, leaving original service as ClusterIP
  local lb_svc="${svc}-lb"

  # Get the original service selector
  local selector=$(kubectl -n "$NS" get service "$svc" -o json | jq -c '.spec.selector')

  # Check if LoadBalancer service already exists and preserve its external IP
  local existing_ip=""
  if kubectl -n "$NS" get service "$lb_svc" >/dev/null 2>&1; then
    existing_ip=$(kubectl -n "$NS" get service "$lb_svc" -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "")
    echo "[expose] Found existing $lb_svc with IP: ${existing_ip:-pending}"
  fi

  # Create/update LoadBalancer service with HTTP port only
  local load_balancer_ip_spec=""
  if [[ -n "$existing_ip" && "$existing_ip" != "null" ]]; then
    load_balancer_ip_spec="  loadBalancerIP: $existing_ip"
    echo "[expose] Preserving existing IP: $existing_ip"
  fi

  kubectl -n "$NS" apply -f - <<EOF
apiVersion: v1
kind: Service
metadata:
  name: $lb_svc
  namespace: $NS
spec:
  type: LoadBalancer
$load_balancer_ip_spec
  selector: $selector
  ports:
  - name: http
    port: 80
    targetPort: 8080
    protocol: TCP
EOF

  echo "[expose] Ensured $lb_svc for public HTTP access (IP preserved), $svc remains ClusterIP for internal/Orleans"
}

ensure_internal_lb() {
  local svc=$1
  local lb_svc="${svc}-lb"

  echo "[expose] Ensuring internal LoadBalancer for $svc"

  local svc_json
  if ! svc_json=$(kubectl -n "$NS" get service "$svc" -o json); then
    echo "[expose] ERROR: base service $svc not found" >&2
    return 1
  fi

  local selector
  selector=$(echo "$svc_json" | jq -c '.spec.selector // {}')
  if [[ -z "$selector" || "$selector" == "null" || "$selector" == "{}" ]]; then
    echo "[expose] ERROR: service $svc has no selector; cannot create LB" >&2
    return 1
  fi

  local target_port
  target_port=$(echo "$svc_json" | jq -r '.spec.ports[0].targetPort // "8080"')

  local existing_ip=""
  if kubectl -n "$NS" get service "$lb_svc" >/dev/null 2>&1; then
    existing_ip=$(kubectl -n "$NS" get service "$lb_svc" -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "")
    echo "[expose] Found existing $lb_svc with IP: ${existing_ip:-pending}"
  fi

  local load_balancer_ip_spec=""
  if [[ -n "$existing_ip" && "$existing_ip" != "null" ]]; then
    load_balancer_ip_spec="  loadBalancerIP: $existing_ip"
    echo "[expose] Preserving existing internal IP: $existing_ip"
  fi

  local target_port_field
  if [[ "$target_port" =~ ^[0-9]+$ ]]; then
    target_port_field=$target_port
  else
    target_port_field="\"$target_port\""
  fi

  cat <<EOF | kubectl -n "$NS" apply -f -
apiVersion: v1
kind: Service
metadata:
  name: $lb_svc
  namespace: $NS
  annotations:
    service.beta.kubernetes.io/azure-load-balancer-internal: "true"
spec:
  type: LoadBalancer
$load_balancer_ip_spec
  selector: $selector
  ports:
  - name: http
    port: 80
    targetPort: $target_port_field
    protocol: TCP
EOF

  echo "[expose] Ensured internal LoadBalancer $lb_svc for $svc"
}

case "$DEPLOYMENT_MODEL" in
  public)
    echo "[expose] Public deployment - creating external LoadBalancers..."
    # Ensure all main services are ClusterIP for internal communication
    for base in api-main web-docgen mcpserver-core mcpserver-flow; do
      kubectl -n "$NS" patch service "$base" --type='merge' -p '{"spec":{"type":"ClusterIP"}}' 2>/dev/null || true
      ensure_lb "$base"
    done
    ;;

  hybrid)
    echo "[expose] Hybrid deployment - creating internal LoadBalancers for VNET access..."
    # Create internal load balancers for hybrid mode (accessible within VNET)
    for base in api-main web-docgen mcpserver-core mcpserver-flow; do
      ensure_internal_lb "$base"
    done
    ;;

  private)
    echo "[expose] Private deployment - no external endpoints will be created"
    echo "[expose] Services remain ClusterIP only, accessible within cluster"
    # Don't create any LoadBalancers for private mode
    ;;

  *)
    echo "[expose] Unknown deployment model: $DEPLOYMENT_MODEL"
    exit 1
    ;;
esac

if [[ "$DEPLOYMENT_MODEL" == "private" ]]; then
  echo "[expose] Private deployment - no endpoints to expose"
  # Create empty/placeholder endpoints file for private mode
  cat > exposed-endpoints.env <<EOF
# Private deployment - no external endpoints
EXPOSED_API_MAIN=
EXPOSED_WEB_DOCGEN=
EXPOSED_MCP_CORE=
EXPOSED_MCP_FLOW=
DEPLOYMENT_MODE=private
EOF
  echo "[expose] Wrote placeholder exposed-endpoints.env for private deployment"
else
  # Wait for IPs for public and hybrid modes
  echo "[expose] Waiting for LoadBalancer IPs..."
  wait_ip() {
    local svc=$1; local tries=60
    while [[ $tries -gt 0 ]]; do
      ip=$(kubectl -n "$NS" get svc "$svc" -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || true)
      host=$(kubectl -n "$NS" get svc "$svc" -o jsonpath='{.status.loadBalancer.ingress[0].hostname}' 2>/dev/null || true)
      if [[ -n "$ip" || -n "$host" ]]; then
        echo "${ip:-$host}"; return 0
      fi
      sleep 5; tries=$((tries-1))
    done
    return 1
  }

  API_IP=$(wait_ip api-main-lb)
  DOCGEN_IP=$(wait_ip web-docgen-lb)
  MCP_CORE_IP=$(wait_ip mcpserver-core-lb)
  MCP_FLOW_IP=$(wait_ip mcpserver-flow-lb)

  echo "[expose] api-main-lb: $API_IP"
  echo "[expose] web-docgen-lb: $DOCGEN_IP"
  echo "[expose] mcpserver-core-lb: $MCP_CORE_IP"
  echo "[expose] mcpserver-flow-lb: $MCP_FLOW_IP"

  # Export for downstream scripts and persist
  if [[ "$DEPLOYMENT_MODEL" == "hybrid" ]]; then
    echo "[expose] Note: These are internal IPs accessible only within VNET"
  fi

  HYBRID_METADATA=""
  if [[ "$DEPLOYMENT_MODEL" == "hybrid" ]]; then
    echo "[expose] Gathering AKS load balancer metadata for Private Link consumers"

    if [[ -z "${AKS_RESOURCE_GROUP:-}" && -z "${AZURE_RESOURCE_GROUP:-}" ]]; then
      echo "[expose] ERROR: AKS_RESOURCE_GROUP or AZURE_RESOURCE_GROUP must be set for hybrid deployments" >&2
      exit 1
    fi

    AKS_RG="${AKS_RESOURCE_GROUP:-${AZURE_RESOURCE_GROUP}}"
    AKS_NAME="${AKS_CLUSTER_NAME:-aks-${AKS_RG}}"

    if ! az aks show --resource-group "$AKS_RG" --name "$AKS_NAME" >/dev/null 2>&1; then
      echo "[expose] ERROR: Unable to resolve AKS cluster $AKS_NAME in $AKS_RG" >&2
      exit 1
    fi

    NODE_RG=$(az aks show --resource-group "$AKS_RG" --name "$AKS_NAME" --query "nodeResourceGroup" -o tsv)
    AKS_LOCATION=$(az aks show --resource-group "$AKS_RG" --name "$AKS_NAME" --query "location" -o tsv)
    if [[ -n "${AZURE_SUBSCRIPTION_ID:-}" ]]; then
      SUBSCRIPTION_ID="$AZURE_SUBSCRIPTION_ID"
    else
      SUBSCRIPTION_ID=$(az account show --query id -o tsv 2>/dev/null || echo "")
    fi

    if [[ -z "$NODE_RG" || -z "$AKS_LOCATION" ]]; then
      echo "[expose] ERROR: Failed to resolve AKS node resource group or location" >&2
      exit 1
    fi

    resolve_frontend_id() {
      local target_ip=$1
      local fe_id=""
      for lb_name in $(az network lb list --resource-group "$NODE_RG" --query "[].name" -o tsv); do
        fe_id=$(az network lb frontend-ip list --resource-group "$NODE_RG" --lb-name "$lb_name" --query "[?privateIPAddress=='$target_ip'].id" -o tsv | head -n1)
        if [[ -n "$fe_id" ]]; then
          echo "$fe_id"
          return 0
        fi
      done
      return 1
    }

    API_FE_ID=""
    DOCGEN_FE_ID=""
    MCP_CORE_FE_ID=""
    MCP_FLOW_FE_ID=""

    if [[ -n "$API_IP" ]]; then
      API_FE_ID=$(resolve_frontend_id "$API_IP") || true
    fi
    if [[ -n "$DOCGEN_IP" ]]; then
      DOCGEN_FE_ID=$(resolve_frontend_id "$DOCGEN_IP") || true
    fi
    if [[ -n "$MCP_CORE_IP" ]]; then
      MCP_CORE_FE_ID=$(resolve_frontend_id "$MCP_CORE_IP") || true
    fi
    if [[ -n "$MCP_FLOW_IP" ]]; then
      MCP_FLOW_FE_ID=$(resolve_frontend_id "$MCP_FLOW_IP") || true
    fi

    if [[ -z "$API_FE_ID" || -z "$DOCGEN_FE_ID" || -z "$MCP_CORE_FE_ID" || -z "$MCP_FLOW_FE_ID" ]]; then
      echo "[expose] WARNING: Unable to resolve one or more load balancer frontend IDs. Downstream private link setup may fail." >&2
    fi

    HYBRID_METADATA="EXPOSED_API_MAIN_FRONTEND_ID=$API_FE_ID
EXPOSED_WEB_DOCGEN_FRONTEND_ID=$DOCGEN_FE_ID
EXPOSED_MCP_CORE_FRONTEND_ID=$MCP_CORE_FE_ID
EXPOSED_MCP_FLOW_FRONTEND_ID=$MCP_FLOW_FE_ID
EXPOSED_AKS_NODE_RESOURCE_GROUP=$NODE_RG
EXPOSED_AKS_CLUSTER_LOCATION=$AKS_LOCATION
EXPOSED_AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID"
  fi

  {
    echo "EXPOSED_API_MAIN=http://$API_IP"
    echo "EXPOSED_WEB_DOCGEN=http://$DOCGEN_IP"
    echo "EXPOSED_MCP_CORE=http://$MCP_CORE_IP"
    echo "EXPOSED_MCP_FLOW=http://$MCP_FLOW_IP"
    echo "DEPLOYMENT_MODE=$DEPLOYMENT_MODEL"
    if [[ -n "$HYBRID_METADATA" ]]; then
      echo "$HYBRID_METADATA"
    else
      echo "EXPOSED_API_MAIN_FRONTEND_ID="
      echo "EXPOSED_WEB_DOCGEN_FRONTEND_ID="
      echo "EXPOSED_MCP_CORE_FRONTEND_ID="
      echo "EXPOSED_MCP_FLOW_FRONTEND_ID="
      echo "EXPOSED_AKS_NODE_RESOURCE_GROUP="
      echo "EXPOSED_AKS_CLUSTER_LOCATION="
      echo "EXPOSED_AZURE_SUBSCRIPTION_ID="
    fi
  } > exposed-endpoints.env

  echo "[expose] Wrote exposed endpoints to exposed-endpoints.env"
fi
