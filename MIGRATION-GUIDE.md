# Microsoft Greenlight - Migration Guide

This guide covers common migration scenarios for existing Greenlight deployments.

## Migration Scenarios

### Scenario 1: GitHub Repository-Level to GitHub Environments (Most Common)

**Current State:** You have deployed Greenlight from a GitHub fork using repository-level variables and secrets, without GitHub Environments support.

**Target State:** Modern deployment with GitHub Environments for better environment separation.

#### Step 1: Analyze Current Configuration

First, document your existing repository variables and secrets.

**Via GitHub UI:**
- Go to Settings → Secrets and variables → Actions
- Document Variables and Secrets tabs

**Via GitHub CLI:**
```bash
# List all repository variables
gh variable list

# List all repository secrets (names only, values are hidden)
gh secret list

# Export variables to a file for documentation
gh variable list --json name,value > current-variables.json

# Common variables to check:
gh variable get AZURE_SUBSCRIPTION_ID
gh variable get AZURE_RESOURCE_GROUP
gh variable get AZURE_LOCATION
gh variable get DEPLOYMENT_MODEL
gh variable get MEMORY_BACKEND
```

**Typical Configuration:**
- Variables: `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_LOCATION`, `DEPLOYMENT_MODEL`, `MEMORY_BACKEND`
- Secrets: `AZURE_CREDENTIALS`, `PVICO_ENTRA_CREDENTIALS`, `PVICO_AZUREMAPS_KEY`, `PVICO_OPENAI_CONNECTIONSTRING`

#### Step 2: Create GitHub Environments

**Via GitHub UI:**
1. Go to Settings → Environments
2. Click "New environment"
3. Create environments as needed (e.g., `dev`, `staging`, `prod`)
4. For each environment, configure variables, secrets, and protection rules

**Via GitHub CLI:**
```bash
# Create environments (requires gh 2.30.0+)
gh api repos/:owner/:repo/environments/dev --method PUT
gh api repos/:owner/:repo/environments/staging --method PUT
gh api repos/:owner/:repo/environments/prod --method PUT

# Set environment-specific variables
gh variable set AZURE_RESOURCE_GROUP --env dev --body "rg-greenlight-dev"
gh variable set AZURE_RESOURCE_GROUP --env staging --body "rg-greenlight-staging"
gh variable set AZURE_RESOURCE_GROUP --env prod --body "rg-greenlight-prod"

# Set environment-specific secrets
gh secret set PVICO_OPENAI_CONNECTIONSTRING --env prod --body "$PROD_OPENAI_KEY"

# Add protection rules (requires API call)
gh api repos/:owner/:repo/environments/prod --method PUT \
  --field reviewers[][id]=12345 \
  --field deployment_branch_policy[protected_branches]=true
```

Example environment structure:
```
Repository
├── Environments
│   ├── dev
│   │   ├── Variables: AZURE_RESOURCE_GROUP=rg-greenlight-dev
│   │   └── Secrets: (inherits from repository or override)
│   ├── staging
│   │   ├── Variables: AZURE_RESOURCE_GROUP=rg-greenlight-staging
│   │   └── Secrets: (inherits from repository or override)
│   └── prod
│       ├── Variables: AZURE_RESOURCE_GROUP=rg-greenlight-prod
│       ├── Secrets: PVICO_OPENAI_CONNECTIONSTRING=(production key)
│       └── Protection: Required reviewers
```

#### Step 3: Update Workflow Files

**Old workflow** (without environments):
```yaml
name: Deploy
on:
  workflow_dispatch:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Deploy
        env:
          AZURE_RESOURCE_GROUP: ${{ vars.AZURE_RESOURCE_GROUP }}
```

**New workflow** (with environments):
```yaml
name: Modern Deploy
on:
  workflow_dispatch:
    inputs:
      environment:
        description: 'Environment name'
        required: true
        default: 'dev'
        type: choice
        options:
          - dev
          - staging
          - prod

jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: ${{ github.event.inputs.environment }}
    steps:
      - uses: actions/checkout@v4
      - name: Deploy
        env:
          AZURE_RESOURCE_GROUP: ${{ vars.AZURE_RESOURCE_GROUP }}
```

#### Step 4: Bootstrap the New Environment

Run the bootstrap script to set up your environment configuration:

```bash
# Create environment configuration file
cp build/environment-variables-github-sample.yml build/environment-variables-github-dev.yml

# Edit the file with your values
vi build/environment-variables-github-dev.yml

# Run bootstrap
build/scripts/build-modern-bootstrap-github.sh \
  build/environment-variables-github-dev.yml \
  your-org/your-repo
```

#### Step 5: Migrate Gradually

1. **Keep repository-level variables initially** - They act as defaults
2. **Override in environments** - Add environment-specific overrides
3. **Test with dev environment first** - Validate the new workflow
4. **Move to production** - Once validated, update production

---

