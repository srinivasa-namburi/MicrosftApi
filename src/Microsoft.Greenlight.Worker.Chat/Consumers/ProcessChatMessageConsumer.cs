using AutoMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Microsoft.SemanticKernel;
using Scriban;
using System.Text;
using System.Text.RegularExpressions;

#pragma warning disable SKEXP0010
namespace Microsoft.Greenlight.Worker.Chat.Consumers;

public class ProcessChatMessageConsumer : IConsumer<ProcessChatMessage>
{
    private Kernel? _sk;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IPromptInfoService _promptInfoService;
    private readonly ILogger<ProcessChatMessageConsumer> _logger;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IContentReferenceService _contentReferenceService;
    private readonly IRagContextBuilder _ragContextBuilder;
    private readonly IKernelFactory _kernelFactory;

    public ProcessChatMessageConsumer(DocGenerationDbContext dbContext,
        IMapper mapper,
        IServiceProvider sp,
        IPromptInfoService promptInfoService,
        ILogger<ProcessChatMessageConsumer> logger,
        IDocumentProcessInfoService documentProcessInfoService,
        IKernelFactory kernelFactory,
        IContentReferenceService contentReferenceService, 
        IRagContextBuilder ragContextBuilder)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _promptInfoService = promptInfoService;
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
        _kernelFactory = kernelFactory;
        _contentReferenceService = contentReferenceService;
        _ragContextBuilder = ragContextBuilder;
    }

    public async Task Consume(ConsumeContext<ProcessChatMessage> context)
    {
        var userMessageDto = context.Message.ChatMessageDto;

        try
        {
            await StoreChatMessageAndReferenceItems(userMessageDto);
        }
        catch (OperationCanceledException e)
        {
            _logger.LogWarning(e, "Duplicate message detected");
            return;
        }
        await ProcessUserMessage(userMessageDto, context);
    }

    private async Task StoreChatMessageAndReferenceItems(ChatMessageDTO chatMessageDto)
    {
        // Store the chat message
        var chatMessageEntity = _mapper.Map<ChatMessage>(chatMessageDto);

        if (chatMessageEntity.Source == ChatMessageSource.User)
        {
            var userInformation = await _dbContext.UserInformations.FirstOrDefaultAsync(x => x.ProviderSubjectId == chatMessageDto.UserId);
            if (userInformation != null)
            {
                chatMessageEntity.AuthorUserInformationId = userInformation.Id;
            }
        }

        var existingMessage = await _dbContext.ChatMessages.FirstOrDefaultAsync(x => x.Id == chatMessageEntity.Id);
        if (existingMessage != null)
        {
            _dbContext.Entry(existingMessage).CurrentValues.SetValues(chatMessageEntity);
        }
        else
        {
            await _dbContext.ChatMessages.AddAsync(chatMessageEntity);
        }

        await _dbContext.SaveChangesAsync();

        // Extract references from message and add to conversation - only for user messages
        if (chatMessageDto.Source != ChatMessageSource.User)
        {
            return;
        }

        var extractedReferences = await ExtractReferencesFromChatMessageAsync(chatMessageDto);

        if (extractedReferences.Any())
        {
            var conversation = await _dbContext.ChatConversations
                .FirstOrDefaultAsync(x => x.Id == chatMessageDto.ConversationId);

            if (conversation != null)
            {
                bool conversationModified = false;
                foreach (var reference in extractedReferences)
                {
                    if (!conversation.ReferenceItemIds.Contains(reference.Id))
                    {
                        conversation.ReferenceItemIds.Add(reference.Id);
                        conversationModified = true;
                    }
                }

                if (conversationModified)
                {
                    _dbContext.ChatConversations.Update(conversation);
                    await _dbContext.SaveChangesAsync();
                }
            }
        }
    }


    private async Task ProcessUserMessage(ChatMessageDTO userMessageDto, ConsumeContext<ProcessChatMessage> context)
    {
        var assistantMessageDto = new ChatMessageDTO()
        {
            ConversationId = userMessageDto.ConversationId,
            Source = ChatMessageSource.Assistant,
            CreatedUtc = DateTime.UtcNow,
            ReplyToId = userMessageDto.Id,
            Id = Guid.NewGuid(),
            State = ChatMessageCreationState.InProgress
        };

        var conversation = await _dbContext.ChatConversations
            .FirstOrDefaultAsync(x => x.Id == userMessageDto.ConversationId);

        var documentProcessInfo = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(conversation!.DocumentProcessName);

        if (documentProcessInfo == null)
        {
            throw new InvalidOperationException($"Document process with short name {conversation.DocumentProcessName} not found");
        }

        // Get all references for context
        var referenceItems = await _contentReferenceService.GetContentReferenceItemsFromIdsAsync(conversation.ReferenceItemIds);

        // Send notification with current set of references, if any
        if (referenceItems.Any())
        {
            var referenceItemDtOs = _mapper.Map<List<ContentReferenceItemInfo>>(referenceItems);

            await context.Publish(new ConversationReferencesUpdatedNotification(conversation.Id, referenceItemDtOs));
        }

        if (userMessageDto.Message != null)
        {
            var contextString = await BuildContextWithSelectedReferencesAsync(userMessageDto.Message, referenceItems, 5);

            _sk = await _kernelFactory.GetKernelForDocumentProcessAsync(conversation.DocumentProcessName);

            var openAiSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(
                documentProcessInfo, AiTaskType.ChatReplies);

            string? systemPrompt = string.IsNullOrEmpty(conversation.SystemPrompt)
                ? await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(
                    PromptNames.ChatSystemPrompt, conversation.DocumentProcessName)
                : conversation.SystemPrompt;

            openAiSettings.ChatDeveloperPrompt = systemPrompt;

            var chatHistoryString = await CreateChatHistoryString(context, userMessageDto, 10);
            var previousSummariesForConversationString = await GetSummariesConversationString(userMessageDto);

            var userPrompt = await BuildUserPromptAsync(
                chatHistoryString,
                previousSummariesForConversationString,
                userMessageDto.Message!,
                conversation.DocumentProcessName,
                contextString);

            var kernelArguments = new KernelArguments(openAiSettings);

            var updateBlock = "";
            var responseDateSet = false;

            await foreach (var response in _sk.InvokePromptStreamingAsync(userPrompt, kernelArguments))
            {
                updateBlock += response;
                if (updateBlock.Length > 20)
                {
                    if (!responseDateSet)
                    {
                        assistantMessageDto.CreatedUtc = DateTime.UtcNow;
                        assistantMessageDto.State = ChatMessageCreationState.InProgress;
                        responseDateSet = true;
                    }

                    assistantMessageDto.Message += updateBlock;
                    await context.Publish<ChatMessageResponseReceived>(
                        new ChatMessageResponseReceived(userMessageDto.ConversationId, assistantMessageDto, updateBlock));
                    updateBlock = "";
                }
            }

            if (updateBlock.Length > 0)
            {
                assistantMessageDto.Message += updateBlock;
            }

            assistantMessageDto.State = ChatMessageCreationState.Complete;

            await context.Publish<ChatMessageResponseReceived>(new ChatMessageResponseReceived(userMessageDto.ConversationId, assistantMessageDto, updateBlock));
        }

        await StoreChatMessageAndReferenceItems(assistantMessageDto);
    }

    private async Task<List<ContentReferenceItem>> ExtractReferencesFromChatMessageAsync(ChatMessageDTO messageDto)
    {
        var contentReferences = new List<ContentReferenceItem>();

        if (string.IsNullOrEmpty(messageDto.Message))
        {
            return contentReferences;
        }

        // Extract reference patterns from message
        var matches = Regex.Matches(messageDto.Message, @"#\(Reference:(\w+):([0-9a-fA-F-]+)\)");
        foreach (Match match in matches)
        {
            if (Enum.TryParse(match.Groups[1].Value, out ContentReferenceType referenceType))
            {
                var referenceId = Guid.Parse(match.Groups[2].Value);

                try
                {
                    // Use the enhanced service to get or create the reference with RAG text 
                    var reference = await _contentReferenceService.GetOrCreateContentReferenceItemAsync(referenceId, referenceType);
                    if (reference != null)
                    {
                        contentReferences.Add(reference);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing reference {Id} of type {Type}", referenceId, referenceType);
                }
            }
        }

        // This is used to clean the message of reference tags. To test some new functionality, we're keeping these in place.
        // messageDto.Message = Regex.Replace(messageDto.Message, @"#\(Reference:(\w+):([0-9a-fA-F-]+)\)", "");

        return contentReferences;
    }

    /// <summary>
    /// Builds a context string with selected references for the user query.
    /// This performs basically "internal rag" using embeddings and chunking.
    /// </summary>
    /// <param name="userQuery"></param>
    /// <param name="allReferences"></param>
    /// <param name="topN"></param>
    /// <returns></returns>
    private async Task<string> BuildContextWithSelectedReferencesAsync(string userQuery, List<ContentReferenceItem> allReferences, int topN)
    {
        // Delegate to the specialized service
        var context = await _ragContextBuilder.BuildContextWithSelectedReferencesAsync(userQuery, allReferences, topN);
        return context;
    }


    private async Task<string> BuildUserPromptAsync(string chatHistoryString, string previousSummariesForConversationString, string userMessage, string documentProcessName, string contextString)
    {
        var initialUserPrompt = await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(PromptNames.ChatSinglePassUserPrompt, documentProcessName);
        var template = Template.Parse(initialUserPrompt);

        var result = await template.RenderAsync(new
        {
            chatHistoryString,
            previousSummariesForConversationString,
            userMessage,
            documentProcessName,
            contextString

        }, member => member.Name);

        return result;
    }

    private async Task<string> GetSummariesConversationString(ChatMessageDTO userMessageDto)
    {
        var summaries = await _dbContext.ConversationSummaries
            .Where(x => x.ConversationId == userMessageDto.ConversationId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        var summariesString = "";
        foreach (var summary in summaries)
        {
            summariesString += summary.SummaryText + "\n";
        }

        return summariesString;
    }

    private async Task<string> CreateChatHistoryString(ConsumeContext<ProcessChatMessage> context,
        ChatMessageDTO userMessageDto, int numberOfMessagesToInclude = Int32.MaxValue)
    {
        List<ChatMessage> chatHistory = [];

        if (numberOfMessagesToInclude == Int32.MaxValue)
        {
            chatHistory = await _dbContext.ChatMessages
                .Where(x => x.ConversationId == userMessageDto.ConversationId && x.Id != userMessageDto.Id)
                .OrderBy(x => x.CreatedUtc)
                .ToListAsync();
        }
        else
        {
            chatHistory = await _dbContext.ChatMessages
                .Where(x => x.ConversationId == userMessageDto.ConversationId && x.Id != userMessageDto.Id)
                .OrderByDescending(x => x.CreatedUtc)
                .Take(numberOfMessagesToInclude)
                .OrderBy(x => x.CreatedUtc)
                .ToListAsync();
        }

        if (chatHistory.Count == 0)
        {
            return "";
        }

        var fullChatHistoryCount = _dbContext.ChatMessages.Count(x => x.ConversationId == userMessageDto.ConversationId);

        var chatHistoryString = CreateChatHistoryStringFromChatHistory(chatHistory);

        var fiveEarliestMessages = chatHistory.OrderBy(x => x.CreatedUtc).Take(5).ToList();
        var earliestMessage = fiveEarliestMessages.Last();

        if (fullChatHistoryCount > numberOfMessagesToInclude)
        {
            await context.Publish(new GenerateChatHistorySummary(userMessageDto.ConversationId, earliestMessage.CreatedUtc));
        }

        return chatHistoryString;
    }

    private static string CreateChatHistoryStringFromChatHistory(List<ChatMessage> chatHistory)
    {
        var chatHistoryBuilder = new StringBuilder();

        foreach (var chatMessage in chatHistory)
        {
            chatHistoryBuilder
                .AppendLine("role:" + chatMessage.Source.ToString())
                .AppendLine("content:" + chatMessage.Message);
        }

        var chatHistoryString = chatHistoryBuilder.ToString();
        return chatHistoryString;
    }
}
