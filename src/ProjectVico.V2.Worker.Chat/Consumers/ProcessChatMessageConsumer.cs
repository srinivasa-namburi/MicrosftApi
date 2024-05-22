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

namespace ProjectVico.V2.Worker.Chat.Consumers;

public class ProcessChatMessageConsumer : IConsumer<ProcessChatMessage>
{
    private readonly Kernel _kernel;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private string _systemPrompt = "You are a friendly assistant answering questions posed by the user.";

    public ProcessChatMessageConsumer(
        Kernel kernel,
        DocGenerationDbContext dbContext,
        IMapper mapper)
    {
        _kernel = kernel;
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task Consume(ConsumeContext<ProcessChatMessage> context)
    {
        var userMessageDto = context.Message.ChatMessageDto;

        await StoreChatMessage(userMessageDto);
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
        await _dbContext.SaveChangesAsync();
    }

    private async Task ProcessUserMessage(ChatMessageDTO userMessageDto, ConsumeContext<ProcessChatMessage> context)
    {
        // Using Semantic Kernel, process the user message in a streaming fashion. For each update from the stream, publish an event with the 
        // full response generated so far, as well as a message with just the latest update. The API will consume these events and update the
        // chat interface with the full response and the latest update.

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

        var systemPrompt = conversation!.SystemPrompt;

        var openAiSettings = new OpenAIPromptExecutionSettings()
        {
            ChatSystemPrompt = systemPrompt,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 3072,
            Temperature = 0.7
        };

        var updateBlock = "";
        var responseDateSet = false;

        var chatHistoryString = await CreateChatHistoryString(context, userMessageDto, 10);
        var previousSummariesForConversationString = await GetSummariesConversationString(userMessageDto);

        var userPrompt =
            $"""
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
            {userMessageDto.Message}
            [/User]
            """;

        var kernelArguments = new KernelArguments(openAiSettings);

        await foreach (var response in _kernel.InvokePromptStreamingAsync(userPrompt, kernelArguments))
        {
            // Publish event with full response so far every 20 characters
            updateBlock += response;
            if (updateBlock.Length > 20)
            {
                if (!responseDateSet)
                {
                    assistantMessageDto.CreatedAt = DateTime.UtcNow;
                    assistantMessageDto.State = ChatMessageCreationState.InProgress;
                    responseDateSet = true;
                }
                assistantMessageDto.Message += updateBlock;
                await context.Publish<ChatMessageResponseReceived>(new ChatMessageResponseReceived(userMessageDto.ConversationId, assistantMessageDto, updateBlock));
                updateBlock = "";
            }
        }

        // Publish event with full response so far with any remaining unprocessed characters after stream has ended
        if (updateBlock.Length > 0)
        {
            assistantMessageDto.Message += updateBlock;
        }

        // Set the message state to complete
        assistantMessageDto.State = ChatMessageCreationState.Complete;

        await context.Publish<ChatMessageResponseReceived>(new ChatMessageResponseReceived(userMessageDto.ConversationId, assistantMessageDto, updateBlock));

        // Save Response Message to database
        await StoreChatMessage(assistantMessageDto);
    }

    private async Task<string> GetSummariesConversationString(ChatMessageDTO userMessageDto)
    {
        // Get all summaries for the conversation
        var summaries = await _dbContext.ConversationSummaries
            .Where(x => x.ConversationId == userMessageDto.ConversationId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        // Create a string with all summaries - separate each with a newline
        var summariesString = "";
        foreach (var summary in summaries)
        {
            summariesString += summary.SummaryText + "\n";
        }

        return summariesString;
    }


    /// <summary>
    /// Returns chat history, excluding the user message that triggered the assistant response
    /// </summary>
    /// <param name="context"></param>
    /// <param name="userMessageDto"></param>
    /// <param name="numberOfMessagesToInclude"></param>
    /// <returns>A formatted string with chat history</returns>
    private async Task<string> CreateChatHistoryString(ConsumeContext<ProcessChatMessage> context,
        ChatMessageDTO userMessageDto, int numberOfMessagesToInclude = Int32.MaxValue)
    {
        List<ChatMessage> chatHistory = new List<ChatMessage>();

        if (numberOfMessagesToInclude == Int32.MaxValue)
        {
            chatHistory = await _dbContext.ChatMessages
                .Where(x => x.ConversationId == userMessageDto.ConversationId && x.Id != userMessageDto.Id)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }
        else
        {
            chatHistory = await _dbContext.ChatMessages
                .Where(x => x.ConversationId == userMessageDto.ConversationId && x.Id != userMessageDto.Id)
                .OrderByDescending(x => x.CreatedAt)
                .Take(numberOfMessagesToInclude)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }


        if (chatHistory.Count == 0)
        {
            return "";
        }

        var fullChatHistoryCount = _dbContext.ChatMessages.Count(x => x.ConversationId == userMessageDto.ConversationId);

        var chatHistoryString = CreateChatHistoryStringFromChatHistory(chatHistory);

        // If the total chat history count (number of chat history items) for the conversation
        // is greater than the number of messages to include, publish a message to the bus to
        // potentially generate a summary of the chat history.
        // We want to summarize any messages prior to the 5 latest messages in the returned chatHistory list

        // We need to refresh the chat history list to get the 5 earliest messages
        var fiveEarliestMessages = chatHistory.OrderBy(x => x.CreatedAt).Take(5).ToList();
        // Get the date from the latest message in the list
        var earliestMessage = fiveEarliestMessages.Last();

        if (fullChatHistoryCount > numberOfMessagesToInclude)
        {
            await context.Publish(new GenerateChatHistorySummary(userMessageDto.ConversationId, earliestMessage.CreatedAt));
        }

        return chatHistoryString;
    }


    private static string CreateChatHistoryStringFromChatHistory(List<ChatMessage> chatHistory)
    {
        // Create a stringbuilder to store the chat history
        var chatHistoryBuilder = new StringBuilder();

        // For each chat message, create a string with timestamp, user/assistant, and message. Separate by | character and end with a newline
        foreach (var chatMessage in chatHistory)
        {
            chatHistoryBuilder
                .AppendLine("role:" + chatMessage.Source.ToString())
                .AppendLine("content:" + chatMessage.Message);

        }

        // Output the chat history to a string variable
        var chatHistoryString = chatHistoryBuilder.ToString();
        return chatHistoryString;
    }
}