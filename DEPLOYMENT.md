## Solution Deployment

# Pre-Requisites
- Create a Service Principle, with owner permissions on the tenant, which will be used to deploy solution (can be deleted right after deployment)
  - ![image](https://github.com/user-attachments/assets/e95ce5ad-36cc-4be9-a2ab-644c23b4bf47)
- Create an App Registration
  - ![image](https://github.com/user-attachments/assets/5599dd35-025d-40be-a8b3-c88399aff74d)
- Fork the Repository
  -  ![image](https://github.com/user-attachments/assets/556c6ac0-7354-447b-8c91-1a15469aee8f)
- Add Azure credential with Owner access, you can create a Service Principle for the deployment and delete it right after
  -  ![image](https://github.com/user-attachments/assets/1bdd6805-791e-4db7-a2bc-82f8a878ecf4)
- Create pre-req resources in Azure Portal: Azure OpenAI service, VNET, Azure Maps
  - <img width="349" alt="image" src="https://github.com/user-attachments/assets/e7b7ba2d-d56d-4c81-b5b3-6c50e37d538f">
- Add the following deployment variables to the Secrets and Variables section of the repository:   
  - AZURE_ENV_NAME: {Whatever you’d like the Resource Group to be named}
  - AZURE_SUBSCRIPTION_ID: {Your Subscription ID}
  - AZURE_CLOUD: {AzureUSGovernment | } 
  - AZURE_LOCATION : {usgovvirginia | swedencentral}
  - DEPLOYMENT_MODEL: {private | public}
  - AZURE_SUBNET_CAE: 
  - AZURE_SUBNET_PE
    - <img width="394" alt="image" src="https://github.com/user-attachments/assets/8d473af4-1551-49da-9a5a-9e75bbd4ab5f">

  





 

