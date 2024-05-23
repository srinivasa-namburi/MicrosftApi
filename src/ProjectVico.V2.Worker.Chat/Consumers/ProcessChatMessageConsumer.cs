using System.Text;
using AutoMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ProjectVico.V2.Shared.Contracts.Chat;
using ProjectVico.V2.Shared.Contracts.Messages.Chat.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.Chat.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using Microsoft.Data.SqlClient;

namespace ProjectVico.V2.Worker.Chat.Consumers;

public class ProcessChatMessageConsumer : IConsumer<ProcessChatMessage>
{
    private Kernel _kernel;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IServiceProvider _sp;
    private readonly OpenAIClient _openAIClient;
    private readonly string _deploymentName;
    private readonly int _numberOfPasses = 4;
    private const string CompleteTag = "[COMPLETE]";
    private const int SlidingWindowSize = 500;  // Adjust the size if needed - must always be bigger than ChunkOutputSize
    private const int ChunkOutputSize = 20; // Adjust the size if needed - must always be smaller than SlidingWindowSize

    public ProcessChatMessageConsumer(
        Kernel kernel,
        DocGenerationDbContext dbContext,
        IMapper mapper,
        IServiceProvider sp,
        [FromKeyedServices("openai-planner")] OpenAIClient openAIClient,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions
        )
    {
        _kernel = kernel;
        _dbContext = dbContext;
        _mapper = mapper;
        _sp = sp;
        _openAIClient = openAIClient;
        _deploymentName = serviceConfigurationOptions.Value.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName;
    }

    public async Task Consume(ConsumeContext<ProcessChatMessage> context)
    {
        var userMessageDto = context.Message.ChatMessageDto;

        try
        {
            await StoreChatMessage(userMessageDto);
        }
        catch (OperationCanceledException e)
        {
            // If the message is a duplicate, log the error and exit (as a successful run)
            return;
        }
        await ProcessUserMessage(userMessageDto, context);
    }

