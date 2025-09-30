#!/usr/bin/env bash
set -euo pipefail
# Updates an Azure DevOps variable group with workload identity values captured during pipeline run.
# Usage: build-modern-update-wi-variable-group.sh <variable-group-name> <org-url> <project>
# Requires: az devops extension

VG_NAME=${1:?Missing variable group name}
ADO_ORG=${2:?Missing org url}
ADO_PROJECT=${3:?Missing project}

if ! command -v az >/dev/null 2>&1; then
  echo "[wi-vg-update] Azure CLI not installed" >&2
  exit 1
fi

az devops configure --defaults organization="$ADO_ORG" project="$ADO_PROJECT" >/dev/null
VG_ID=$(az pipelines variable-group list --query "[?name=='$VG_NAME'].id | [0]" -o tsv)
if [[ -z "$VG_ID" ]]; then
  echo "[wi-vg-update] Variable group $VG_NAME not found"
  exit 0
fi

update_var() {
  local key=$1
  local value=$2
  if [[ -z "$value" ]]; then return; fi
  # Only update if existing different or empty
  existing=$(az pipelines variable-group variable list --group-id "$VG_ID" --query "$key.value" -o tsv 2>/dev/null || true)
  if [[ "$existing" != "$value" ]]; then
    az pipelines variable-group variable create --group-id "$VG_ID" --name "$key" --value "$value" >/dev/null || \
      az pipelines variable-group variable update --group-id "$VG_ID" --name "$key" --value "$value" >/dev/null || true
    echo "[wi-vg-update] Updated $key"
  else
    echo "[wi-vg-update] $key unchanged"
  fi
}

update_var WORKLOAD_IDENTITY_NAME "${WORKLOAD_IDENTITY_NAME:-}"
update_var WORKLOAD_IDENTITY_CLIENT_ID "${WORKLOAD_IDENTITY_CLIENT_ID:-}"
update_var WORKLOAD_IDENTITY_PRINCIPAL_ID "${WORKLOAD_IDENTITY_PRINCIPAL_ID:-}"
update_var WORKLOAD_IDENTITY_RESOURCE_ID "${WORKLOAD_IDENTITY_RESOURCE_ID:-}"
update_var WORKLOAD_IDENTITY_SERVICE_ACCOUNT "${WORKLOAD_IDENTITY_SERVICE_ACCOUNT:-greenlight-app}"
update_var AKS_OIDC_ISSUER "${AKS_OIDC_ISSUER:-}"
update_var WORKLOAD_IDENTITY_FEDERATED_SUBJECT "${WORKLOAD_IDENTITY_FEDERATED_SUBJECT:-}"

echo "[wi-vg-update] Completed workload identity variable group synchronization"
