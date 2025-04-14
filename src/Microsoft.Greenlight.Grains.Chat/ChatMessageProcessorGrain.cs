using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Grains.Chat.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Microsoft.SemanticKernel;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Greenlight.Grains.Chat;

/// <summary>
/// Grain for processing individual chat messages without blocking the conversation grain
/// </summary>
public class ChatMessageProcessorGrain : Grain, IChatMessageProcessorGrain
{
    private readonly IMapper _mapper;
    private readonly IPromptInfoService _promptInfoService;
    private readonly ILogger<ChatMessageProcessorGrain> _logger;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IContentReferenceService _contentReferenceService;
    private readonly IRagContextBuilder _ragContextBuilder;
    private readonly IKernelFactory _kernelFactory;

    public ChatMessageProcessorGrain(
        IMapper mapper,
        IPromptInfoService promptInfoService,
        ILogger<ChatMessageProcessorGrain> logger,
        IDocumentProcessInfoService documentProcessInfoService,
        IKernelFactory kernelFactory,
        IContentReferenceService contentReferenceService,
        IRagContextBuilder ragContextBuilder)
    {
        _mapper = mapper;
        _promptInfoService = promptInfoService;
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
        _kernelFactory = kernelFactory;
        _contentReferenceService = contentReferenceService;
        _ragContextBuilder = ragContextBuilder;
    }

    public async Task ProcessMessageAsync(
    ChatMessageDTO userMessageDto,
    Guid conversationId,
    string documentProcessName,
    string systemPrompt,
    List<Guid> referenceItemIds,
    List<ChatMessage> conversationMessages,
    List<ConversationSummary> conversationSummaries)
{
    try
    {
        _logger.LogInformation("Processing message {MessageId} for conversation {ConversationId}",
            userMessageDto.Id, conversationId);

        // Result object that will contain extracted references and the generated response
        var result = new ProcessMessageResult
        {
            ExtractedReferences = new List<ContentReferenceItem>(),
            UserMessageEntity = _mapper.Map<ChatMessage>(userMessageDto)
        };

        // Process user information
        if (userMessageDto.Source == ChatMessageSource.User && !string.IsNullOrEmpty(userMessageDto.UserId))
        {
            result.UserMessageEntity.AuthorUserInformation = new UserInformation
            {
                ProviderSubjectId = userMessageDto.UserId,
                FullName = userMessageDto.UserFullName
            };
        }
        else
        {
            result.UserMessageEntity.AuthorUserInformation = null;
        }

        // Extract references if it's a user message
        if (userMessageDto.Source == ChatMessageSource.User && !string.IsNullOrEmpty(userMessageDto.Message))
        {
            result.ExtractedReferences = await ExtractReferencesFromChatMessageAsync(userMessageDto);
        }

        // Generate the assistant's response
        result.AssistantMessageDto = await GenerateAssistantResponseAsync(
            userMessageDto,
            documentProcessName,
            systemPrompt,
            referenceItemIds,
            conversationMessages,
            conversationSummaries);

        // Create the assistant message entity
        result.AssistantMessageEntity = _mapper.Map<ChatMessage>(result.AssistantMessageDto);

        // Call back to the ConversationGrain
        var conversationGrain = GrainFactory.GetGrain<IConversationGrain>(conversationId);
        await conversationGrain.OnMessageProcessingComplete(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing message {Id} for conversation {ConversationId}",
            userMessageDto.Id, conversationId);
        throw;
    }
}


