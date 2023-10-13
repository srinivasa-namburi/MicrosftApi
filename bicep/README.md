# Project Vico Infrastructure

## Overview
![Project Vico Infrastructure diagram](documentation/project_vico_infrastructure_public.png)

__Note:__ What is greyed out is not yet deployed and/or correctly implemented.

## Prerequisites

## Deployment options

### Local deployment with user account and deployment stacks __(Preview)__
```bash
az login
az stack sub create --name 'vico-stack' --location '<region>' --template-file 'main.bicep' --deployment-resource-group '<resource_group_name>' --deny-settings-mode 'none' --parameters main.bicepparam
```
### Deploy applications with VS
Follow instructions here after opening the relevant .sln project files: https://learn.microsoft.com/en-us/dotnet/core/deploying/deploy-with-vs?tabs=vs156.

### Github Actions
#### Deploy infrastructure and applications
Deployment process:
- Create a service principal with owner role on target resource group which is contained in a openai GPT3.5 whitelisted subscription.
- Create Github secrets with the service principal credentials (AZURE_CREDENTIALS), the subscription ID (AZURE_SUBSCRIPTION_ID), and the resource group name (AZURE_RESOURCE_GROUP).
- Run deploy-all github action.