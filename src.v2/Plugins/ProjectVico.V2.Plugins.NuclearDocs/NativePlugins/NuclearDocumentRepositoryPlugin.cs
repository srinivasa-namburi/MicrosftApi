using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Models.Enums;
using static System.Collections.Specialized.BitVector32;

namespace ProjectVico.V2.Plugins.NuclearDocs.NativePlugins;

public class NuclearDocumentRepositoryPlugin
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly IIndexingProcessor _indexingProcessor;
    private readonly OpenAIClient _openAIClient;

    public NuclearDocumentRepositoryPlugin(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        IIndexingProcessor indexingProcessor,
        [FromKeyedServices("openai-planner")] OpenAIClient openAIClient)
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _indexingProcessor = indexingProcessor;
        _openAIClient = openAIClient;
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

    [KernelFunction("GetDocumentOutputForSection")]
    [Description(
        "Writes output for a specific section indicated by the section name, using knowledge of similar written sections from earlier environmental reports.")]
    public async Task<List<string>> GetOutputForSectionAsync(
        [Description(
            "The name of the section. Please remove any chapter or section numbering from the section names you're searching for.")]
        string sectionName)
    {
        var documents = await _indexingProcessor.SearchWithHybridSearch(sectionName);
        var outputStrings = await GenerateSectionOutputWithOpenAIAsync(documents);
        return outputStrings;
    }

    [KernelFunction("GetBodyTextNodesOnly")]
    [Description(
        "Gets the body text for each title or section, ignoring its sub sections. If no feasible body text is found, returns an empty list")]
    public async Task<List<ContentNode>> GetBodyTextContentNodesOnlyForTitle(
        [Description("The Section or Title number - 1, 1.1, 1.1.1 etc")]
        string sectionOrTitleNumber,
        [Description("The name of the section or title/heading")]
        string sectionOrTitleText,
        [Description("The Content Node Type we are dealing with - either Heading or Title")]
        string contentNodeTypeString
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

        List<ContentNode> bodyContentNodes =
            await GenerateBodyContentNodesForTitleOrSection(sectionOrTitleNumber, sectionOrTitleText, contentNodeType,
                documents);

        return bodyContentNodes;


    }

    private async Task<List<ContentNode>> GenerateBodyContentNodesForTitleOrSection(string sectionOrTitleNumber,
        string sectionOrTitleText, ContentNodeType contentNodeType, List<ReportDocument> documents)
    {
        // Generate example  // Build the examples for the prompt
        var sectionExample = new StringBuilder();

        // Get the 6 first documents
        var firstDocuments = documents.Take(6).ToList();

        foreach (var document in firstDocuments)
        {
            sectionExample.AppendLine($"[EXAMPLE: {document.Title}]");
            sectionExample.AppendLine(document.Content);
            sectionExample.AppendLine();
        }

        // Generate section output prompt
        var exampleString = sectionExample.ToString();
        //string sectionPrompt =
        //    $"""
        //     Below, there are several sections from previous applications denoted by [EXAMPLE: <section title>].

        //     Prioritize the FIRST three or four of these examples as they are the most relevant.

        //     Don't disregard the other examples - at least summarize their content
        //     to see if it fits in with the primary section or is usable to expand on it.

        //     Using this information, write a similar section(sub-section) or chapter(section),
        //     depending on which is most appropriate to the query. 

        //     Be as verbose as necessary to include all required information.

        //     If you encounter sub sections ("{sectionOrTitleNumber}.1", "{sectionOrTitleNumber}.1.1", etc),
        //     DISREGARD THEM AND ALL THEIR CONTENT. ONLY write the body text that belongs to
        //     the main section ("{sectionOrTitleText}")

        //     Make sure to clean up the text inside the Text property of the generated json array which the plugin returns.
        //     In particular, make sure it complies with UTF-8 encoding. Paragraphs should be separateed by two newlines (\n\n)
        //     For lists, use only * and - characters as bullet points, and make sure to have a space after the bullet point character.

        //     If you are missing details to write specific portions, please indicate that with [DETAIL: <dataType>] -
        //     and put the type of data needed in the dataType parameter.
        //     \n\n
        //     {exampleString}
        //     \n\n
        //     """;

        string sectionPrompt =
           $"""
             Below, there are several sections from previous applications denoted by [EXAMPLE: <section title>].
             
             Prioritize the FIRST three or four of these examples as they are the most relevant.
             
             Don't disregard the other examples - at least summarize their content
             to see if it fits in with the primary section or is usable to expand on it.
             
             Using this information, write a similar section(sub-section) or chapter(section),
             depending on which is most appropriate to the query. 
             
             Be as verbose as necessary to include all required information.
             
             If you encounter sub sections ("{sectionOrTitleNumber}.1", "{sectionOrTitleNumber}.1.1", etc),
             DISREGARD THEM AND ALL THEIR CONTENT. ONLY write the body text that belongs to
             the main section ("{sectionOrTitleText}")
             
             For headings, remove the numbering and any heading prefixes like "Section" or "Chapter" from the heading text.
             
             Make sure to clean up the text inside the Text property of the generated json array which the plugin returns.
             In particular, make sure it complies with UTF-8 encoding. Paragraphs should be separateed by two newlines (\n\n)
             For lists, use only * and - characters as bullet points, and make sure to have a space after the bullet point character.
             
             Format the text with Markdown syntax. For example, use #, ##, ### for headings, * and - for bullet points, etc.
             
             If you are missing details to write specific portions, please indicate that with [DETAIL: <dataType>] -
             and put the type of data needed in the dataType parameter.
             \n\n
             {exampleString}
             \n\n
             """;

        var systemPrompt =
            "[SYSTEM]: This is a chat between an intelligent AI bot specializing in assisting with producing environmental reports for Small Modular nuclear Reactors (''SMR'') and one or more participants. The AI has been trained on GPT-4 LLM data through to April 2023 and has access to additional data on more recent SMR environmental report samples. Try to be complete with your responses. Provide responses that can be copied directly into an environmental report, so no polite endings like 'i hope that helps', no beginning with 'Sure, I can do that', etc.\"";

        var chatResponses = new List<string>();

        var chatCompletionOptions = new ChatCompletionsOptions()
        {
            Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(sectionPrompt)
            },
            DeploymentName = this._serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName,
            MaxTokens = 16384,
            Temperature = 0.2f,
            FrequencyPenalty = 0.5f
        };

        StringBuilder chatStringBuilder = new StringBuilder();

        await foreach (StreamingChatCompletionsUpdate chatUpdate in
                       await _openAIClient.GetChatCompletionsStreamingAsync(chatCompletionOptions))
        {
            if (chatUpdate.Role.HasValue)
            {
                Console.Write($"{chatUpdate.Role.Value.ToString().ToUpperInvariant()}: ");
            }

            if (string.IsNullOrEmpty(chatUpdate.ContentUpdate)) continue;

            Console.Write(chatUpdate.ContentUpdate);
            chatStringBuilder.Append(chatUpdate.ContentUpdate);
        }

        chatResponses.Add(chatStringBuilder.ToString());

        List<ContentNode> bodyContentNodes = new List<ContentNode>();

        foreach (var chatResponse in chatResponses)
        {
            var bodyContentNode = new ContentNode()
            {
                Id = Guid.NewGuid(),
                Text = chatResponse,
                Type = ContentNodeType.BodyText
            };

            bodyContentNodes.Add(bodyContentNode);
        }

        return bodyContentNodes;

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

        var prompt = $"""
                      [SYSTEM]: Follow the instructions below accurately. Do not generate any introductory text or additional numbering, 
                      bullets, etc. Just the list of sections. \n\n
                      [USER]:
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
        var sectionCompletion = await this._openAIClient.GetChatCompletionsAsync(
                       new ChatCompletionsOptions()
                       {
                           DeploymentName = this._serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName,
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
            $"Below, there are several sections from previous applications. Prioritize the FIRST of these examples as it is the most relevant. Don't disregard the other examples though - at least summarize their content to see if it fits in with the primary section or is usable to expand on it.  Using this information, write a similar section(sub-section) or chapter(section), depending on which is most appropriate to the query. Be as verbose as necessary to include all required information. If you are missing details to write specific portions, please indicate that with [DETAIL: <dataType>] - and put the type of data needed in the dataType parameter.\n\n{exampleString}\n\n";

        var systemPrompt =
            "[SYSTEM]: This is a chat between an intelligent AI bot specializing in assisting with producing environmental reports for Small Modular nuclear Reactors (''SMR'') and one or more participants. The AI has been trained on GPT-4 LLM data through to April 2023 and has access to additional data on more recent SMR environmental report samples. Try to be complete with your responses. Provide responses that can be copied directly into an environmental report, so no polite endings like 'i hope that helps', no beginning with 'Sure, I can do that', etc.\"";

        var chatResponses = new List<string>();

        var chatCompletionOptions = new ChatCompletionsOptions()
        {
            Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(sectionPrompt)
            },
            DeploymentName = this._serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName,
            MaxTokens = 8192,
            Temperature = 0.2f,
            FrequencyPenalty = 0.5f
        };

        StringBuilder chatStringBuilder = new StringBuilder();

        await foreach (StreamingChatCompletionsUpdate chatUpdate in
                       await _openAIClient.GetChatCompletionsStreamingAsync(chatCompletionOptions))
        {
            if (chatUpdate.Role.HasValue)
            {
                Console.Write($"{chatUpdate.Role.Value.ToString().ToUpperInvariant()}: ");
            }

            if (string.IsNullOrEmpty(chatUpdate.ContentUpdate)) continue;

            Console.Write(chatUpdate.ContentUpdate);
            chatStringBuilder.Append(chatUpdate.ContentUpdate);
        }

        chatResponses.Add(chatStringBuilder.ToString());
        return chatResponses;

    }

}
