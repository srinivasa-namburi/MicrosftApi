// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using System.Net;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.CognitiveSearch.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;

namespace ProjectVico.Plugins.DocQnA;

public class DocumentIntelligencePlugin
{
    private readonly AiOptions _aiOptions;
    private readonly IIndexingProcessor _indexingProcessor;
    private readonly OpenAIClient _openAIClient;

    public DocumentIntelligencePlugin(IOptions<AiOptions> aiOptions, IIndexingProcessor indexingProcessor, OpenAIClient openAIClient)
    {
        this._aiOptions = aiOptions.Value;
        this._indexingProcessor = indexingProcessor;
        this._openAIClient = openAIClient;
    }

    [Function("GetFullChaptersForQuery")]
    [OpenApiOperation(operationId: "GetFullChaptersForQuery", tags: new[] { "ExecuteFunction" },
        Description = "To retrieve a list of section names relevant to the user's query, submit the full query to perform a vector search for related sections")]
    [OpenApiParameter(name: "query", Description = "The query to search for", Required = true,
        In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string),
        Description = "Returns a list of chapters/sections/titles containing the query separated by newlines.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json",
        bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> GetFullChaptersForQueryAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
        HttpRequestData req)
    {
        string query = req.Query.GetValues("query").First();

        // Retrieve documents via Indexing Processor
        var documents = await this._indexingProcessor.SearchWithHybridSearch(query);

        var outputStrings = await this.GenerateUniqueSectionNamesForQueryAsync(documents);

        // Generate a full string from the outputStrings, which is a List<string>
        var outputString = string.Join("\n\n", outputStrings);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");


        await response.WriteStringAsync(outputString ?? string.Empty);

        return response;

    }

    private async Task<IEnumerable<string?>> GenerateUniqueSectionNamesForQueryAsync(List<ReportDocument> documents)
    {
        var sectionNames = new List<string?>();
        var sectionNamesSet = new HashSet<string?>();
        foreach (var document in documents)
        {
            var sectionName = document.Title;
            if (!sectionNamesSet.Contains(sectionName))
            {
                sectionNamesSet.Add(sectionName);
                sectionNames.Add(sectionName);
            }
        }

        return sectionNames;

    }


