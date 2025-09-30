# Workload Identity Authentication for Greenlight

## Overview
All authentication to Azure resources (Storage, SQL, etc.) MUST use workload identity via DefaultAzureCredential. Storage account keys are NOT allowed.

## Key Components

### 1. Managed Identity
- **Identity Name**: environment-specific (e.g., `uami-demo`, override via `WORKLOAD_IDENTITY_NAME`)
- **Service Account**: `greenlight-app`
- **Namespace**: `greenlight-dev`

### 2. Required Permissions

#### Storage Accounts (for Orleans)
- Storage Blob Data Contributor
- Storage Table Data Contributor
- Storage Queue Data Contributor

#### SQL Database
- db_owner role
- CONTROL permissions (for DB SetupManager DDL operations)

### 3. Environment Variables Required
The following environment variables MUST be set on all pods for DefaultAzureCredential to work:

```yaml
AZURE_CLIENT_ID: <workload-identity-client-id>
AZURE_TENANT_ID: <tenant-id>
AZURE_AUTHORITY_HOST: https://login.microsoftonline.com/
AZURE_USE_WORKLOAD_IDENTITY: "true"
Orleans__Azure__UseDefaultAzureCredential: "true"
```

### 4. Connection String Format
When using workload identity, connection strings should be **endpoint-only**:

```yaml
# Correct - endpoint only, DefaultAzureCredential handles auth
ConnectionStrings__blob-orleans: https://storage.blob.core.windows.net/
ConnectionStrings__clustering: https://storage.table.core.windows.net/
ConnectionStrings__checkpointing: https://storage.table.core.windows.net/

# WRONG - never include keys
ConnectionStrings__blob-orleans: DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...
```

### 5. SQL Connection String
SQL uses Active Directory Default authentication:

```yaml
ConnectionStrings__ProjectVicoDB: Server=tcp:sqlserver.database.windows.net,1433;Encrypt=True;Authentication='Active Directory Default';Database=ProjectVicoDB
```

## Pipeline Configuration

### Variable Groups Required
Configure these in your ADO variable group:

```yaml
WORKLOAD_IDENTITY_NAME: uami-demo
WORKLOAD_IDENTITY_CLIENT_ID: <client-id>
WORKLOAD_IDENTITY_PRINCIPAL_ID: <principal-id>
WORKLOAD_IDENTITY_SERVICE_ACCOUNT: greenlight-app
```

### Scripts Flow

1. **build-modern-setup-workload-identity.sh**
   - Creates/configures managed identity
   - Grants storage and SQL permissions
   - Sets up federated credentials
   - Creates Kubernetes service account

2. **build-modern-inject-workload-identity-env.sh**
   - Injects environment variables into Helm templates
   - Adds service account to deployments
   - Creates workload identity ConfigMap

3. **build-modern-helm-deploy.sh**
   - Calls workload identity setup scripts
   - Deploys with proper authentication config

## Troubleshooting

### Orleans Storage Authentication Errors
```
Azure.RequestFailedException: This request is not authorized to perform this operation.
Status: 403 (Forbidden)
ErrorCode: AuthorizationFailure
```

**Solution**: Ensure workload identity has Storage Blob/Table/Queue Data Contributor roles.

### SQL Authentication Errors
```
Microsoft.Data.SqlClient.SqlException: Login failed for user '<token-identified principal>'
```

**Solution**:
1. Verify workload identity is granted db_owner role
2. Check AZURE_CLIENT_ID environment variable is set
3. Ensure SQL connection string uses 'Active Directory Default'

### Verifying Configuration

Check service account:
```bash
kubectl get serviceaccount greenlight-app -n greenlight-dev -o yaml
```

Check pod environment:
```bash
kubectl describe pod <pod-name> -n greenlight-dev | grep -A 20 "Environment:"
```

Check workload identity permissions:
```bash
az role assignment list --assignee <principal-id> --all
```

## Important Notes

1. **NEVER** use storage account keys
2. **NEVER** hardcode credentials
3. **ALWAYS** use endpoint-only connection strings with workload identity
4. **ALWAYS** ensure pods use the correct service account
5. The `AzureCredentialHelper` in the application handles authentication via DefaultAzureCredential