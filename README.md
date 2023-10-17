## Project Overview
This repo contains the Generative AI for Nuclear Licencing Copilot solution accellerator, intended to reduce the time and cost of creating licencing application documents, and is intended to be built upon and customized to meet users' needs. Any output should always be reviewed by a human. 

## Solution Overview
![image](https://github.com/Azure/AI-For-SMRs/assets/18575224/20952d5b-d3bb-4ff6-9327-ace9ad54a12f)


## License
Please see [LICENSE](https://github.com/Azure/AI-For-SMRs/blob/main/LICENSE) for this solution's License terms. 

## Getting Started with Generative AI
Build a Good Understanding of Generative AI with these courses: 
* [Welcoming the generative AI era with Microsoft Azure ](https://azure.microsoft.com/en-us/blog/welcoming-the-generative-ai-era-with-microsoft-azure/)
* [Introduction to Generative AI - Training | Microsoft Learn](https://learn.microsoft.com/en-us/training/paths/introduction-generative-ai/)
* [Generative AI with Azure OpenAI Service (DALL-E Overview) - C# Corner](https://www.c-sharpcorner.com/article/generative-ai-with-azure-openai-dall-e-overview/)
* [Develop Generative AI solutions with Azure OpenAI Service Tutorial](https://learn.microsoft.com/en-us/training/paths/develop-ai-solutions-azure-openai/)

## Running the Solution
* Close the repository
* Deploy the backend of the solution with the steps outlined in [bicep/README.md](https://github.com/Azure/AI-For-SMRs/blob/main/bicep/README.md)
* Open the solution in Visual Studio Code as Administrator
* Copy and paste the .env.sample file and rename it to .env
* Add a line to this file with the URL of the solution backend:
  * REACT_APP_BACKEND_URI={InsertURLHere}
* CD to ..\SC_AI-for_SMRs\src\Frontend\Word_Plugin
* Run npm install
* Run npm start

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