    [Function("GetDescriptionForDocumentSection")]
    [OpenApiOperation(operationId: "GetDescriptionForDocumentSection", tags: new[] { "ExecuteFunction" },
        Description =
            "Using your knowledge of similar written section from earlier environmental reports, write a description of what types of information is needed, and what should be included,  to write a specific section indicated by <sectionName>.")]
    [OpenApiParameter(name: "sectionName",
        Description =
            "The name of the section. Please remove any chapter or section numbering from the section names you're searching for.",
        Required = true,
        In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string),
        Description = "Returns a description on how to write a particular section indicated by <sectionName>")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json",
        bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> GetDescriptionForDocumentSectionAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
        HttpRequestData req)
    {
        string sectionName = req.Query.GetValues("sectionName").First();

        // Retrieve documents via Indexing Processor
        var documents = await this._indexingProcessor.SearchWithHybridSearch(sectionName);

        var outputStrings = await this.GenerateSectionDescriptionWithOpenAIAsync(documents);

        // Generate a full string from the outputStrings, which is a List<string>
        var outputString = string.Join("\n\n", outputStrings);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await response.WriteStringAsync(outputString ?? string.Empty);

        return response;
    }


    [Function("GetDocumentOutputForSection")]
    [OpenApiOperation(operationId: "GetDocumentOutputForSection", tags: new[] { "ExecuteFunction" },
        Description =
            "Using your knowledge of similar written section from earlier environmental reports, write output for a specific section indicated by <sectionName>.")]
    [OpenApiParameter(name: "sectionName", Description = "The name of the section. Please remove any chapter or section numbering from the section names you're searching for.", Required = true,
        In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string),
        Description = "Returns a output for a particular section indicated by <sectionName>")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json",
        bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> GetOutputForSectionAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        string sectionName = req.Query.GetValues("sectionName").First();

        // Retrieve documents via Indexing Processor
        var documents = await this._indexingProcessor.SearchWithHybridSearch(sectionName);
        var outputStrings = await this.GenerateSectionOutputWithOpenAIAsync(documents);

        // Generate a full string from the outputStrings, which is a List<string>
        var outputString = string.Join("\n\n", outputStrings);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await response.WriteStringAsync(outputString ?? string.Empty);

        return response;
    }

    private async Task<List<string>> GenerateSectionDescriptionWithOpenAIAsync(List<ReportDocument> sections)
    {
                // Generate example  // Build the examples for the prompt
        var sectionExample = new StringBuilder();

        // Get the 6 first sections
        var firstSections = sections.Take(6).ToList();

        foreach (var section in firstSections)
        {
            sectionExample.AppendLine($"[EXAMPLE: {section.Title}]");
            sectionExample.AppendLine(section.Content);
            sectionExample.AppendLine();
        }

        // Generate section output prompt
        var exampleString = sectionExample.ToString();
        string sectionPrompt =
            $"Below, there are several sections from previous applications. Prioritize the FIRST of these examples as it is the most relevant. Don't disregard the other examples though - at least summarize their content to see if it fits in with the primary section or is usable to expand on it.  Using this information, describe the information and inputs necessary to write a similar section. Be as verbose as necessary to include all required information. If you are missing details to write specific portions, please indicate that with [DETAIL: <dataType>] - and put the type of data needed in the dataType parameter.\n\n{exampleString}\n\n";

        var systemPrompt =
            "[SYSTEM]: This is a chat between an intelligent AI bot specializing in assisting with producing environmental reports for Small Modular nuclear Reactors (''SMR'') and one or more participants. The AI has been trained on GPT-4 LLM data through to April 2023 and has access to additional data on more recent SMR environmental report samples. Try to be complete with your responses. Provide responses that can be copied directly into an environmental report, so no polite endings like 'i hope that helps', no beginning with 'Sure, I can do that', etc.\"";

        var chatResponses = new List<string>();

        // Generate chat completion for section output
        var sectionCompletion = await this._openAIClient.GetChatCompletionsAsync(
                       new ChatCompletionsOptions()
                       {
                DeploymentName = this._aiOptions.OpenAI.CompletionModel,
                Messages =
                {
                    new ChatMessage("system", systemPrompt),
                    new ChatMessage("user", sectionPrompt)
                },
                MaxTokens = 8192,
                Temperature = 0.2f,
                FrequencyPenalty = 0.5f
            });

        // Get the response from the API
        var chatResponseMessage = sectionCompletion.Value.Choices[0].Message.Content;
        chatResponses.Add(chatResponseMessage);
        return chatResponses;
    }

    private async Task<List<string>> GenerateSectionOutputWithOpenAIAsync(List<ReportDocument> sections)
    {
        // Generate example  // Build the examples for the prompt
        var sectionExample = new StringBuilder();

        // Get the 6 first sections
        var firstSections = sections.Take(6).ToList();

        foreach (var section in firstSections)
        {
            sectionExample.AppendLine($"[EXAMPLE: {section.Title}]");
            sectionExample.AppendLine(section.Content);
            sectionExample.AppendLine();
        }

        // Generate section output prompt
        var exampleString = sectionExample.ToString();
        string sectionPrompt =
            $"Below, there are several sections from previous applications. Prioritize the FIRST of these examples as it is the most relevant. Don't disregard the other examples though - at least sumamrize their content to see if it fits in with the primary section or is usable to expand on it.  Using this information, write a similar section(sub-section) or chapter(section), depending on which is most appropriate to the query. Be as verbose as necessary to include all required information. If you are missing details to write specific portions, please indicate that with [DETAIL: <dataType>] - and put the type of data needed in the dataType parameter.\n\n{exampleString}\n\n";

        var systemPrompt =
            "[SYSTEM]: This is a chat between an intelligent AI bot specializing in assisting with producing environmental reports for Small Modular nuclear Reactors (''SMR'') and one or more participants. The AI has been trained on GPT-4 LLM data through to April 2023 and has access to additional data on more recent SMR environmental report samples. Try to be complete with your responses. Provide responses that can be copied directly into an environmental report, so no polite endings like 'i hope that helps', no beginning with 'Sure, I can do that', etc.\"";

        var chatResponses = new List<string>();

        // Generate chat completion for section output
        var sectionCompletion = await this._openAIClient.GetChatCompletionsAsync(
            new ChatCompletionsOptions()
            {
                DeploymentName = this._aiOptions.OpenAI.CompletionModel,
                Messages =
                {
                    new ChatMessage("system", systemPrompt),
                    new ChatMessage("user", sectionPrompt)
                },
                MaxTokens = 8192,
                Temperature = 0.2f,
                FrequencyPenalty = 0.5f
            });

        // Get the response from the API
        var chatResponseMessage = sectionCompletion.Value.Choices[0].Message.Content;
        chatResponses.Add(chatResponseMessage);
        return chatResponses;
    }
}
