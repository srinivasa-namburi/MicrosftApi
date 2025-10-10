#!/usr/bin/env bash
set -euo pipefail

# Copyright (c) Microsoft Corporation. All rights reserved.

# Apply Kubernetes resource configuration to running deployments
# This script patches existing Kubernetes resources after Helm deployment
# Usage: build-modern-apply-k8s-resources.sh <namespace> [resources-json]

NAMESPACE=${1:-greenlight-demo}
RESOURCES_JSON=${2:-${KUBERNETES_RESOURCES_CONFIG:-}}
ENABLE_SPOTNODES=${ENABLE_SPOTNODES:-false}

# Exclusion list: workloads that should NOT be moved to spot nodes
# Redis and other stateful services should remain on regular nodes
EXCLUDED_FROM_SPOT=("redis-statefulset" "redis-signalr-statefulset")

if [[ -z "$RESOURCES_JSON" ]] && [[ "$ENABLE_SPOTNODES" != "true" ]]; then
  echo "[k8s-resources] No KUBERNETES_RESOURCES_CONFIG provided and ENABLE_SPOTNODES not enabled, skipping"
  exit 0
fi

# Validate JSON if provided
if [[ -n "$RESOURCES_JSON" ]]; then
  if ! echo "$RESOURCES_JSON" | jq empty 2>/dev/null; then
    echo "[k8s-resources] ERROR: Invalid JSON in KUBERNETES_RESOURCES_CONFIG"
    echo "[k8s-resources] Content preview: ${RESOURCES_JSON:0:200}"
    exit 1
  fi
fi

echo "[k8s-resources] Applying Kubernetes configuration to namespace: $NAMESPACE"
[[ -n "$RESOURCES_JSON" ]] && echo "[k8s-resources]   - Resource limits/requests: enabled"
[[ "$ENABLE_SPOTNODES" == "true" ]] && echo "[k8s-resources]   - Spot node affinity: enabled"

# Helper function to check if a workload should be excluded from spot nodes
is_excluded_from_spot() {
  local name="$1"
  for excluded in "${EXCLUDED_FROM_SPOT[@]}"; do
    if [[ "$name" == "$excluded" ]]; then
      return 0  # true, is excluded
    fi
  done
  return 1  # false, not excluded
}

# Apply spot node affinity and tolerations to a deployment
apply_spot_config() {
  local deployment_name="$1"
  local namespace="$2"

  # Spot node configuration patch (taint-based, no specific node pool)
  # Uses preferred affinity for the spot=true label
  local spot_patch='
  {
    "spec": {
      "template": {
        "spec": {
          "tolerations": [
            {
              "key": "kubernetes.azure.com/scalesetpriority",
              "operator": "Equal",
              "value": "spot",
              "effect": "NoSchedule"
            }
          ],
          "affinity": {
            "nodeAffinity": {
              "preferredDuringSchedulingIgnoredDuringExecution": [
                {
                  "weight": 100,
                  "preference": {
                    "matchExpressions": [
                      {
                        "key": "spot",
                        "operator": "In",
                        "values": ["true"]
                      }
                    ]
                  }
                }
              ]
            }
          }
        }
      }
    }
  }'

  if kubectl patch deployment "$deployment_name" -n "$namespace" --type strategic --patch "$spot_patch" 2>/dev/null; then
    echo "[k8s-resources]     ✅ Spot configuration applied"
  else
    echo "[k8s-resources]     ⚠️  Failed to apply spot configuration (trying merge strategy)"
    kubectl patch deployment "$deployment_name" -n "$namespace" --type merge --patch "$spot_patch" 2>/dev/null || \
      echo "[k8s-resources]     ❌ Failed to apply spot configuration"
  fi
}

