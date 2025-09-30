#!/usr/bin/env bash
set -euo pipefail

show_usage() {
  cat >&2 <<'USAGE'
Usage: run-db-setupmanager.sh [resource-group] [aks-cluster-name] [namespace] [sleep_before_job_seconds]

- resource-group: AKS cluster resource group (defaults to AZURE_RESOURCE_GROUP if set)
- aks-cluster-name: AKS cluster name (defaults to AKS_CLUSTER_NAME if set)
- namespace: Kubernetes namespace to run in (defaults to AKS_NAMESPACE if set)
- sleep_before_job_seconds: Optional delay before the job starts (default 120)
USAGE
}

RESOURCE_GROUP=${1:-${AZURE_RESOURCE_GROUP:-}}
CLUSTER_NAME=${2:-${AKS_CLUSTER_NAME:-}}
NAMESPACE=${3:-${AKS_NAMESPACE:-}}
SLEEP_BEFORE_JOB=${4:-0}

if [[ -z "$RESOURCE_GROUP" ]]; then
  read -rp "AKS resource group (cluster RG): " RESOURCE_GROUP
fi
if [[ -z "$CLUSTER_NAME" ]]; then
  read -rp "AKS cluster name: " CLUSTER_NAME
fi
if [[ -z "$NAMESPACE" ]]; then
  read -rp "Kubernetes namespace (e.g. greenlight-dev): " NAMESPACE
fi

if [[ -z "$RESOURCE_GROUP" || -z "$CLUSTER_NAME" || -z "$NAMESPACE" ]]; then
  show_usage
  exit 1
fi

if ! command -v az >/dev/null 2>&1; then
  echo "[dbjob-cli] Azure CLI (az) is required." >&2
  exit 1
fi

SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/pipeline-internal"
RUNNER="$SCRIPTS_DIR/build-modern-run-db-setup-job.sh"
if [[ ! -f "$RUNNER" ]]; then
  echo "[dbjob-cli] Unable to locate pipeline script: $RUNNER" >&2
  exit 1
fi

echo "[dbjob-cli] Fetching AKS credentials for $RESOURCE_GROUP / $CLUSTER_NAME"
az aks get-credentials \
  --resource-group "$RESOURCE_GROUP" \
  --name "$CLUSTER_NAME" \
  --overwrite-existing \
  --only-show-errors

if [[ "$SLEEP_BEFORE_JOB" -gt 0 ]]; then
  echo "[dbjob-cli] Invoking db setup job in namespace '$NAMESPACE' (sleep $SLEEP_BEFORE_JOB s)"
else
  echo "[dbjob-cli] Invoking db setup job in namespace '$NAMESPACE'"
fi
"$RUNNER" "$NAMESPACE" "$SLEEP_BEFORE_JOB"
