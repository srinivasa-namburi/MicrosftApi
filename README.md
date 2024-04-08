## Project Overview

This repo contains the Generative AI for Nuclear Licencing Copilot solution accellerator, intended to reduce the time and cost of creating licencing application documents, and is intended to be built upon and customized to meet users' needs. Any output should always be reviewed by a human.

## Solution Overview
![V2SolutionArchitectureAndDataflow](https://github.com/Azure/AI-For-SMRs/assets/18575224/04161656-996f-47cc-b1fc-ecd261e0f148)

## License

Please see [LICENSE](https://github.com/Azure/AI-For-SMRs/blob/main/LICENSE) for this solution's License terms.

## Getting Started with Generative AI

Build a Good Understanding of Generative AI with these courses:

- [Welcoming the generative AI era with Microsoft Azure ](https://azure.microsoft.com/en-us/blog/welcoming-the-generative-ai-era-with-microsoft-azure/)
- [Introduction to Generative AI - Training | Microsoft Learn](https://learn.microsoft.com/en-us/training/paths/introduction-generative-ai/)
- [Generative AI with Azure OpenAI Service (DALL-E Overview) - C# Corner](https://www.c-sharpcorner.com/article/generative-ai-with-azure-openai-dall-e-overview/)
- [Develop Generative AI solutions with Azure OpenAI Service Tutorial](https://learn.microsoft.com/en-us/training/paths/develop-ai-solutions-azure-openai/)

## Running the Word Copilot Solution

The Word Copilot connects to a WebAPI backend which is interfacing with the Semantic Kernal. This can either be running on your local machine, or it can be running in Azure. If you are running it from your own machine, see steps below in section

- Install NodeJS on your local machine
- Clone the repository
- Deploy the backend of the solution with the steps outlined in [bicep/README.md](https://github.com/Azure/AI-For-SMRs/blob/main/bicep/README.md)
- Open Visual Studio Code as Administrator
- Open the solution in Visual Studio Code
- Copy and paste the .env.sample file and rename it to .env
- Add a line to this file with the URL of the solution backend:
  - REACT_APP_BACKEND_URI={InsertURLHere}
- In the terminal CD to ..\SC_AI-for_SMRs\src\Frontend\Word_Plugin
- Run npm install
- To Run with NPM, Run "npm start"
- To Run with Visual Studio Code debugger: Open the debugging pane, hit the dropdown to select the debugging environmnent and select "Word Desktop (Edge Chromium)". Now Hit the debugging start green arrow to launch the Word Copilot
- When the project is launching, hit yes to trust certificates and to allow LocalHost loopback for Microsoft Edge Webview:
  - ![image](https://github.com/Azure/AI-For-SMRs/assets/18575224/aeaaafe7-fb49-4266-b375-01b75451982d)
- Hit okay as Word opens:
  - ![image](https://github.com/Azure/AI-For-SMRs/assets/18575224/1f03b1a0-95f5-448d-9f05-855fd5ef4858)

## Running the WebAPI on your local machine

- Open Visual Studio 2022 and open ProjectVICO.sln
- Open src/Frontend/WebApi/appsettings.local.json
  - Create an appsettings.local.json file by copying appsetings.json. This file is by default not source-controlled so secrets stay on your local machine
  - You need at least an Azure OpenAI Service instance already provisioned, as well as the key and endpoint address. You also need to deploy a gpt-4-32k model (v0613) as well as a text-embedding-ada002 (v2) model. Update the appsettings.local.json file with the key and endpoint address of your Azure OpenAI Service instance, as well as the model names.
  - You also need an Azure Cognitive Search instance provisioned. Similarly to the Azure OpenAI Service instance, you need to update the appsettings.local.json file with the key and endpoint address of your Azure Cognitive Search instance.
  - Run the script bicep/scripts/azure-search-index-restore.ps1 to populate the Cognitive Search instance with an index containing our pre-trained knowledge base for nuclear licensing.
- Open src/Plugins/ProjectVico.Plugins.DocQnA/local.settings.json
  - As above, this file does not exist, but must be created by copying from appsettings.json
  - Similarly as above for the API, this file needs to be configured with connections to Azure OpenAI Service as well as Azure Cognitive Search.
- Hit Solution > Properties > StartUp projects and set the FrontEnd.API and Plugins.DocQnA as the startup projects:
- ![image](https://github.com/Azure/AI-For-SMRs/assets/18575224/d35c1ca6-35ca-48ee-9ef3-6e00944bbda7)
- Start with or without debugging
- Hit yes to trust the local certificates (you will only need to do this the first time)
- When the projects launch, they should report their URL, which is the URL to use in the .env of the Word Colpilot project to point to this WebAPI

## Exploring Deployment, Customizing and Debugging the Solution
See [ExploringCustomizingAndDebuggingSolution.md](https://github.com/Azure/AI-For-SMRs/blob/main/ExploringCustomizingAndDebuggingSolution.md)

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft
trademarks or logos is subject to and must follow
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
