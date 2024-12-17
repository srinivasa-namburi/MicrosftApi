## Project Overview

This repo contains the Generative AI for Nuclear Licencing Copilot solution accellerator, intended to reduce the time and cost of creating licencing application documents, and is intended to be built upon and customized to meet users' needs. Any output should always be reviewed by a human.

## Solution Overview

![V2SolutionArchitectureAndDataflow](./docs/assets/V2SolutionArchitectureAndDataflow.png)

## License

Please see [LICENSE](https://github.com/Azure/AI-For-SMRs/blob/main/LICENSE) for this solution's License terms.

## Getting Started with Generative AI

Build a Good Understanding of Generative AI with these courses:

- [Welcoming the generative AI era with Microsoft Azure](https://azure.microsoft.com/en-us/blog/welcoming-the-generative-ai-era-with-microsoft-azure/)
- [Introduction to Generative AI - Training | Microsoft Learn](https://learn.microsoft.com/en-us/training/paths/introduction-generative-ai/)
- [Generative AI with Azure OpenAI Service (DALL-E Overview) - C# Corner](https://www.c-sharpcorner.com/article/generative-ai-with-azure-openai-dall-e-overview/)
- [Develop Generative AI solutions with Azure OpenAI Service Tutorial](https://learn.microsoft.com/en-us/training/paths/develop-ai-solutions-azure-openai/)

## Local Machine Setup

### Prerequisites

Install the following:

- [Docker Desktop](https://docs.docker.com/)
- [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/)

Create the following:

- Service principal by following step 1 from the [Deployment](./DEPLOYMENT.md) doc
  - Take note of the Tenant ID, Client ID, Scopes and Client Secret output by this step. They will be used in
    subsequent steps.
- Azure OpenAI and Azure Map services by following step 3 from the [Deployment](./DEPLOYMENT.md) doc

### Running the Project

- In Visual Studio, open `src\Microsoft.Greenlight.sln`
- Ensure Docker Desktop Engine is running
- In `src\Microsoft.Greenlight.AppHost`, create an `appsettings.Development.json` file.
  - Add the following to your `appsettings.Development.json`. Be sure to replace values that are specific to your
    resources that were created in the prerequisite section. This file is by default not source-controlled so secrets
    stay on your local machine.

      ```json
      "Azure": {
        "CredentialSource": "AzureCli"
      },
      "AzureAd": {
        "TenantId": "TENANT ID OF SVC PRIN FROM PREREQ",
        "ClientId": "CLIENT ID OF SVC PRIN FROM PREREQ",
        "Scopes": "SCOPE OF SVC PRIN FROM PREREQ",
        "ClientSecret": "CLIENT SECRET OF SVC PRIN FROM PREREQ",
        "Instance": "https://login.microsoftonline.com/"
      },
      "ConnectionStrings": {
        "openai-planner": "Endpoint=AZURE OPENAI ENDPOINT FROM PREREQ;Key=AZURE OPENAI KEY FROM PREREQ"
      },
      "ServiceConfiguration": {
        "AzureMaps": {
          "Key": "AZURE MAPS KEY"
        }
      }
      ```

- In Visual Studio, right-click on `Connected Services` in the `Microsoft.Greenlight.AppHost` project. Select
  `Azure Resource Provisioning Settings`. In the subsequent dialog box, select the Subscription, Location, and Resource
  Group that you would like to use for Azure deployments.
  - Note: This project uses [Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) to deploy Azure resources
    required by the project.
- In the Developer Powershell within Visual Studio, execute az login:
  `az login --tenant "<TENANT ID TO DEPLOY RESOURCES>"`.
- In Visual Studio, right-click on `Microsoft.Greenlight.AppHost` project. Select `Manage User Secrets` which will open
  `secrets.json`.
  - Set the SQL Password and Azure Tenant ID in `secrets.json` using the following syntax.

    ```json
    "Azure:TenantId": "TENANT ID TO DEPLOY RESOURCES",
    "Parameters:sqlPassword": "SOME PASSWORD"
    ```

- Start debugging
- Hit yes to trust the local certificates (you will only need to do this the first time)
- When the projects launch, the Aspire portal and frontend application should open in separate browser windows.

## Exploring Deployment, Customizing and Debugging the Solution

See [ExploringCustomizingAndDebuggingSolution.md](./ExploringCustomizingAndDebuggingSolution.md)

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft
trademarks or logos is subject to and must follow
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
