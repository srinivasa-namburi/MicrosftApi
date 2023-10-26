# Project Vico Infrastructure

## Overview

![Project Vico Infrastructure diagram](documentation/project_vico_infrastructure_public.png)

**Note:** What is greyed out is not yet deployed and/or correctly implemented.

## Prerequisites

## Deployment options

### Local deployment with user account and deployment stacks **(Preview)**

```bash
az login
az stack sub create --name 'vico-stack' --location '<region>' --template-file 'main.bicep' --deployment-resource-group '<resource_group_name>' --deny-settings-mode 'none' --parameters main.bicepparam
```

### Deploy applications with VS

Follow instructions here after opening the relevant .sln project files: https://learn.microsoft.com/en-us/dotnet/core/deploying/deploy-with-vs?tabs=vs156.

### Github Actions

#### Deploy infrastructure and applications

Deployment process:

- Precreate the designated Azure Resource Group. Resource Group must be contained in an Azure subscription which has been whitelisted for Azure OpenAI Service.
- Create a service principal (App Registration)
- Create Github secrets with the service principal credentials (AZURE_CREDENTIALS), the subscription ID (AZURE_SUBSCRIPTION_ID), and the resource group name (AZURE_RESOURCE_GROUP).
  - For AZURE_CREDENTIALS, use the following command to generate the JSON string:
  ```bash
  az ad sp create-for-rbac
          --name "<service principal name>"
          --scopes "/subscriptions/<subscriptionId/resourceGroups/<resource group name>"
          --role owner
  ```
- Run deploy-all github action.
