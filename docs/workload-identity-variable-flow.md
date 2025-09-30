# Workload Identity Variable Flow

This document traces how workload identity variables flow from AKS provisioning through to deployment.

## 1. AKS Cluster Provisioning (`provision-aks-cluster.sh`)

Provisioning enables the AKS OIDC issuer and can still create a default user-assigned managed identity (`uami-$CLUSTER_NAME`). That historical identity remains valid, but the Azure DevOps pipeline now prefers an environment-scoped identity so each namespace/environment can authenticate independently while sharing the same cluster.

## 2. ADO Variable Group Storage

For each deployment environment we track the identity that should be used by workloads in that namespace:

| Variable Name | Source | Example Value |
|--------------|--------|---------------|
| `AKS_CLUSTER_NAME` | Provisioned cluster name | `aks-greenlight-private` |
| `AKS_RESOURCE_GROUP` | AKS cluster resource group | `rg-greenlight-aks-swedencentral` |
| `WORKLOAD_IDENTITY_NAME` | Created per environment | `uami-demo` |
| `WORKLOAD_IDENTITY_CLIENT_ID` | From identity creation | `4a3b5c6d-8edf-45df-8538-05df6c552137` |
| `WORKLOAD_IDENTITY_PRINCIPAL_ID` | From identity creation | `0a691b25-397d-4796-977c-f7db884b2cd6` |
| `WORKLOAD_IDENTITY_RESOURCE_ID` | From identity creation | `/subscriptions/.../resourceGroups/rg-greenlight-adodemo/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-demo` |
| `WORKLOAD_IDENTITY_SERVICE_ACCOUNT` | Fixed | `greenlight-app` |
| `AKS_OIDC_ISSUER` | From `az aks show` | `https://swedencentral.oic.prod-aks.azure.com/...` |
| `WORKLOAD_IDENTITY_FEDERATED_SUBJECT` | Constructed | `system:serviceaccount:greenlight-demo:greenlight-app` |

## 3. Pipeline Variable Usage (`azure-pipelines-modern.yml`)

### During AKS Provisioning (Optional)
```yaml
# Lines 190-221 - If DEPLOY_AKS=true
# Captures workload identity values and updates variable group
WI_CLIENT_ID=$(jq -r '.workloadIdentity.clientId')
echo "##vso[task.setvariable variable=WORKLOAD_IDENTITY_CLIENT_ID]$WI_CLIENT_ID"
```

### During Azure Deployment
```yaml
AKS_NAME="${AKS_CLUSTER_NAME:-aks-$(AZURE_RESOURCE_GROUP)}"
WI_ENV_NAME="${ENVIRONMENT_NAME:-${AKS_NAMESPACE:-$(GREENLIGHT_NAMESPACE)}}"
WI_NAME=$(echo "uami-${WI_ENV_NAME}" | tr '[:upper:]' '[:lower:]' | tr -c 'a-z0-9-' '-')
```

### During Workload Identity Setup
```yaml
# Lines 275-279
"$SCRIPTS_DIR/build-modern-setup-workload-identity.sh" \
  "$(AZURE_RESOURCE_GROUP)" \
  "${AKS_RESOURCE_GROUP:-$(AZURE_RESOURCE_GROUP)}" \
  "$AKS_NAME" \
  "$(AKS_NAMESPACE)" \
  "$WORKLOAD_IDENTITY_SERVICE_ACCOUNT"
```

## 4. Workload Identity Setup Script (`build-modern-setup-workload-identity.sh`)

### Input Parameters
```bash
ENV_RESOURCE_GROUP=${1:?Missing environment resource group}
AKS_RESOURCE_GROUP=${2:?Missing AKS resource group}  # Allows shared cluster
AKS_NAME=${3:?Missing AKS cluster name}
NAMESPACE=${4:-greenlight-dev}
SERVICE_ACCOUNT=${5:-greenlight-app}
```

### Constructs Identity Name
```bash
# Line 16
DEFAULT_WI_NAME="uami-${ENVIRONMENT_NAME:-${NAMESPACE}}"
IDENTITY_NAME="${WORKLOAD_IDENTITY_NAME:-$DEFAULT_WI_NAME}"
# Results in: uami-demo (per environment)
```

## 5. Helm Deployment (`build-modern-helm-deploy.sh`)

### Uses Workload Identity Variables
```bash
# Lines 546-563 - If WORKLOAD_IDENTITY_CLIENT_ID is set
# Calls scripts to inject workload identity configuration
```

## Variable Name Mapping Summary

| Context | Variable Name | Value Pattern |
|---------|--------------|---------------|
| **provision-aks-cluster.sh** | `$WI_IDENTITY_NAME` (legacy default) | `uami-$CLUSTER_NAME` |
| **ADO Variable Group** | `WORKLOAD_IDENTITY_NAME` | `uami-${ENVIRONMENT_NAME}` (e.g., `uami-demo`) |
| | `WORKLOAD_IDENTITY_RESOURCE_ID` | `/subscriptions/.../resourceGroups/<env-rg>/.../uami-${ENVIRONMENT_NAME}` |
| **Pipeline (azure-pipelines-modern.yml)** | `WI_NAME` (local) | Sanitized `uami-${ENVIRONMENT_NAME:-$AKS_NAMESPACE}` |
| **build-modern-setup-workload-identity.sh** | `$IDENTITY_NAME` | `${WORKLOAD_IDENTITY_NAME:-uami-${ENVIRONMENT_NAME:-${NAMESPACE}}}` |

## Critical Points

1. **Identity Creation**: The pipeline creates or reuses a user-assigned managed identity in the deployment resource group (`uami-${ENVIRONMENT_NAME}` by default).
2. **Scoped Access**: Each namespace/environment can use a distinct identity even when sharing the same AKS cluster.
3. **Overrides**: Set `WORKLOAD_IDENTITY_NAME` in the variable group to override the default naming if needed.
4. **Federated Subject**: The script always emits the subject `system:serviceaccount:<namespace>:<serviceAccount>` for the federated credential, matching Helm deployments.

## Restoring Missing Variables

If variables are missing from the ADO variable group, run:
```bash
./build/scripts/restore-workload-identity-variables.sh \
  rg-greenlight-adodev \
  aks-greenlight-adodev \
  greenlight-modern-dev \
  "https://dev.azure.com/your-org" \
  industry-permitting
```

This will:
1. Find the existing environment identity (for example `uami-demo`)
2. Extract its client ID, principal ID, and resource ID
3. Get the AKS OIDC issuer
4. Update the ADO variable group with all values