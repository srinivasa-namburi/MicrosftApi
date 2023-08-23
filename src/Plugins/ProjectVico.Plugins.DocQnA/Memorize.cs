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

public class Memorize
{
    private readonly ILogger _logger;

    public Memorize(ILoggerFactory loggerFactory)
    {
        //_logger = loggerFactory.CreateLogger<DemoHttpTrigger>();
    }

    [Function("Memorize")]
    [OpenApiOperation(operationId: "Memorize", tags: new[] { "ExecuteFunction" }, Description = "DO NOT USE THIS FUNCTION")]
    [OpenApiParameter(name: "docUri", Description = "The URI of the document where the section comes from", Required = true, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "sectionName", Description = "The name of the section to memorize", Required = true, In = ParameterLocation.Query)]
    [OpenApiRequestBody(contentType: "text/plain", bodyType: typeof(string), Required = true, Description = "The section to memorize")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns the sum of the two numbers.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        //_logger.LogInformation("C# HTTP trigger function processed a request.");

        // Get the input params
        string docUri = req.Query.GetValues("docUri").First();
        string sectionName = req.Query.GetValues("sectionName").First();

        StreamReader reader = new StreamReader(req.Body);
        string sectionContent = reader.ReadToEnd();

        // Create connection to the semantic memory
        var AzureCognitiveSearch = new AzureCognitiveSearchMemoryStore(
            "https://smrlicenseacs.search.windows.net",
            System.Environment.GetEnvironmentVariable("AzureCognitiveSearchApiKey", EnvironmentVariableTarget.Process)!
        );

        SemanticTextMemory semanticMemory = new SemanticTextMemory(
            AzureCognitiveSearch,
            new AzureTextEmbeddingGeneration(
                "smrlicenseembeddingada002",
                "https://smrlicencesoldev.openai.azure.com/",
                System.Environment.GetEnvironmentVariable("AzureOpenAIApiKey", EnvironmentVariableTarget.Process)!
            )
        );

        // Create kernel
        IKernel kernel = new KernelBuilder()
            .WithAzureChatCompletionService(
                "smrlicencegpt35",
                "https://smrlicencesoldev.openai.azure.com/",
                System.Environment.GetEnvironmentVariable("AzureOpenAIApiKey", EnvironmentVariableTarget.Process)!
            )
            .Build();

        // Create semantic function that comes up with alternative section names
        string prompt = "Provide a list of 2 alternative section names for the section '{{$sectionName}}'. Use newlines to separate each alternative name. Do not use bullets, dashes, or numbers for the list.\n\n[EXAMPLE]\nSection A\nSection B\nSection C\n\n[SECTION]\n{{$input}}\n\n[ALTERNATIVES]\n";
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
            ["input"] = sectionContent,
            ["sectionName"] = sectionName
        };
        var results = (await altSectionNames.InvokeAsync(input)).Result;

        // Extract the section names from the results
        var sectionNames = (results.Split(new string[] {"\n"}, StringSplitOptions.None));
        sectionNames.Append(sectionName);

        // Save the section content and the section names to the semantic memory
        foreach (var name in sectionNames)
        {
            await semanticMemory.SaveReferenceAsync(
                collection: "section-embeddings",
                externalSourceName: "Index of sections in documents",
                externalId: docUri + "–" + sectionName + "-" + name,
                description: sectionContent,
                text: name);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        return response;
    }
}