    private async Task<List<ContentReferenceItem>> ExtractReferencesFromChatMessageAsync(ChatMessageDTO messageDto)
    {
        if (messageDto.Source == ChatMessageSource.Assistant)
        {
            // Don't process references from assistant messages
            return [];
        }
        
        if (string.IsNullOrEmpty(messageDto.Message))
        {
            return [];
        }

        // Extract reference patterns from message
        var matches = Regex.Matches(messageDto.Message, @"#\(Reference:(\w+):([0-9a-fA-F-]+)\)");
        var processedReferences = new Dictionary<string, ContentReferenceItem>(); // Track by reference key

        if (matches.Count <= 0)
        {
            return [];
        }

        await SendStatusNotificationAsync(messageDto.Id, "Extracting references from message...", true);
        var contentReferences = new List<ContentReferenceItem>();

        foreach (Match match in matches)
        {
            if (Enum.TryParse(match.Groups[1].Value, out ContentReferenceType referenceType))
            {
                var referenceId = Guid.Parse(match.Groups[2].Value);
                string matchKey = match.Value;

                try
                {
                    // Check for duplicate in this message first (exact same reference)
                    if (processedReferences.ContainsKey(matchKey))
                    {
                        contentReferences.Add(processedReferences[matchKey]);
                        continue;
                    }

                    // For external files, notify user that analysis might take some time
                    if (referenceType == ContentReferenceType.ExternalFile)
                    {
                        var referenceItem =
                            await _contentReferenceService.GetCachedReferenceByIdAsync(referenceId,
                                ContentReferenceType.ExternalFile);
                        if (referenceItem != null)
                        {
                            await SendStatusNotificationAsync(messageDto.Id,
                                $"Processing file reference {referenceItem.DisplayName}", true);
                        }
                    }

                    // Get or create the reference
                    var reference =
                        await _contentReferenceService.GetOrCreateContentReferenceItemAsync(referenceId,
                            referenceType);
                    if (reference != null)
                    {
                        processedReferences[matchKey] = reference;
                        contentReferences.Add(reference);
                    }
                }
                catch (Exception ex)
                {
                    await SendStatusNotificationAsync(messageDto.Id, "Failed processing reference!", false);
                    _logger.LogError(ex, "Error processing reference {Id} of type {Type}", referenceId,
                        referenceType);
                }
            }
        }

        return contentReferences;
    }

    private async Task<ChatMessageDTO> GenerateAssistantResponseAsync(
        ChatMessageDTO userMessageDto,
        string documentProcessName,
        string systemPrompt,
        List<Guid> referenceItemIds,
        List<ChatMessage> conversationMessages,
        List<ConversationSummary> conversationSummaries)
    {
        var assistantMessageDto = new ChatMessageDTO
        {
            ConversationId = userMessageDto.ConversationId,
            Source = ChatMessageSource.Assistant,
            CreatedUtc = DateTime.UtcNow,
            ReplyToId = userMessageDto.Id,
            Id = Guid.NewGuid(),
            State = ChatMessageCreationState.InProgress,
            Message = ""
        };

        if (string.IsNullOrEmpty(userMessageDto.Message))
        {
            assistantMessageDto.State = ChatMessageCreationState.Complete;
            return assistantMessageDto;
        }

        // Get references for context
        var referenceItems = await _contentReferenceService.GetContentReferenceItemsFromIdsAsync(referenceItemIds);

        // Send notification with current set of references, if any
        if (referenceItems.Any())
        {
            var referenceItemDtOs = _mapper.Map<List<ContentReferenceItemInfo>>(referenceItems);
            await SendReferencesUpdatedNotificationAsync(userMessageDto.ConversationId, referenceItemDtOs);
        }

        await SendStatusNotificationAsync(userMessageDto.Id, "Adjusting reference context and processing references. Might take a while.", true);

        // Build context with selected references
        var contextString = await BuildContextWithSelectedReferencesAsync(userMessageDto.Message, referenceItems, 5);

        // Get document process info
        var documentProcessInfo = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);
        if (documentProcessInfo == null)
        {
            throw new InvalidOperationException($"Document process with short name {documentProcessName} not found");
        }

