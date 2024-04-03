using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using ProjectVico.V2.Plugins.Default.NuclearDocs.Services.AiCompletionService;
using ProjectVico.V2.Plugins.Shared;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Plugins.Default.NuclearDocs;

public class NRCDocumentsPlugin : IPluginImplementation
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly IIndexingProcessor _indexingProcessor;
    private readonly OpenAIClient _openAIClient;
    private readonly IAiCompletionService _aiCompletionService;

    public NRCDocumentsPlugin(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        IIndexingProcessor indexingProcessor,
        [FromKeyedServices("openai-planner")] OpenAIClient openAIClient,
        IAiCompletionService aiCompletionService
        )
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _indexingProcessor = indexingProcessor;
        _openAIClient = openAIClient;
        _aiCompletionService = aiCompletionService;
    }

    [KernelFunction("GetFullChaptersForQuery")]
    [Description(
        "Retrieves a list of section names relevant to the user's query, submitting the full query to perform a vector search for related sections.")]
    public async Task<IEnumerable<string?>> GetFullChaptersForQueryAsync(
        [Description("The query to search for.")]
        string query)
    {
        var documents = await _indexingProcessor.SearchWithHybridSearch(query);
        var outputStrings = await GenerateUniqueSectionNamesForQueryAsync(documents);
        return outputStrings;
    }

    [KernelFunction("GetDescriptionForDocumentSection")]
    [Description(
        "Writes a description of what types of information is needed, and what should be included, to write a specific section indicated by the section name.")]
    public async Task<List<string>> GetDescriptionForDocumentSectionAsync(
        [Description(
            "The name of the section. Please remove any chapter or section numbering from the section names you're searching for.")]
        string sectionName)
    {
        var documents = await _indexingProcessor.SearchWithHybridSearch(sectionName);
        var outputStrings = await GenerateSectionDescriptionWithOpenAIAsync(documents);
        return outputStrings;
    }

    [KernelFunction("GetStreamingBodyTextForSection")]
    [Description(
        "Writes the body text in a streaming fashion for a title or section, ignoring its sub sections. If no feasible body text is found, returns an empty string")]
    public async IAsyncEnumerable<string> GetStreamingBodyTextForTitleOrSection(
        [Description("The name of the section or title/heading")]
        string sectionOrTitleText,
        [Description("The Content Node Type we are dealing with - either Heading or Title")]
        string contentNodeTypeString,
        [Description("A long string with an indented table of contents for the whole document to provide context")]
        string tableOfContentsString,
        [Description("The ID of a metadata record for inclusion into generation. Optional.")]
        Guid? metadataId = null,
        [Description("The Section or Title number - 1, 1.1, 1.1.1 etc. Optional.")]
        string sectionOrTitleNumber = ""
    )
    {
        var contentNodeType = ContentNodeType.Heading;
        if (contentNodeTypeString == "Title")
        {
            contentNodeType = ContentNodeType.Title;
        }

        List<ReportDocument> documents = new List<ReportDocument>();
        if (contentNodeType == ContentNodeType.Heading)
        {
            documents = await _indexingProcessor.SearchWithHybridSearch(sectionOrTitleNumber + " " +
                                                                        sectionOrTitleText);
        }
        else if (contentNodeType == ContentNodeType.Title)
        {

            documents = await _indexingProcessor.SearchWithTitleSearch(sectionOrTitleNumber + " " +
                                                                       sectionOrTitleText);
        }
        else
        {
            throw new ArgumentException(
                "contentNodeType must be either ContentNodeType.Heading or ContentNodeType.Title",
                nameof(contentNodeType));
        }

        await foreach (var bodyTextPart in GenerateStreamingBodyTextForTitleOrSection(sectionOrTitleNumber,
            sectionOrTitleText, contentNodeType, documents, tableOfContentsString, metadataId))
        {
            yield return bodyTextPart;
        }
    }

    private async IAsyncEnumerable<string> GenerateStreamingBodyTextForTitleOrSection(string sectionOrTitleNumber,
        string sectionOrTitleText, ContentNodeType contentNodeType, List<ReportDocument> documents,
        string tableOfContentsString, Guid? metadataId = null)
    {
        var bodyContextTextStream = _aiCompletionService.GetStreamingBodyContentText(documents,
            sectionOrTitleNumber, sectionOrTitleText, contentNodeType, tableOfContentsString, metadataId);

        await foreach (var bodyTextPart in bodyContextTextStream)
        {
            yield return bodyTextPart;
        }
    }


    [KernelFunction("GenerateDocumentOutline")]
    [Description("Writes an outline for a new environmental report, including all sections and subsections, using knowledge of similar written sections from earlier environmental reports.")]
    public async Task<List<string>> GenerateDocumentOutlineAsync()
    {
        var titleDocuments = await _indexingProcessor.GetAllUniqueTitlesAsync(20);
        List<string> outputStrings = await GenerateDocumentOutlineWithOpenAiAsync(titleDocuments.ToList());
        return outputStrings;
    }

    private async Task<List<string>> GenerateDocumentOutlineWithOpenAiAsync(List<ReportDocument> titleDocuments)
    {
        var sectionStrings = new ConcurrentBag<string>();
        await Task.WhenAll(titleDocuments.Select(async (titleDocument) =>
        {
            var prompt = GenerateSectionOutputPromptForTitle(titleDocument);
            var sectionCompletion = await _openAIClient.GetChatCompletionsAsync(
            new ChatCompletionsOptions()
            {
                DeploymentName = _serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName,
                Messages = {
                        new ChatRequestUserMessage(prompt)
                    },
                MaxTokens = 400,
                Temperature = 0.1f,
                FrequencyPenalty = 0.7f
            });

            var chatResponseMessage = sectionCompletion.Value.Choices[0].Message.Content;
            sectionStrings.Add(chatResponseMessage);
        }));

        var systemPrompt =
            "Follow the instructions below accurately. Do not generate any introductory text or additional numbering, bullets, etc. Just the list of sections.";

        var prompt = $"""
                      This is a list of sections and subsections gathered from many different environmental reports.
                      Please deduplicate the list of sections and subsections, and generate a complete outline for the document.
                      To deduplicate, please remove any sections or subsections that seem very similar. The content is drawn from many reports, so there will be some overlap.
                      Create a numbering system that mimics the section numbering in the full
                      pool of sections you are using. 
                      MAKE SURE that you retain the correct hierarchy of sections and subsections, 
                      preserving the numbering system of [1.1] or [1.1.1] (etc) for a section and [1] for a chapter title.
                      Don't turn subsections (1.1, 1.1.1) into chapters (1) or vice versa. The hierarchical structure is important.
                      Remove all TABLE OF CONTENTS, INDEX, APPENDIX, and other non-content chapters and their subsections.
                      The list of sections follow in the [CONTENT: <content>] section in this request.
                      [CONTENT: {string.Join("\n", sectionStrings)}]\n
                      """;

        var deduplicatedOutlineResponse = await _openAIClient.GetChatCompletionsAsync(
        new ChatCompletionsOptions()
        {
            DeploymentName = _serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName,
            Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(prompt)
                },
            MaxTokens = 800,
            Temperature = 0.1f,
            FrequencyPenalty = 0.8f
        });

        var chatResponseMessage = deduplicatedOutlineResponse.Value.Choices[0].Message.Content;
        var outputStrings = new List<string>(chatResponseMessage.Split("\n"));

        return outputStrings;
    }

    private string GenerateSectionOutputPromptForTitle(ReportDocument titleDocument)
    {
        var prompt = $"""
                      [SYSTEM]: Follow the instructions below accurately. Do not generate any introductory text or additional numbering, 
                      bullets, etc. Just the list of sections. \n\n
                      If there are no subsections, return just the root section ({titleDocument.Title}).\n\n
                      [USER]:
                      This is a title/chapter in an environmental report.
                      Please extract the sections and subsections in the <Content>. These can be identified by a numeric pattern with periods.
                      They are in the Content of the section, indicated by [SECTION CONTENT: <content>].
                      For example, if you have a Chapter called '1 Waste management', and it has subsections '1.2 Radioactive waste management'
                      and '1.3 Non-radioactive waste management', then you would extract these as subsections of '1 Waste management'. 
                      Please go up to 3 levels deep with the subsections.
                      Please also return the root (first) title/chapter which is indicated by [SECTION: <section title>].
                      [SECTION CONTENT: {titleDocument.Content}]\n
                      """;

        return prompt;
    }

    private async Task<IEnumerable<string?>> GenerateUniqueSectionNamesForQueryAsync(List<ReportDocument> documents)
    {
        var sectionNames = new HashSet<string?>();
        foreach (var document in documents)
        {
            sectionNames.Add(document.Title);
        }
        return sectionNames;
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
        var sectionCompletion = await _openAIClient.GetChatCompletionsAsync(
                       new ChatCompletionsOptions()
                       {
                           DeploymentName = _serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName,
                           Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestSystemMessage(sectionPrompt)
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
