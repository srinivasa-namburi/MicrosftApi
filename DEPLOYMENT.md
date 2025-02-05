## Solution Deployment

# Pre-Requisites

1. Fork the Repository, then download the fork you've created of the GenAI for Industry Permitting repository with git, and run this command from a PowerShell prompt (if you're on Windows, open Powershell through Terminal or the Windows Powershell application, if you're on Linux/Mac, precede the follow script name with "pwsh"). Make sure your active working directory is wherever you downloaded the repository to. This script creates the Service Principle used by the applicaion for it's ongoing operation. It acts as the Application client against Microsoft Entra to run authentication. Take note of the outputted secret details at the end of the script, this will be required for the PVICO_ENTRA_CREDENTIALS secret later on.

   ```
   .\build\scripts\sp-create.ps1
   ```

   - ![ForkGithubRepository](./docs/assets/ForkRepository.png)

2. Create a Service Principal for GitHub Actions to utilize for deployment. Either (1) Cloud Application Administrator or (2) Application Developer permission on the tenant (Microsoft Entra) is required to perform this step. Application Developer is normally sufficient - and both Cloud Application Administrator and Application Developer supersede it. To do this, run this command (you must have the Azure CLI installed and be logged on to the tenant using "az login" first - you can download the Azure CLI here if you don't have it : https://learn.microsoft.com/en-us/cli/azure/install-azure-cli). The output of this script should be noted for storage in the AZURE_CREDENTIALS secret in a later step. Substitute <subscriptionId> with your actual subscription id:

To create the service principal with owner permissions on the subscription(which allows the solution to create resource groups), run the following command:

```
  az ad sp create-for-rbac
          --name "sp-ms-industrypermitting-deploy"
          --scopes "/subscriptions/<subscriptionId>"
          --role owner
```

If you have a pre existing resource group you wish to use, you can create the service principal
with owner permissions on the resource group instead of the subscription. To do this, run the following command:

```
  az ad sp create-for-rbac
          --name "sp-ms-industrypermitting-deploy"
          --scopes "/subscriptions/<subscriptionId>/resourceGroups/<resourceGroupName>"
          --role owner
```

Please note that this requires the Resource Group to be created manually before running the deployment script.

If you wish to grant access to a an additional, different resource group for the already created service principal, you can run the following command, where <appId> is the application id of the service principal created above:

```
  az role assignment create
          --role "Owner"
          --assignee <appId>
          --scope /subscriptions/<subscriptionId>/resourceGroups/<resourceGroupName>

```

3. Create the following pre-req resources in Azure Portal. These can be in any Resource Group:

   1. Azure OpenAI service
      - Create a deployment for EITHER GPT-4o (highly recommended) or GPT-4 v 1106-preview.
        - For GPT-4o - it needs to be called "gpt-4o"
        - For GPT-4-128K/Turbo - it needs to be called "gpt-4-128k"
        - We recommend a 150,000 tokens per minute (TPM) limit as a minimum.
      - Create a deploymend for text-embedding-ada-002.
        - Call it "text-embedding-ada-002" and select v2 (should be the default).
        - We recommend a 300,000 tokens per minute (TPM) limit as a minimum
   2. Azure Maps

   - ![ConfigureAzureResources](./docs/assets/ConfigureAzureResources.png)

4. If using private networking:

   - Create or select a VNET in the Azure Portal in the same subscription of the deployment
   - Create or select a Subnet in the VNET created above
   - Create a Subnet for Container Apps Environment for GenAI for Industry Permitting
     - The Subnet for Container Apps Environment must use Subnet Delegation - delegate to Microsoft.App.Environments.
   - Take note of the Subnet ID for both of these subnets, you will these to fill in the the AZURE_SUBNET_CAE and AZURE_SUBNET_PE variables.

5. Add the following deployment variables to the Secrets and Variables section of the repository:

   - Secrets:
     - AZURE_CREDENTIALS : { The output of the creation command for the deployment service principal in Step 2 above }
     - PVICO_AZUREMAPS_KEY : { The Key to the Azure Maps instance you created in Step 3 }
     - PVICO_ENTRA_CREDENTIALS : { This is the output of the .\sp-create.ps1 script run above }
     - PVICO_OPENAI_CONNECTIONSTRING : { Use the following format : Endpoint=\<endpoint of Azure Openai Instance\>;Key=\<key of Azure OpenAI instance\> }
   - Variables:
     - AZURE_CLOUD: {AzureUSGovernment | AzureCloud}
     - AZURE_RESOURCE_GROUP: {Whatever you’d like the Resource Group to be named.}
     - AZURE_LOCATION : {usgovvirginia | swedencentral}
     - AZURE_SUBNET_CAE : {This is the Subnet ID of the Container Apps Environment subnet created above - find it in the portal}
     - AZURE_SUBNET_PE : {This is the Subnet ID of the Container Apps Environment subnet created above - find it in the portal}
     - AZURE_SUBSCRIPTION_ID: {Your Subscription ID}
     - DEPLOYMENT_MODEL: {private | public}
     - PVICO_OPENAI_RESOURCEGROUP : {The name of the Resource Group where your Azure OpenAI instance has been deployed}
     - <img width="394" alt="SetGithubSecrets" src="./docs/assets/GithubSecrets.png">

Note that if you previously had an AZURE_ENV variable, this has been replaced by the
AZURE_RESOURCE_GROUP variable. To maintain backwards compatibility, you can keep using the
AZURE_ENV variable if its present in your environment with the same behavior - in that case,
a resource group is expected to be present with the name "rg-<AZURE_ENV>" in your subscription already.

6. If you are running the "load-trainingdata" process to bring in the sample training data to the solution, add the following deployment variables to the Secrets and Variables section of the repository. These will only be available after the solution is fully deployed.
   - Secrets:
     - PVICO_AISEARCH_KEY
     - PVICO_TRAININGDATA_DOWNLOAD_TOKEN : { Request MS assistance in getting this token for your deployment. }
   - Variables:
     - PVICO_AISEARCH_HOST
     - PVICO_INDEXES_TO_POPULATE