        // Initialize the Semantic Kernel
        var sk = await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcessName);
        var openAiSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(
            documentProcessInfo, AiTaskType.ChatReplies);

        #pragma warning disable SKEXP0010
        openAiSettings.ChatDeveloperPrompt = systemPrompt;
        #pragma warning restore SKEXP0010

        // Prepare chat history and summaries
        var chatHistoryString = CreateChatHistoryString(conversationMessages, 10);
        var previousSummariesString = GetSummariesString(conversationSummaries);

        // Build the user prompt
        var userPrompt = await BuildUserPromptAsync(
            chatHistoryString,
            previousSummariesString,
            userMessageDto.Message,
            documentProcessName,
            contextString);

        var kernelArguments = new KernelArguments(openAiSettings);

        var updateBlock = "";
        var responseDateSet = false;

        await SendStatusNotificationAsync(userMessageDto.Id, "Responding...", false, true);

        // Stream the response
        await foreach (var response in sk.InvokePromptStreamingAsync(userPrompt, kernelArguments))
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
                await SendResponseReceivedNotificationAsync(userMessageDto.ConversationId, assistantMessageDto, updateBlock);
                updateBlock = "";
            }
        }

        if (updateBlock.Length > 0)
        {
            assistantMessageDto.Message += updateBlock;
        }

        assistantMessageDto.State = ChatMessageCreationState.Complete;
        await SendResponseReceivedNotificationAsync(userMessageDto.ConversationId, assistantMessageDto, updateBlock);

        return assistantMessageDto;
    }

    private async Task<string> BuildContextWithSelectedReferencesAsync(string userQuery, List<ContentReferenceItem> allReferences, int topN)
    {
        // Delegate to the RAG context builder service
        return await _ragContextBuilder.BuildContextWithSelectedReferencesAsync(userQuery, allReferences, topN);
    }

    private async Task<string> BuildUserPromptAsync(string chatHistoryString, string previousSummariesForConversationString,
        string userMessage, string documentProcessName, string contextString)
    {
        var initialUserPrompt = await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(
            PromptNames.ChatSinglePassUserPrompt, documentProcessName);

        var template = Scriban.Template.Parse(initialUserPrompt);

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

    private string GetSummariesString(List<ConversationSummary> summaries)
    {
        var summariesString = "";
        foreach (var summary in summaries.OrderBy(x => x.CreatedAt))
        {
            summariesString += summary.SummaryText + "\n";
        }

        return summariesString;
    }

    private string CreateChatHistoryString(List<ChatMessage> messages, int numberOfMessagesToInclude = int.MaxValue)
    {
        // If there are no messages, return empty string
        if (messages.Count == 0)
        {
            return "";
        }

        List<ChatMessage> chatHistory;
        if (numberOfMessagesToInclude == int.MaxValue)
        {
            chatHistory = messages
                .OrderBy(x => x.CreatedUtc)
                .ToList();
        }
        else
        {
            chatHistory = messages
                .OrderByDescending(x => x.CreatedUtc)
                .Take(numberOfMessagesToInclude)
                .OrderBy(x => x.CreatedUtc)
                .ToList();
        }

        return CreateChatHistoryStringFromChatHistory(chatHistory);
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

        return chatHistoryBuilder.ToString();
    }

    private async Task SendStatusNotificationAsync(Guid messageId, string notificationMessage, bool persistent = false, bool processingComplete = false)
    {
        var notification = new ChatMessageStatusNotification(messageId, notificationMessage)
        {
            ProcessingComplete = processingComplete,
            Persistent = persistent
        };

        _logger.LogInformation("Sending status notification for message {MessageId}: {Message}", messageId, notificationMessage);

        var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
        await notifierGrain.NotifyChatMessageStatusAsync(notification);
    }

    // Replace SendResponseReceivedNotificationAsync with this:
    private async Task SendResponseReceivedNotificationAsync(Guid conversationId, ChatMessageDTO assistantMessageDto, string updateBlock)
    {
        var notification = new ChatMessageResponseReceived(conversationId, assistantMessageDto, updateBlock);

        var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
        await notifierGrain.NotifyChatMessageResponseReceivedAsync(notification);
    }

    // Replace SendReferencesUpdatedNotificationAsync with this:
    private async Task SendReferencesUpdatedNotificationAsync(Guid conversationId, List<ContentReferenceItemInfo> referenceItems)
    {
        var notification = new ConversationReferencesUpdatedNotification(conversationId, referenceItems);
        
        _logger.LogInformation("Sending references updated notification for conversation {MessageId}.", conversationId);
        
        var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
        await notifierGrain.NotifyConversationReferencesUpdatedAsync(notification);
    }
}