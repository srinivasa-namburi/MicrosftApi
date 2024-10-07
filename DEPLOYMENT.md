## Solution Deployment

# Pre-Requisites

1. Fork the Repository, then download the fork you've created of the GenAI for Industry Permitting repository with git, and run this command from a PowerShell prompt (if you're on Windows, open Powershell through Terminal or the Windows Powershell application, if you're on Linux/Mac, precede the follow script name with "pwsh"). Make sure your active working directory is wherever you downloaded the repository to. This script creates the Service Principle used by the applicaion for it's ongoing operation. It acts as the Application client against Microsoft Entra to run authentication.

   ```
   .\build\scripts\sp-create.ps1
   ```

   - ![image](https://github.com/user-attachments/assets/556c6ac0-7354-447b-8c91-1a15469aee8f)

2. Create a Service Principal for GitHub Actions to utilize for deployment. Either (1) Cloud Application Administrator or (2) Application Developer permission on the tenant (Microsoft Entra) is required to perform this step. Application Developer is normally sufficient - and both Cloud Application Administrator and Application Developer supersede it. To do this, run this command (you must have the Azure CLI installed and be logged on to the tenant using "az login" first - you can download the Azure CLI here if you don't have it : https://learn.microsoft.com/en-us/cli/azure/install-azure-cli). Substitute <subscriptionId> with your actual subscription id:

```
  az ad sp create-for-rbac
          --name "sp-ms-industrypermitting-deploy"
          --scopes "/subscriptions/<subscriptionId>"
          --role owner
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

   - ![image](https://github.com/user-attachments/assets/36341022-c7ed-4411-886b-554a9593b453)

4. If using private networking:

   - Create or select a VNET in the Azure Portal in the same subscription of the deployment
   - Create or select a VNET for services
   - Create a VNET for Container Apps Environment for GenAI for Industry Permitting
     - This must use Subnet Delegation - delegate to Microsoft.App.Environments. Take note of the Subnet ID for both of these subnets, it is needed for later

5. Add the following deployment variables to the Secrets and Variables section of the repository:

   - Secrets:
     - AZURE_CREDENTIALS : { The output of the creation command for the deployment service principal in Step 2 above }
     - PVICO_AZUREMAPS_KEY : { The Key to the Azure Maps instance you created in Step 3 }
     - PVICO_ENTRA_CREDENTIALS : { This is the output of the .\sp-create.ps1 script run above }
     - PVICO_OPENAI_CONNECTIONSTRING : { Use the following format : Endpoint=\<endpoint of Azure Openai Instance\>;Key=\<key of Azure OpenAI instance\> }
   - Variables:
     - AZURE_CLOUD: {AzureUSGovernment | AzureCloud}
     - AZURE_ENV_NAME: {Whatever you’d like the Resource Group to be named. It will be prefixed with "rg-" automatically.}
     - AZURE_LOCATION : {usgovvirginia | swedencentral}
     - AZURE_SUBNET_CAE : {This is the Subnet ID of the Container Apps Environment subnet created above - find it in the portal}
     - AZURE_SUBNET_PE : {This is the Subnet ID of the Container Apps Environment subnet created above - find it in the portal}
     - AZURE_SUBSCRIPTION_ID: {Your Subscription ID}
     - DEPLOYMENT_MODEL: {private | public}
     - PVICO_OPENAI_RESOURCEGROUP : {The name of the Resource Group where your Azure OpenAI instance has been deployed}
     - <img width="394" alt="image" src="https://github.com/user-attachments/assets/8d473af4-1551-49da-9a5a-9e75bbd4ab5f">

6. If you are running the "load-trainingdata" process to bring in the sample training data to the solution, add the following deployment variables to the Secrets and Variables section of the repository. These will only be available after the solution is fully deployed.
   - Secrets:
     - PVICO_AISEARCH_KEY
     - PVICO_TRAININGDATA_DOWNLOAD_TOKEN : { Request MS assistance in getting this token for your deployment. }
   - Variables:
     - PVICO_AISEARCH_HOST
     - PVICO_INDEXES_TO_POPULATE
