## Solution Deployment

# Pre-Requisites
1. Fork the Repository, then download the fork you've created of the GenAI for Industry Permitting repository with git, and run this command from a PowerShell prompt (if you're on Windows, open Powershell through Terminal or the Windows Powershell application, if you're on Linux/Mac, precede the follow script name with "pwsh"). Make sure your active working directory is wherever you downloaded the repository to. This script creates the Service Principle used by the applicaion for it's ongoing operation. It acts as the Application client against Microsoft Entra to run authentication.
    ```
    .\build\scripts\sp-create.ps1
    ```
      -  ![image](https://github.com/user-attachments/assets/556c6ac0-7354-447b-8c91-1a15469aee8f)
    
2. Create another Service Principle, with Owner access on the Subscription being used for deployment resource group, which will be used to deploy solution (can be deleted right after deployment). This one either requires (1) Cloud Application Administrator or (2) Application Developer permission on the tenant (Microsoft Entra). Application Developer is normally sufficient - and both Cloud Application Administrator and Application Developer supersede it. To do this, run this command (you must have the Azure CLI installed and be logged on to the tenant using "az login" first - you can download the Azure CLI here if you don't have it : https://learn.microsoft.com/en-us/cli/azure/install-azure-cli). Substitute <subscriptionId> with your actual subscription id:
  ```
    az ad sp create-for-rbac
            --name "sp-ms-industrypermitting-deploy"
            --scopes "/subscriptions/<subscriptionId>"
            --role owner
 ```
3. Create the following pre-req resources in Azure Portal. These can be in any Resource Group:
    1. Azure OpenAI service
    2. Azure Maps
      - <img width="349" alt="image" src="https://github.com/user-attachments/assets/e7b7ba2d-d56d-4c81-b5b3-6c50e37d538f">

  4. If using private networking:
        - Create or select a VNET in the Azure Portal in the same subscription of the deployment
        - Create or select a VNET for services
        - Create a VNET for Container Apps Environment for GenAI for Industry Permitting
      - This must use Subnet Delegation - delegate to Microsoft.App.Environments. Take note of the Subnet ID for both of these subnets, it is needed for later
  
5. Add the following deployment variables to the Secrets and Variables section of the repository:   
    - Secrets:
        - AZURE_CREDENTIALS
        - PVICO_AZUREMAPS_KEY
        - PVICO_ENTRA_CREDENTIALS
        - PVICO_OPENAI_CONNECTIONSTRING 
    - Variables:
        - AZURE_CLOUD: {AzureUSGovernment | } 
        - AZURE_ENV_NAME: {Whatever you’d like the Resource Group to be named}
        - AZURE_LOCATION : {usgovvirginia | swedencentral}
        - AZURE_SUBNET_CAE
        - AZURE_SUBNET_PE
        - AZURE_SUBSCRIPTION_ID: {Your Subscription ID}
        - DEPLOYMENT_MODEL: {private | public}
      - <img width="394" alt="image" src="https://github.com/user-attachments/assets/8d473af4-1551-49da-9a5a-9e75bbd4ab5f">

6. If you are running the "load-trainingdata" process to bring in the sample training data to the solution, add the following deployment variables to the Secrets and Variables section of the repository:
    - Secrets:
        - PVICO_AISEARCH_KEY
        - PVICO_TRAININGDATA_DOWNLOAD_TOKEN
    - Variables:
        - PVICO_AISEARCH_HOST
        - PVICO_INDEXES_TO_POPULATE

  





 

