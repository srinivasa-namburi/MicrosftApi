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

namespace ProjectVico.Plugins.Sample.FunctionApp;

public class Ask
{
    private readonly ILogger _logger;

    public Ask(ILoggerFactory loggerFactory)
    {
        //_logger = loggerFactory.CreateLogger<DemoHttpTrigger>();
    }

    [Function("Ask")]
    [OpenApiOperation(operationId: "Ask", tags: new[] { "ExecuteFunction" }, Description = "Ask the QnA bot a question about previous applications.")]
    [OpenApiParameter(name: "query", Description = "The question for the bot", Required = true, In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns the sum of the two numbers.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        //_logger.LogInformation("C# HTTP trigger function processed a request.");

        string query = req.Query.GetValues("query").First();

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

        // var gitHubFiles = new Dictionary<string, string>
        // {
        //     ["https://github.com/microsoft/semantic-kernel/blob/main/README.md"]
        //         = "README: Installation, getting started, and how to contribute",
        //     ["https://github.com/microsoft/semantic-kernel/blob/main/samples/notebooks/dotnet/02-running-prompts-from-file.ipynb"]
        //         = "Jupyter notebook describing how to pass prompts from a file to a semantic skill or function",
        //     ["https://github.com/microsoft/semantic-kernel/blob/main/samples/notebooks/dotnet/00-getting-started.ipynb"]
        //         = "Jupyter notebook describing how to get started with the Semantic Kernel",
        //     ["https://github.com/microsoft/semantic-kernel/tree/main/samples/skills/ChatSkill/ChatGPT"]
        //         = "Sample demonstrating how to create a chat skill interfacing with ChatGPT",
        //     ["https://github.com/microsoft/semantic-kernel/blob/main/dotnet/src/SemanticKernel/Memory/VolatileMemoryStore.cs"]
        //         = "C# class that defines a volatile embedding store",
        //     ["https://github.com/microsoft/semantic-kernel/blob/main/samples/dotnet/KernelHttpServer/README.md"]
        //         = "README: How to set up a Semantic Kernel Service API using Azure Function Runtime v4",
        //     ["https://github.com/microsoft/semantic-kernel/blob/main/samples/apps/chat-summary-webapp-react/README.md"]
        //         = "README: README associated with a sample chat summary react-based webapp",
        // };

        // foreach (var entry in gitHubFiles)
        // {
        //     await kernel.Memory.SaveReferenceAsync(
        //         collection: "mabolan-test",
        //         externalSourceName: "Matthew's Test Collection",
        //         externalId: entry.Key,
        //         description: "this is a description",
        //         text: entry.Value);
        // }


        IAsyncEnumerable<MemoryQueryResult> memories = semanticMemory.SearchAsync("section-embeddings", query, limit: 10);

        var textResponse = new StringBuilder();
        textResponse.AppendLine("I found the following information:");

        await foreach (var memory in memories)
        {
            textResponse.AppendLine("    Key: " + memory.Metadata.Id);
            textResponse.AppendLine("    Section: " + memory.Metadata.Description);
            textResponse.AppendLine("    Relevance: " + memory.Relevance);
            textResponse.AppendLine();
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        response.WriteString(textResponse.ToString() ?? string.Empty);

        return response;
    }
}
