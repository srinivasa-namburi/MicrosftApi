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
    [OpenApiOperation(operationId: "Memorize", tags: new[] { "ExecuteFunction" }, Description = "Memorize a section of a doc.")]
    [OpenApiParameter(name: "docUri", Description = "The URI of the document where the section comes from", Required = true, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "sectionName", Description = "The name of the section to memorize", Required = true, In = ParameterLocation.Query)]
    [OpenApiRequestBody(contentType: "text/plain", bodyType: typeof(string), Required = true, Description = "The section to memorize")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns the sum of the two numbers.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        //_logger.LogInformation("C# HTTP trigger function processed a request.");

        string docUri = req.Query.GetValues("docUri").First();
        string sectionName = req.Query.GetValues("sectionName").First();

        StreamReader reader = new StreamReader(req.Body);
        string sectionContent = reader.ReadToEnd();

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

        IKernel kernel = new KernelBuilder()
            .WithAzureChatCompletionService(
                "smrlicencegpt35",
                "https://smrlicencesoldev.openai.azure.com/",
                System.Environment.GetEnvironmentVariable("AzureOpenAIApiKey", EnvironmentVariableTarget.Process)!
            )
            .Build();

        string prompt = "Provide a list of 5 alternative section names for the section '{{$sectionName}}'. Use newlines to separate each alternative name. Do not use bullets, dashes, or numbers for the list.\n\n[EXAMPLE]\nSection A\nSection B\nSection C\n\n[SECTION]\n{{$input}}\n\n[ALTERNATIVES]\n";
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

        var input = new ContextVariables()
        {
            ["input"] = sectionContent,
            ["sectionName"] = sectionName
        };

        var results = (await altSectionNames.InvokeAsync(input)).Result;

        var sectionNames = (results.Split(new string[] {"\n"}, StringSplitOptions.None));
        sectionNames.Append(sectionName);

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