# Get list of service names from JSON config (if provided)
SERVICE_NAMES=""
if [[ -n "$RESOURCES_JSON" ]]; then
  IS_ARRAY=$(echo "$RESOURCES_JSON" | jq 'if type == "array" then "true" else "false" end' -r)

  if [[ "$IS_ARRAY" == "true" ]]; then
    SERVICE_NAMES=$(echo "$RESOURCES_JSON" | jq -r '.[].name')
  else
    SERVICE_NAMES=$(echo "$RESOURCES_JSON" | jq -r 'keys[]')
  fi
fi

for service_name in $SERVICE_NAMES; do
  # Extract config for this service
  if [[ "$IS_ARRAY" == "true" ]]; then
    service_config=$(echo "$RESOURCES_JSON" | jq -c ".[] | select(.name == \"$service_name\")")
  else
    service_config=$(echo "$RESOURCES_JSON" | jq -c ".\"$service_name\"")
  fi

  if [[ -z "$service_config" ]] || [[ "$service_config" == "null" ]]; then
    continue
  fi

  # Convert service name to match Kubernetes naming (hyphens)
  k8s_service_name=$(echo "$service_name" | tr '_' '-')

  echo "[k8s-resources] Processing service: $k8s_service_name"

  # Deployment name has -deployment suffix
  deployment_name="${k8s_service_name}-deployment"

  # Check if deployment exists
  if ! kubectl get deployment "$deployment_name" -n "$NAMESPACE" >/dev/null 2>&1; then
    echo "[k8s-resources]   Deployment $deployment_name not found in namespace $NAMESPACE, skipping"
    continue
  fi

  # Extract resource values
  req_cpu=$(echo "$service_config" | jq -r '.requests.cpu // empty')
  req_mem=$(echo "$service_config" | jq -r '.requests.memory // empty')
  lim_cpu=$(echo "$service_config" | jq -r '.limits.cpu // empty')
  lim_mem=$(echo "$service_config" | jq -r '.limits.memory // empty')
  min_replicas=$(echo "$service_config" | jq -r '.replicas.min // empty')
  max_replicas=$(echo "$service_config" | jq -r '.replicas.max // empty')

  # Build resource patch
  if [[ -n "$req_cpu" ]] || [[ -n "$req_mem" ]] || [[ -n "$lim_cpu" ]] || [[ -n "$lim_mem" ]]; then
    echo "[k8s-resources]   Patching resources for $deployment_name"

    # Build JSON patch for resources
    resources_patch='{"resources":{}}'

    if [[ -n "$req_cpu" ]] || [[ -n "$req_mem" ]]; then
      requests_patch='{"requests":{}}'
      [[ -n "$req_cpu" ]] && requests_patch=$(echo "$requests_patch" | jq --arg cpu "$req_cpu" '.requests.cpu = $cpu')
      [[ -n "$req_mem" ]] && requests_patch=$(echo "$requests_patch" | jq --arg mem "$req_mem" '.requests.memory = $mem')
      resources_patch=$(echo "$resources_patch" | jq --argjson req "$requests_patch" '.resources.requests = $req.requests')
      echo "[k8s-resources]     - requests: cpu=$req_cpu, memory=$req_mem"
    fi

    if [[ -n "$lim_cpu" ]] || [[ -n "$lim_mem" ]]; then
      limits_patch='{"limits":{}}'
      [[ -n "$lim_cpu" ]] && limits_patch=$(echo "$limits_patch" | jq --arg cpu "$lim_cpu" '.limits.cpu = $cpu')
      [[ -n "$lim_mem" ]] && limits_patch=$(echo "$limits_patch" | jq --arg mem "$lim_mem" '.limits.memory = $mem')
      resources_patch=$(echo "$resources_patch" | jq --argjson lim "$limits_patch" '.resources.limits = $lim.limits')
      echo "[k8s-resources]     - limits: cpu=$lim_cpu, memory=$lim_mem"
    fi

    # Apply patch to first container (main app container - uses service name without -deployment suffix)
    patch_json=$(jq -n --argjson res "$resources_patch" '{"spec":{"template":{"spec":{"containers":[{"name":"'"$k8s_service_name"'","resources":$res.resources}]}}}}')

    if kubectl patch deployment "$deployment_name" -n "$NAMESPACE" --type strategic --patch "$patch_json" 2>/dev/null; then
      echo "[k8s-resources]     ✅ Resources patched successfully"
    else
      echo "[k8s-resources]     ⚠️  Failed to patch resources, trying alternative approach"
      # Alternative: patch using merge strategy
      kubectl patch deployment "$deployment_name" -n "$NAMESPACE" --type merge --patch "$patch_json" || \
        echo "[k8s-resources]     ❌ Failed to patch resources"
    fi
  fi

  # Patch replicas if specified
  if [[ -n "$min_replicas" ]]; then
    echo "[k8s-resources]   Setting replicas to $min_replicas for $deployment_name"
    if kubectl scale deployment "$deployment_name" -n "$NAMESPACE" --replicas="$min_replicas" 2>/dev/null; then
      echo "[k8s-resources]     ✅ Replicas set successfully"
    else
      echo "[k8s-resources]     ❌ Failed to set replicas"
    fi
  fi

  # Create or update HPA if both min and max replicas are specified
  if [[ -n "$min_replicas" ]] && [[ -n "$max_replicas" ]] && [[ "$min_replicas" != "$max_replicas" ]]; then
    echo "[k8s-resources]   Creating/updating HPA: min=$min_replicas, max=$max_replicas"

    hpa_name="${deployment_name}-hpa"

    # Check if HPA exists
    if kubectl get hpa "$hpa_name" -n "$NAMESPACE" >/dev/null 2>&1; then
      echo "[k8s-resources]     Updating existing HPA"
      # Patch existing HPA
      kubectl patch hpa "$hpa_name" -n "$NAMESPACE" --type merge --patch "$(cat <<EOF
{
  "spec": {
    "minReplicas": $min_replicas,
    "maxReplicas": $max_replicas
  }
}
EOF
)" && echo "[k8s-resources]     ✅ HPA updated" || echo "[k8s-resources]     ❌ Failed to update HPA"
    else
      echo "[k8s-resources]     Creating new HPA"
      # Create new HPA
      kubectl apply -f - <<EOF
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: $hpa_name
  namespace: $NAMESPACE
  labels:
    app.kubernetes.io/name: $k8s_service_name
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: $deployment_name
  minReplicas: $min_replicas
  maxReplicas: $max_replicas
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
EOF
      if [[ $? -eq 0 ]]; then
        echo "[k8s-resources]     ✅ HPA created successfully"
      else
        echo "[k8s-resources]     ❌ Failed to create HPA"
      fi
    fi
  fi

  # Apply spot node configuration if enabled
  if [[ "$ENABLE_SPOTNODES" == "true" ]]; then
    # Check if this deployment should be excluded from spot nodes
    if is_excluded_from_spot "$deployment_name"; then
      echo "[k8s-resources]   Skipping spot configuration for $deployment_name (excluded)"
    else
      echo "[k8s-resources]   Applying spot node configuration to $deployment_name"
      apply_spot_config "$deployment_name" "$NAMESPACE"
    fi
  fi

  echo ""
done

# If ENABLE_SPOTNODES is true but no RESOURCES_JSON, apply spot config to all deployments
if [[ "$ENABLE_SPOTNODES" == "true" ]] && [[ -z "$RESOURCES_JSON" ]]; then
  echo "[k8s-resources] Applying spot node configuration to all deployments in namespace..."

  ALL_DEPLOYMENTS=$(kubectl get deployments -n "$NAMESPACE" -o jsonpath='{.items[*].metadata.name}')

  for deployment_name in $ALL_DEPLOYMENTS; do
    if is_excluded_from_spot "$deployment_name"; then
      echo "[k8s-resources]   Skipping $deployment_name (excluded from spot nodes)"
    else
      echo "[k8s-resources]   Processing $deployment_name"
      apply_spot_config "$deployment_name" "$NAMESPACE"
    fi
  done
fi

echo "[k8s-resources] Configuration applied successfully"