### ⚠️ IMPORTANT: Endpoint URLs Will Change

**Unless you have `HOSTNAME_OVERRIDE` configured**, your application endpoints WILL change during migration:

| Component | Container Apps URL | Kubernetes URL |
|-----------|-------------------|----------------|
| Web App | `https://web-docgen.azurecontainerapps.io` | `http://<ingress-ip>` or custom domain |
| API | `https://api-main.azurecontainerapps.io` | `http://<ingress-ip>/api` |

**To maintain the same URLs:**
1. Set up a custom domain with `HOSTNAME_OVERRIDE` before migration
2. Or use Azure Application Gateway / Front Door to maintain consistent endpoints
3. Update all client applications and integrations with new endpoints post-migration

---

### Scenario 2: Container Apps to Kubernetes (Infrastructure Migration)

**Current State:** Running on Azure Container Apps
**Target State:** Running on Azure Kubernetes Service (AKS)

#### Step 1: Provision AKS Cluster

Based on your current deployment model:

**For Public Deployments:**
```bash
# Simple public cluster
build/scripts/provision-aks-cluster.sh \
  $AZURE_RESOURCE_GROUP \
  $AZURE_LOCATION \
  aks-greenlight
```

**For Private Deployments:**
```bash
# Private cluster (requires existing VNET)
build/scripts/provision-aks-cluster.sh \
  $AZURE_RESOURCE_GROUP \
  $AZURE_LOCATION \
  aks-greenlight \
  --deployment-model private \
  --vnet-name your-vnet \
  --subnet-name your-subnet
```

#### Step 2: Update GitHub Variables

Add new variables for Kubernetes deployment:
```yaml
AKS_CLUSTER_NAME: aks-greenlight
AKS_RESOURCE_GROUP: $AZURE_RESOURCE_GROUP  # Can be same or different
AKS_NAMESPACE: greenlight-prod
HELM_RELEASE: greenlight
```

#### Step 3: Deploy to Both (Parallel Running)

During migration, you can run both Container Apps and Kubernetes:
1. Deploy to AKS using new workflow
2. Keep Container Apps running
3. Test thoroughly on AKS
4. Switch traffic when ready
5. Decommission Container Apps

---

### Scenario 3: Azure DevOps Migration (Platform Change)

**Current State:** Using GitHub Actions
**Target State:** Using Azure DevOps Pipelines

#### Step 1: Import Repository to Azure DevOps

```bash
# Clone from GitHub
git clone https://github.com/your-org/your-repo.git
cd your-repo

# Add Azure DevOps remote
git remote add ado https://dev.azure.com/your-org/your-project/_git/your-repo

# Push to Azure DevOps
git push ado --all
git push ado --tags
```

#### Step 2: Create Variable Groups

Map GitHub secrets to ADO variable groups:

| GitHub | Azure DevOps Variable Group |
|--------|----------------------------|
| Repository Secrets | Library → Variable Groups → greenlight-modern-prod |
| Environment Variables | Pipeline → Environments → Production → Variables |

#### Step 3: Configure Service Connection

1. Project Settings → Service connections
2. New service connection → Azure Resource Manager
3. Use existing service principal from `AZURE_CREDENTIALS`

#### Step 4: Set Up Pipeline

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include:
      - main

variables:
  - group: greenlight-modern-prod

stages:
  - template: build/azure-pipelines-modern.yml
```

---

## Common Migration Patterns

### Pattern 1: Secrets Migration

**Complete migration using GitHub CLI:**
```bash
# Step 1: List current repository secrets
gh secret list

# Step 2: For each secret, copy to environment
# Example for PVICO_ENTRA_CREDENTIALS
# Note: You need the actual secret value, as GitHub doesn't allow reading secret values

# Repository level (old) - shared across all deployments
gh secret set PVICO_ENTRA_CREDENTIALS --body "$CREDENTIALS"

# Environment level (new) - environment-specific
gh secret set PVICO_ENTRA_CREDENTIALS --env dev --body "$CREDENTIALS"
gh secret set PVICO_ENTRA_CREDENTIALS --env staging --body "$CREDENTIALS"
gh secret set PVICO_ENTRA_CREDENTIALS --env prod --body "$PROD_CREDENTIALS"

# Step 3: Migrate all secrets in batch
for env in dev staging prod; do
  echo "Setting secrets for $env environment..."
  gh secret set AZURE_CREDENTIALS --env $env --body "$AZURE_CREDENTIALS"
  gh secret set PVICO_ENTRA_CREDENTIALS --env $env --body "$PVICO_ENTRA_CREDENTIALS"
  # Optional secrets can be set per environment or omitted
  [ -n "$PVICO_AZUREMAPS_KEY" ] && gh secret set PVICO_AZUREMAPS_KEY --env $env --body "$PVICO_AZUREMAPS_KEY"
done