    private async Task StoreChatMessage(ChatMessageDTO chatMessageDto)
    {
        var chatMessage = _mapper.Map(chatMessageDto, new ChatMessage());

        if (chatMessage.Source == ChatMessageSource.User)
        {
            var userInformation =
                await _dbContext.UserInformations.FirstOrDefaultAsync(x =>
                    x.ProviderSubjectId == chatMessageDto.UserId);

            if (userInformation != null)
            {
                chatMessage.AuthorUserInformationId = userInformation.Id;
            }
        }

        _dbContext.ChatMessages.Add(chatMessage);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException e)
        {
            // If it's a foreign key constraint violation, log the error and exit (as a successful run)
            // This is likely a duplicate message being processed
            if (e.InnerException is SqlException sqlException && (sqlException.Number == 547 || sqlException.Number == 2627))
            {
                throw new OperationCanceledException("Duplicate message detected", e);

            }
        }
    }

    private async Task ProcessUserMessage(ChatMessageDTO userMessageDto, ConsumeContext<ProcessChatMessage> context)
    {
        var assistantMessageDto = new ChatMessageDTO()
        {
            ConversationId = userMessageDto.ConversationId,
            Source = ChatMessageSource.Assistant,
            CreatedAt = DateTime.UtcNow,
            ReplyToId = userMessageDto.Id,
            Id = Guid.NewGuid(),
            State = ChatMessageCreationState.InProgress
        };

        var conversation = await _dbContext.ChatConversations
            .FirstOrDefaultAsync(x => x.Id == userMessageDto.ConversationId);

        using var scope = _sp.CreateScope();

        _kernel = scope.ServiceProvider.GetRequiredKeyedService<Kernel>(conversation.DocumentProcessName + "-Kernel");

        var systemPrompt = conversation!.SystemPrompt;

        var chatHistoryString = await CreateChatHistoryString(context, userMessageDto, 10);
        var previousSummariesForConversationString = await GetSummariesConversationString(userMessageDto);

        var chatResponses = new List<string>();
        var lastPassResponse = new List<string>();
        string originalPrompt = "";

        var fullOutput = new StringBuilder();

        for (int i = 0; i < _numberOfPasses; i++)
        {
            // Initial setup for each pass
            string userPrompt;
            if (i == 0)
            {
                userPrompt = BuildInitialUserPrompt(chatHistoryString, previousSummariesForConversationString, userMessageDto.Message);
                originalPrompt = userPrompt; // Store the initial prompt for further passes
            }
            else
            {
                //var summary = await SummarizeOutput(string.Join("\n\n", chatResponses));
                var summary = string.Join("\n\n", chatResponses);
                userPrompt = BuildContinuingUserPrompt(summary, lastPassResponse, originalPrompt, i);
            }

            var responseDateSet = false;
            var isComplete = false;
            lastPassResponse.Clear(); // Clear lastPassResponse at the start of each pass

            var lineBuffer = new StringBuilder();
            var outputBuffer = new StringBuilder();
            string previousLine = null;

            await foreach (var stringUpdate in GetStreamingResponses(systemPrompt, userPrompt))
            {
                lineBuffer.Append(stringUpdate);

                while (lineBuffer.ToString().Contains("\n"))
                {
                    var line = lineBuffer.ToString();
                    var newLineIndex = line.IndexOf("\n");
                    var currentLine = line.Substring(0, newLineIndex + 1);

                    lineBuffer.Remove(0, newLineIndex + 1);

                    // Check for the complete tag in the current line
                    if (currentLine.Contains(CompleteTag, StringComparison.InvariantCultureIgnoreCase))
                    {
                        isComplete = true;
                        currentLine = currentLine.Replace(CompleteTag, "", StringComparison.InvariantCultureIgnoreCase);
                    }

                    // Add previous line to output buffer if available
                    if (previousLine != null)
                    {
                        outputBuffer.Append(previousLine);
                    }

                    // Set the current line as previous line
                    previousLine = currentLine;

                    // Output every 30 characters from the output buffer
                    while (outputBuffer.Length >= 30)
                    {
                        var outputSegment = outputBuffer.ToString(0, 30);
                        outputBuffer.Remove(0, 30);

                        if (!responseDateSet)
                        {
                            assistantMessageDto.CreatedAt = DateTime.UtcNow;
                            assistantMessageDto.State = ChatMessageCreationState.InProgress;
                            responseDateSet = true;
                        }

                        assistantMessageDto.Message += outputSegment;
                        await context.Publish<ChatMessageResponseReceived>(new ChatMessageResponseReceived(assistantMessageDto.ConversationId, assistantMessageDto, outputSegment));
                        chatResponses.Add(outputSegment);
                        lastPassResponse.Add(outputSegment);
                    }

                    if (isComplete)
                    {
                        break;
                    }
                }

                if (isComplete)
                {
                    break;
                }
            }

            // Process the last remaining line
            if (previousLine != null)
            {
                if (previousLine.Contains(CompleteTag, StringComparison.InvariantCultureIgnoreCase))
                {
                    previousLine = previousLine.Replace(CompleteTag, "", StringComparison.InvariantCultureIgnoreCase);
                    isComplete = true;
                }
                outputBuffer.Append(previousLine);
            }

            // Process any remaining content in the output buffer
            if (outputBuffer.Length > 0)
            {
                var remainingContent = outputBuffer.ToString();
                outputBuffer.Clear();

                // While the last character is a \n or \, remove it
                while (remainingContent.EndsWith("\n") ||
                    remainingContent.EndsWith("\\") ||
                    remainingContent.EndsWith("\\n") ||
                    remainingContent.EndsWith(" "))
                {
                    remainingContent = remainingContent.Substring(0, remainingContent.Length - 1);
                }

                assistantMessageDto.Message += remainingContent;
                await context.Publish<ChatMessageResponseReceived>(new ChatMessageResponseReceived(assistantMessageDto.ConversationId, assistantMessageDto, remainingContent));
                chatResponses.Add(remainingContent);
                lastPassResponse.Add(remainingContent);
            }

            if (isComplete)
            {
                break;
            }
        }

        // Finalize the message state
        assistantMessageDto.State = ChatMessageCreationState.Complete;
        try
        {
            await StoreChatMessage(assistantMessageDto);
        }
        catch (OperationCanceledException)
        {
            // If the message is a duplicate, log the error and exit (as a successful run)
            return;
        }
        await context.Publish<ChatMessageResponseReceived>(new ChatMessageResponseReceived(userMessageDto.ConversationId, assistantMessageDto, assistantMessageDto.Message));
    }

    private string BuildInitialUserPrompt(string chatHistoryString, string previousSummariesForConversationString, string userMessage)
    {
        return $"""
                The 5 last chat messages are between the [ChatHistory] and [/ChatHistory] tags.
                A summary of full conversation history is between the [ChatHistorySummary] and [/ChatHistorySummary] tags.
                The user's question is between the [User] and [/User] tags.

                Please consider this chat history when responding to the user, specifically looking for any context that may be relevant to the user's question.

                Respond with no decoration around your response, but use Markdown formatting.
                Use any plugins or tools you need to answer the question.
                 
                [ChatHistory]
                {chatHistoryString}
                [/ChatHistory]

                [ChatHistorySummary]
                {previousSummariesForConversationString}
                [/ChatHistorySummary]
                 
                [User]
                {userMessage}
                [/User]

                This is the initial query in a potentially multi-pass conversation. If you find it sufficient, you can return the whole
                response in this pass. If no more tokens are available for output, end this pass with no stop tag. That will signal that 
                the process should continue to the next pass.

                Try to write complete paragraphs instead of single sentences under a heading.

                Please be as verbose as neccessary in in your response for this pass. For this query in total, including this initial query,
                we can perform a total of {_numberOfPasses} passes to form a complete response. This is the first pass. Note that you should only use
                as many passes as you need. If you believe you have fully answered the user's query (and this is the last needed pass),
                please end your output with the following text surrounded by new lines:
                
                \n
                [COMPLETE]
                \n

                Note - do NOT use that tag to delineate the end of a single response. It should only be used to indicate the end of the whole section
                when no more passes are needed to finish the section output.

                Also - do NOT use any special tags to delineate the end of a single response when you expect the output to continue in further passes.
                The system will pass your response into the next pass automatically, ensuring continuity.

                Please don't end in the middle of a sentence or thought. If you need to continue in the next pass, end at a natural break in the text
                such as a line break, paragraph break or period. The next pass will pick up where you left off, but start with a new line character. ('\n')
                """;
    }

    private string BuildContinuingUserPrompt(string summary, List<string> lastPassResponse, string originalPrompt, int pass)
    {
        var lastResponse = string.Join("\n\n", lastPassResponse);

        return $"""
                 This is a continuing conversation.
                 
                 You're now going to continue the previous conversation, expanding on the previous output.
                 
                 This is the full output up to now is here between the [SUMMARY] tags:

                 [SUMMARY]
                 {summary}
                 [/SUMMARY]
                            
                 For the next step, you should continue the conversation. The prompt you should use is listed in the [ORIGINALPROMPT] tags below - 
                 but take care not to repeat the same information you've already provided (as detailed in the full output above.). Also ignore the initial 
                 heading and first two paragraphs of the prompt detailing how to respond to the first pass query. 
                 This is pass number {pass + 1} of {_numberOfPasses} - take that into account when you respond.

                 BEGIN your output by using a new line character. ('\n').
                 
                 Please start your response with content only - no ASSISTANT texts explaining your logic, tasks or reasoning.
                 The output from the passes should be possible to tie together with no further parsing necessary.
                 
                 If you believe the output for the whole section is complete (and this is the last needed pass),
                 please end your output with the following text surrounded by new lines:
                 
                 \n
                 [COMPLETE]
                 \n
       
                 Note - do NOT use that tag to delineate the end of a single response. It should only be used to indicate the end of the whole section
                 when no more passes are needed to finish the section output.
                 
                 ORIGINAL PROMPT with examples:

                 [ORIGINALPROMPT]
                 {originalPrompt}
                 [/ORIGINALPROMPT]

                 """;
    }

    private async IAsyncEnumerable<string> GetStreamingResponses(string systemPrompt, string userPrompt)
    {
        var openAiSettings = new OpenAIPromptExecutionSettings()
        {
            ChatSystemPrompt = systemPrompt,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 3072,
            Temperature = 0.5f,
            FrequencyPenalty = 0.5f
        };

        var kernelArguments = new KernelArguments(openAiSettings);

        await foreach (var response in _kernel.InvokePromptStreamingAsync(userPrompt, kernelArguments))
        {
            yield return response.ToString();
        }
    }

    private async Task<string> SummarizeOutput(string originalContent)
    {
        var systemPrompt = """
                           [SYSTEM]: This is a chat between an intelligent AI bot and one or more human participants.
                           The AI has been trained on GPT-4 LLM data through to April 2024.
                           """;

        var summarizePrompt = $"""
                              When responding, do not include ASSISTANT: or initial greetings/information about the reply. 
                              Only the content/summary, please. Please summarize the following text so it can form the basis of further 
                              conversation: 
                              
                              {originalContent}
                              """;

        var chatCompletionOptions = new ChatCompletionsOptions
        {
            Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(summarizePrompt)
            },
            DeploymentName = _deploymentName,
            MaxTokens = 1000,
            Temperature = 0.5f,
            FrequencyPenalty = 0.5f
        };

        var chatStringBuilder = new StringBuilder();
        await foreach (var chatUpdate in await _openAIClient.GetChatCompletionsStreamingAsync(chatCompletionOptions))
        {
            chatStringBuilder.Append(chatUpdate.ContentUpdate);
        }

        return chatStringBuilder.ToString();
    }

    private async Task<string> GetSummariesConversationString(ChatMessageDTO userMessageDto)
    {
        var summaries = await _dbContext.ConversationSummaries
            .Where(x => x.ConversationId == userMessageDto.ConversationId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        var summariesString = string.Join("\n", summaries.Select(summary => summary.SummaryText));
        return summariesString;
    }

    private async Task<string> CreateChatHistoryString(ConsumeContext<ProcessChatMessage> context,
        ChatMessageDTO userMessageDto, int numberOfMessagesToInclude = Int32.MaxValue)
    {
        var chatHistory = await _dbContext.ChatMessages
            .Where(x => x.ConversationId == userMessageDto.ConversationId && x.Id != userMessageDto.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(numberOfMessagesToInclude)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        var fullChatHistoryCount = _dbContext.ChatMessages.Count(x => x.ConversationId == userMessageDto.ConversationId);
        var chatHistoryString = CreateChatHistoryStringFromChatHistory(chatHistory);

        if (fullChatHistoryCount > numberOfMessagesToInclude)
        {
            var fiveEarliestMessages = chatHistory.OrderBy(x => x.CreatedAt).Take(5).ToList();
            var earliestMessage = fiveEarliestMessages.Last();
            await context.Publish(new GenerateChatHistorySummary(userMessageDto.ConversationId, earliestMessage.CreatedAt));
        }

        return chatHistoryString;
    }

    private static string CreateChatHistoryStringFromChatHistory(List<ChatMessage> chatHistory)
    {
        var chatHistoryBuilder = new StringBuilder();

        foreach (var chatMessage in chatHistory)
        {
            chatHistoryBuilder
                .AppendLine("role:" + chatMessage.Source)
                .AppendLine("content:" + chatMessage.Message);
        }

        return chatHistoryBuilder.ToString();
    }
}
