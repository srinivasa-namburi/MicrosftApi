# ADO Variable Group Settings for Greenlight

## Required Variables for Workload Identity

These variables should be in your ADO variable group for each deployment environment (the pipeline will create/update the identity if it is missing):

### Core Variables
```yaml
# Azure Resources
AZURE_RESOURCE_GROUP: rg-greenlight-adodemo
AZURE_LOCATION: swedencentral
AKS_CLUSTER_NAME: aks-greenlight-adodev
AKS_NAMESPACE: greenlight-demo

# Workload Identity (created per environment by the pipeline)
WORKLOAD_IDENTITY_NAME: uami-demo
WORKLOAD_IDENTITY_CLIENT_ID: 4a3b5c6d-8edf-45df-8538-05df6c552137
WORKLOAD_IDENTITY_PRINCIPAL_ID: 0a691b25-397d-4796-977c-f7db884b2cd6
WORKLOAD_IDENTITY_RESOURCE_ID: /subscriptions/<subscription-id>/resourceGroups/rg-greenlight-adodemo/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-demo
WORKLOAD_IDENTITY_SERVICE_ACCOUNT: greenlight-app

# AKS OIDC Configuration
AKS_OIDC_ISSUER: https://swedencentral.oic.prod-aks.azure.com/f34f6f92-ba9e-429d-b284-b3df0dd765d0/6fb3df48-f16a-4581-86ca-615cd61ee731/
WORKLOAD_IDENTITY_FEDERATED_SUBJECT: system:serviceaccount:greenlight-demo:greenlight-app
```

### SQL Admin Identity (separate from workload identity)
```yaml
# SQL Server managed identity admin
SQL_ADMIN_IDENTITY_NAME: sqldocgen-admin-xlssrracdoyrk
SQL_ADMIN_CLIENT_ID: 6f3b89ff-9101-4420-b105-5abac3e348ce
SQL_ADMIN_PRINCIPAL_ID: dd0a25e3-7a57-43e5-8ce6-d7e42bed2a56
```

## How These Are Used

1. **During AKS Provisioning** (`provision-aks-cluster.sh`):
   - Enables the OIDC issuer on the cluster
   - May create a legacy identity (`uami-$CLUSTER_NAME`) but the pipeline no longer depends on it

2. **During Azure Deployment** (`build-modern-deploy-azure.sh`):
   - Sets `WI_NAME=$(echo "uami-${ENVIRONMENT_NAME:-$AKS_NAMESPACE}" ...)`
   - Passes that name to the workload identity setup script for creation/updates

3. **During Workload Identity Setup** (`build-modern-setup-workload-identity.sh`):
   - Creates or reuses the identity in `$(AZURE_RESOURCE_GROUP)`
   - Grants Storage Blob/Table/Queue Data Contributor roles on environment resources
   - Grants SQL db_owner permissions if no custom admin is present
   - Creates/updates Kubernetes service account + federated credential

4. **During Helm Deployment** (`build-modern-helm-deploy.sh`):
   - Injects workload identity environment variables
   - Configures pods to use `greenlight-app` service account

## Important Notes

1. The pipeline computes a sanitized `uami-${ENVIRONMENT_NAME}` value when `WORKLOAD_IDENTITY_NAME` is not provided.
2. Multiple environments can share the same AKS cluster because each namespace has its own managed identity.
3. The SQL admin identity (`sqldocgen-admin-xlssrracdoyrk`) is separate and used for SQL Server Azure AD administration.
4. All pods must use the `greenlight-app` service account to leverage workload identity.

## Verifying Configuration

Check if variables are properly set:
```bash
# In pipeline
echo "WORKLOAD_IDENTITY_NAME: ${WORKLOAD_IDENTITY_NAME}"
echo "WORKLOAD_IDENTITY_CLIENT_ID: ${WORKLOAD_IDENTITY_CLIENT_ID}"
echo "WORKLOAD_IDENTITY_PRINCIPAL_ID: ${WORKLOAD_IDENTITY_PRINCIPAL_ID}"
```

Check identity in Azure:
```bash
az identity show --name uami-demo --resource-group rg-greenlight-adodemo
```

Check federated credential:
```bash
az identity federated-credential list --identity-name uami-demo --resource-group rg-greenlight-adodemo
```