# Step 4: After testing, optionally remove repository-level secrets
# gh secret delete PVICO_ENTRA_CREDENTIALS
```

### Pattern 2: Variable Precedence

Order of precedence (highest to lowest):
1. Environment secrets
2. Environment variables
3. Repository secrets
4. Repository variables
5. Workflow defaults

### Pattern 3: Gradual Migration

```mermaid
graph LR
    A[Repository Variables] -->|Phase 1| B[Create Environments]
    B -->|Phase 2| C[Duplicate to Env]
    C -->|Phase 3| D[Test with Dev]
    D -->|Phase 4| E[Move Prod]
    E -->|Phase 5| F[Remove Repository Vars]
```

---

## Configuration Mapping

### Variables That Don't Change

These work identically in all deployment models:
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_RESOURCE_GROUP`
- `AZURE_LOCATION`
- `DEPLOYMENT_MODEL`
- `MEMORY_BACKEND`
- `PVICO_ENTRA_CREDENTIALS` (Required)

### Variables That Are Now Optional

Can be configured post-deployment via UI:
- `PVICO_AZUREMAPS_KEY` - Configure at `/admin/configuration?section=secrets`
- `PVICO_OPENAI_CONNECTIONSTRING` - Configure at `/admin/configuration?section=secrets`

### New Variables for Kubernetes

Required for AKS deployment:
- `AKS_CLUSTER_NAME`
- `AKS_RESOURCE_GROUP`
- `AKS_NAMESPACE`
- `HELM_RELEASE`

### Deprecated Variables

No longer needed with Kubernetes:
- `AZURE_CAE_*` - Container Apps specific
- `AZURE_ENV_NAME` - Replaced by Kubernetes namespace

---

## Validation Checklist

### Pre-Migration
- [ ] Document all current variables and secrets
- [ ] Back up current configuration
- [ ] Test build locally with new AppHost
- [ ] Provision AKS cluster
- [ ] Verify network connectivity (for private deployments)

### During Migration
- [ ] Create GitHub Environments
- [ ] Bootstrap environment configuration
- [ ] Run parallel deployments
- [ ] Test all endpoints
- [ ] Verify authentication works

### Post-Migration
- [ ] Update DNS records
- [ ] Configure monitoring
- [ ] Remove old Container Apps resources
- [ ] Update documentation
- [ ] Clean up deprecated variables

---

## Rollback Procedures

### Quick Rollback
If issues occur, revert to previous workflow:

**Using Git and GitHub CLI:**
```bash
# Revert workflow file
git revert <commit-hash>
git push

# Trigger old deployment with GitHub CLI
gh workflow run deploy.yml

# Or trigger with specific inputs
gh workflow run deploy.yml \
  --ref main \
  -f environment=production

# Monitor the workflow run
gh run watch

# List recent workflow runs to verify
gh run list --workflow=deploy.yml
```

**Emergency Rollback (skip Git):**
```bash
# Download previous workflow version directly
gh api repos/:owner/:repo/contents/.github/workflows/deploy.yml \
  --jq .content \
  --header "Accept: application/vnd.github.v3.raw" \
  > deploy.yml.backup

# Restore it
cp deploy.yml.backup .github/workflows/deploy.yml
git add .github/workflows/deploy.yml
git commit -m "Emergency rollback to previous deployment workflow"
git push

# Trigger immediately
gh workflow run deploy.yml --ref main
```

### Data Rollback
For database/storage rollback:
1. Stop new deployment
2. Restore from backup
3. Redeploy old version
4. Verify data integrity

---

## Troubleshooting

### Common Issues

**Issue:** "Environment not found"
**Solution:** Ensure environment exists in Settings → Environments

**Issue:** "Variable not found"
**Solution:** Check variable is set at correct level (repo vs environment)

**Issue:** "AKS deployment fails"
**Solution:** Verify cluster credentials and namespace exists

**Issue:** "Authentication fails after migration"
**Solution:** Ensure `PVICO_ENTRA_CREDENTIALS` is correctly set

### Getting Help

1. Check deployment logs in Actions tab
2. Review Azure resource deployment in Portal
3. Use `kubectl describe` for pod issues
4. Check application logs via Log Analytics

---

## Support Resources

- **Documentation:** `DEPLOYMENT.GitHub.md` for detailed setup
- **ADO Guide:** `DEPLOYMENT.ADO.md` for Azure DevOps
- **Scripts:** `build/scripts/` for automation tools
- **Examples:** `build/environment-variables-*-sample.yml`

---

## Summary

Migration paths available:
1. **Repository → Environments** (Recommended for GitHub users)
2. **Container Apps → Kubernetes** (Infrastructure modernization)
3. **GitHub → Azure DevOps** (Platform change)

Key benefits after migration:
- ✅ Environment-specific configuration
- ✅ Better secret management
- ✅ Deployment approval workflows
- ✅ Modern Kubernetes infrastructure
- ✅ Runtime configuration UI
- ✅ Simplified deployment process