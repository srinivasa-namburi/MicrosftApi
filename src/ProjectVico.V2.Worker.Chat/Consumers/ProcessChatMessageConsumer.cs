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
using Microsoft.Data.SqlClient;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.Shared.Prompts;
using Scriban;

namespace ProjectVico.V2.Worker.Chat.Consumers;

public class ProcessChatMessageConsumer : IConsumer<ProcessChatMessage>
{
    private Kernel _kernel;
    private IPromptCatalogTypes _promptCatalogTypes;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IServiceProvider _sp;
    
    public ProcessChatMessageConsumer(
        DocGenerationDbContext dbContext,
        IMapper mapper,
        IServiceProvider sp
        )
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _sp = sp;
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

        // Set up Semantic Kernel for the right Document Process
        using var scope = _sp.CreateScope();

        //_kernel = scope.ServiceProvider.GetRequiredKeyedService<Kernel>(conversation.DocumentProcessName + "-Kernel");
        _kernel = scope.ServiceProvider.GetRequiredServiceForDocumentProcess<Kernel>(conversation.DocumentProcessName)!;

        _promptCatalogTypes =
            scope.ServiceProvider.GetRequiredServiceForDocumentProcess<IPromptCatalogTypes>(conversation.DocumentProcessName)!;

        var systemPrompt = _promptCatalogTypes.ChatSystemPrompt;

        var openAiSettings = new OpenAIPromptExecutionSettings()
        {
            ChatSystemPrompt = systemPrompt,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 3512,
            Temperature = 1.0
        };



        var chatHistoryString = await CreateChatHistoryString(context, userMessageDto, 10);
        var previousSummariesForConversationString = await GetSummariesConversationString(userMessageDto);

        var userPrompt = BuildUserPrompt(
            chatHistoryString, 
            previousSummariesForConversationString,
            userMessageDto.Message);
        
        var kernelArguments = new KernelArguments(openAiSettings);

        var updateBlock = "";
        var responseDateSet = false;

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

    private string BuildUserPrompt(string chatHistoryString, string previousSummariesForConversationString, string userMessage)
    {
        var initialUserPrompt = _promptCatalogTypes.ChatSinglePassUserPrompt;
        var template = Template.Parse(initialUserPrompt);

        var result = template.Render(new
        {
            chatHistoryString,
            previousSummariesForConversationString,
            userMessage
            
        }, member => member.Name);

        return result;
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