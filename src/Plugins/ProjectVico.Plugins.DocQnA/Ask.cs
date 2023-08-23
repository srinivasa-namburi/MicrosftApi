using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextEmbedding;
using Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SemanticFunctions;

namespace ProjectVico.Plugins.Sample.FunctionApp;

public class Ask
{
    private readonly ILogger _logger;

    public Ask(ILoggerFactory loggerFactory)
    {
        //_logger = loggerFactory.CreateLogger<DemoHttpTrigger>();
    }

    [Function("GetDescriptionOfSection")]
    [OpenApiOperation(operationId: "GetDescriptionOfSection", tags: new[] { "ExecuteFunction" }, Description = "Get a description of a section in the application, include required data.")]
    [OpenApiParameter(name: "sectionName", Description = "The name of the section", Required = true, In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns a description of the format of a section.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        //_logger.LogInformation("C# HTTP trigger function processed a request.");

        string sectionName = req.Query.GetValues("sectionName").First();

        SemanticTextMemory semanticMemory = new SemanticTextMemory(
            new AzureCognitiveSearchMemoryStore(
                "https://smrlicenseacs.search.windows.net",
                System.Environment.GetEnvironmentVariable("AzureCognitiveSearchApiKey", EnvironmentVariableTarget.Process)!
            ),
            new AzureTextEmbeddingGeneration(
                "smrlicenseembeddingada002",
                "https://smrlicencesoldev.openai.azure.com/",
                System.Environment.GetEnvironmentVariable("AzureOpenAIApiKey", EnvironmentVariableTarget.Process)!
            )
        );


        IAsyncEnumerable<MemoryQueryResult> memories = semanticMemory.SearchAsync("section-embeddings", sectionName, limit: 12);

        var sections = new Dictionary<string, string>();

        // Create a response to the user
        var textResponse = new StringBuilder();
        //textResponse.AppendLine("I found the following information:");

        await foreach (var memory in memories)
        {
            // textResponse.AppendLine("    Key: " + memory.Metadata.Id);
            // textResponse.AppendLine("    Relevance: " + memory.Relevance);
            // textResponse.AppendLine();

            var sectionParts = memory.Metadata.Id.Split('-');
            var sectionKey = sectionParts[0];// + "-" + sectionParts[1];

            // check if the section already exists
            if (sections.ContainsKey(sectionKey))
            {
                continue;
            }
            sections.Add(sectionKey, memory.Metadata.Description);
        }


        // Build the examples for the prompt
        var sectionExample = new StringBuilder();
        foreach (var section in sections)
        {
            sectionExample.AppendLine($"[EXAMPLE: {section.Key}]");
            sectionExample.AppendLine(section.Value);
            sectionExample.AppendLine();
        }

        // Create kernel
        IKernel kernel = new KernelBuilder()
            .WithAzureChatCompletionService(
                "smrlicencegpt35",
                "https://smrlicencesoldev.openai.azure.com/",
                System.Environment.GetEnvironmentVariable("AzureOpenAIApiKey", EnvironmentVariableTarget.Process)!
            )
            .Build();

        // Create semantic function that comes up with alternative section names
        string prompt = "Below, there are several {{$sectionName}} sections from previous applications. Describe similarities of the sections so that I can write a new section with a similar format.\nBe sure to also identify common types of data that are included in the sections.\nDo not describe the differences between the articles.\n\n{{$input}}\n\n[FORMAT AND REQUIRED DATA FOR SECTION]\n";
        var promptConfig = new PromptTemplateConfig
        {
            Completion =
            {
                MaxTokens = 2000,
                Temperature = 0.2,
                TopP = 0.5,
            }
        };
        var promptTemplate = new PromptTemplate(
            prompt,                          // Prompt template defined in natural language
            promptConfig,                    // Prompt configuration
            kernel                           // SK instance
        );
        var functionConfig = new SemanticFunctionConfig(promptConfig, promptTemplate);
        var altSectionNames = kernel.RegisterSemanticFunction("AdHocPlugin", "AltSectionNames", functionConfig);

        // Run the function with the input
        var input = new ContextVariables()
        {
            ["input"] = sectionExample.ToString(),
            ["sectionName"] = sectionName
        };
        var results = (await altSectionNames.InvokeAsync(input)).Result;
        textResponse.Append(results);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        response.WriteString(textResponse.ToString());

        return response;
    }
}
