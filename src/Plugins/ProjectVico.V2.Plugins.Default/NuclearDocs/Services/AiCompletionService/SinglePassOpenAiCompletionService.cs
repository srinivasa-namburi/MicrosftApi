using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Plugins.Default.NuclearDocs.Services.AiCompletionService;

public class SinglePassOpenAiCompletionService : IAiCompletionService
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly OpenAIClient _openAIClient;
    private readonly TableHelper _tableHelper;

    public SinglePassOpenAiCompletionService(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("openai-planner")] OpenAIClient openAIClient,
        TableHelper tableHelper
    )
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _openAIClient = openAIClient;
        _tableHelper = tableHelper;
    }
    public async Task<List<ContentNode>> GetBodyContentNodes(List<ReportDocument> documents,
        string sectionOrTitleNumber, string sectionOrTitleText, ContentNodeType contentNodeType,
        string tableOfContentsString, Guid? metadataId)
    {
        // Generate example  // Build the examples for the prompt
        var sectionExample = new StringBuilder();

        // Get the 3 first documents
        var firstDocuments = documents.Take(3).ToList();

        //For each document, replace any TABLE_REFERENCE tags with a HTML rendering of the Table in question
        foreach (var document in firstDocuments)
        {
            document.Content = _tableHelper.ReplaceTableReferencesWithHtml(document.Content);
        }

        foreach (var document in firstDocuments)
        {
            sectionExample.AppendLine($"[EXAMPLE: {document.Title}]");
            sectionExample.AppendLine(document.Content);
            sectionExample.AppendLine();
        }

        // Generate section output prompt
        var exampleString = sectionExample.ToString();

        string sectionPrompt =
            $"""
             Below, there are several sections from previous applications denoted by [EXAMPLE: <section title>].

             Prioritize the FIRST three or four of these examples as they are the most relevant.

             Don't disregard the other examples - at least summarize their content
             to see if it fits in with the primary section or is usable to expand on it.

             Using this information, write a similar section(sub-section) or chapter(section),
             depending on which is most appropriate to the query.

             Be as verbose as necessary to include all required information. Try to be very complete in your response, considering all source data. We
             are looking for full sections or chapters - not short summaries.

             For headings, remove the numbering and any heading prefixes like "Section" or "Chapter" from the heading text.

             Make sure to clean up the text inside the Text property of the generated json array which the plugin returns.
             In particular, make sure it complies with UTF-8 encoding. Paragraphs should be separateed by two newlines (\n\n)
             For lists, use only * and - characters as bullet points, and make sure to have a space after the bullet point character.

             Format the text with Markdown syntax. For example, use #, ##, ### for headings, * and - for bullet points, etc.

             The exception to this is if you encounter tables (they start and  end with <table> and </table> tags) - they should be rendered
             as HTML tables in the output if they contain colspan or rowspan attributes in any of their cells, but can be rendered as MarkDown tables if they are simple tables
             without col- or row spans. As a rule, they ar HTML tables in the source examples.

             If you are missing details to write specific portions, please indicate that with [DETAIL: <dataType>] -
             and put the type of data needed in the dataType parameter. Make sure to look at all the source content before you decide you lack details!
             \n\n
             {exampleString}
             \n\n
             """;

        var systemPrompt =
            "[SYSTEM]: This is a chat between an intelligent AI bot specializing in assisting with producing environmental reports for Small Modular nuclear Reactors (''SMR'') and one or more participants. The AI has been trained on GPT-4 LLM data through to April 2023 and has access to additional data on more recent SMR environmental report samples. Try to be complete with your responses. Provide responses that can be copied directly into an environmental report, so no polite endings like 'i hope that helps', no beginning with 'Sure, I can do that', etc.\"";

        var chatResponses = new List<string>();

        var chatCompletionOptions = new ChatCompletionsOptions
        {
            Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(sectionPrompt)
            },
            DeploymentName = _serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName,
            MaxTokens = 15000,
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
}