using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextEmbedding;
using Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Reliability;
using Microsoft.SemanticKernel.SemanticFunctions;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;

namespace ProjectVico.Plugins.DocQnA;

public class SectionPlugin
{
    private readonly IOptions<AiOptions> _aiOptions;
    private readonly ILogger _logger;

    public SectionPlugin(ILoggerFactory loggerFactory, IOptions<AiOptions> aiOptions)
    {
        this._aiOptions = aiOptions;
        //_logger = loggerFactory.CreateLogger<DemoHttpTrigger>();
    }

    // Commented out to avoid conflict with DocumentIntelligencePlugin which is under development and testing
    //[Function("GetOutputForSection")]
    //[OpenApiOperation(operationId: "GetOutputForSection", tags: new[] { "ExecuteFunction" }, Description = "Using your knowledge of similar written section from earlier environmental reports, write output for a specific section indicated by <sectionName>.")]
    //[OpenApiParameter(name: "sectionName", Description = "The name of the section", Required = true, In = ParameterLocation.Query)]
    //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns a output for a particular section indicated by <sectionName>")]
    //[OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> GetOutputForSectionAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        //_logger.LogInformation("C# HTTP trigger function processed a request.");

        string sectionName = req.Query.GetValues("sectionName").First();

        SemanticTextMemory semanticMemory = new SemanticTextMemory(
            new AzureCognitiveSearchMemoryStore(
                this._aiOptions.Value.CognitiveSearch.Endpoint,
                this._aiOptions.Value.CognitiveSearch.Key!
            ),
            new AzureTextEmbeddingGeneration(
                this._aiOptions.Value.OpenAI.EmbeddingModel,
                this._aiOptions.Value.OpenAI.Endpoint,
                this._aiOptions.Value.OpenAI.Key!
            )
        );


        IAsyncEnumerable<MemoryQueryResult> memories = semanticMemory.SearchAsync(this._aiOptions.Value.CognitiveSearch.Index, sectionName, limit: 12);

        var sections = new Dictionary<string, string>();

        // Create a response to the user
        var textResponse = new StringBuilder();

        await foreach (var memory in memories)
        {
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
                this._aiOptions.Value.OpenAI.CompletionModel,
                this._aiOptions.Value.OpenAI.Endpoint,
                this._aiOptions.Value.OpenAI.Key!
            )
            .Configure(config =>
            {
                
                config.SetDefaultHttpRetryConfig(new HttpRetryConfig()
                {
                    MaxTotalRetryTime = TimeSpan.FromSeconds(500),
                });
            })
            .Build();

        // Create semantic function that comes up with alternative section names
        const string Prompt = "Below, there are several {{$sectionName}} sections from previous applications. Using this information, write a similar section. Be as verbose as necessary to include all required information. If you are missing details to write specific portions, please indicate that with [] characters surrounding the needed data type.\n\n{{$input}}\n\n[FORMAT AND REQUIRED DATA FOR SECTION]\n";
        var promptConfig = new PromptTemplateConfig
        {
            Completion =
            {
                MaxTokens = 2500,
                Temperature = 0.2,
                TopP = 0.5,
            }
        };
        var promptTemplate = new PromptTemplate(
            Prompt,                          // Prompt template defined in natural language
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

    // Commented out to avoid conflict with DocumentIntelligencePlugin which is under development and testing
    //[Function("GetDescriptionOfSection")]
    //[OpenApiOperation(operationId: "GetDescriptionOfSection", tags: new[] { "ExecuteFunction" }, Description = "Get a description of a section in the application, include required data.")]
    //[OpenApiParameter(name: "sectionName", Description = "The name of the section", Required = true, In = ParameterLocation.Query)]
    //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns a description of the format of a section.")]
    //[OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> GetDescriptionOfSectionAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        //_logger.LogInformation("C# HTTP trigger function processed a request.");

        string sectionName = req.Query.GetValues("sectionName").First();

        SemanticTextMemory semanticMemory = new SemanticTextMemory(
            new AzureCognitiveSearchMemoryStore(
                this._aiOptions.Value.CognitiveSearch.Endpoint,
                this._aiOptions.Value.CognitiveSearch.Key!
            ),
            new AzureTextEmbeddingGeneration(
                this._aiOptions.Value.OpenAI.EmbeddingModel,
                this._aiOptions.Value.OpenAI.Endpoint,
                this._aiOptions.Value.OpenAI.Key!
            )
        );


        IAsyncEnumerable<MemoryQueryResult> memories = semanticMemory.SearchAsync(this._aiOptions.Value.CognitiveSearch.Index, sectionName, limit: 12);

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
                this._aiOptions.Value.OpenAI.CompletionModel,
                this._aiOptions.Value.OpenAI.Endpoint,
                this._aiOptions.Value.OpenAI.Key!
            )
            .Build();

        // Create semantic function that comes up with alternative section names
        const string Prompt = "Below, there are several {{$sectionName}} sections from previous applications. Describe all the information I would need to provide to create a similar section. Enumerate them all as a list. Be as verbose as necessary to include all required information.\n\n{{$input}}\n\n[FORMAT AND REQUIRED DATA FOR SECTION]\n";
        var promptConfig = new PromptTemplateConfig
        {
            Completion =
            {
                MaxTokens = 2500,
                Temperature = 0.2,
                TopP = 0.4
            }
        };
        var promptTemplate = new PromptTemplate(
            Prompt,                          // Prompt template defined in natural language
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
