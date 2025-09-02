using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Grains.Chat.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Microsoft.SemanticKernel; // Needed for Kernel type
using Microsoft.SemanticKernel.ChatCompletion; // Added for IChatCompletionService, ChatHistory
using System.Text;
// using System.Text.Json; // removed: not used
using System.Text.RegularExpressions;

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

            if (result.ExtractedReferences.Any())
            {
                referenceItemIds.AddRange(result.ExtractedReferences.Select(item => item.Id));
            }

            // Handle system messages differently - they should be stored but not processed for response
            if (userMessageDto.Source == ChatMessageSource.System)
            {
                // For system messages, especially with ContentText, we just store them without generating a response
                _logger.LogInformation("Message {MessageId} is a system message, storing without generating response", userMessageDto.Id);

                // Create an empty "completion" message to satisfy the processing flow but with Complete state
                result.AssistantMessageEntity = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    CreatedUtc = DateTime.UtcNow,
                    Source = ChatMessageSource.System,
                    Message = "Context received", // Just a placeholder message
                    ReplyToChatMessageId = userMessageDto.Id
                };

                // Call back to the ConversationGrain
                var tempConversationGrain = GrainFactory.GetGrain<IConversationGrain>(conversationId);
                await tempConversationGrain.OnMessageProcessingComplete(result);
                return;
            }

            // Generate the assistant's response for non-system messages
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
        var processingTasks = new List<Task<(string MatchKey, ContentReferenceItem? Reference)>>();

        foreach (Match match in matches)
        {
            if (Enum.TryParse(match.Groups[1].Value, out ContentReferenceType referenceType))
            {
                var referenceId = Guid.Parse(match.Groups[2].Value);
                string matchKey = match.Value;

                // Check for duplicate in this message first (exact same reference)
                if (processedReferences.ContainsKey(matchKey))
                {
                    contentReferences.Add(processedReferences[matchKey]);
                    continue;
                }

                // Process each reference in parallel but with careful error handling
                processingTasks.Add(ProcessSingleReferenceAsync(messageDto.Id, referenceId, referenceType, matchKey));
            }
        }

        // Wait for all tasks to complete
        var results = await Task.WhenAll(processingTasks);

        // Process results
        foreach (var result in results)
        {
            if (result.Reference != null)
            {
                processedReferences[result.MatchKey] = result.Reference;
                contentReferences.Add(result.Reference);
            }
        }

        await SendStatusNotificationAsync(messageDto.Id, "All references processed", false, true);
        return contentReferences;
    }

    private async Task<(string MatchKey, ContentReferenceItem? Reference)> ProcessSingleReferenceAsync(
        Guid messageId,
        Guid referenceId,
        ContentReferenceType referenceType,
        string matchKey)
    {
        try
        {
            // For external files, notify user that analysis might take some time
            if (referenceType == ContentReferenceType.ExternalFile)
            {
                var referenceItem = await _contentReferenceService.GetCachedReferenceByIdAsync(referenceId, ContentReferenceType.ExternalFile);
                if (referenceItem != null)
                {
                    await SendStatusNotificationAsync(messageId, $"Processing file reference {referenceItem.DisplayName}", true);
                }
            }

            // Get or create the reference with timeout
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2)); // 2 minute timeout
            var getReferenceTask = _contentReferenceService.GetOrCreateContentReferenceItemAsync(referenceId, referenceType);

            var completedTask = await Task.WhenAny(getReferenceTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("Timeout occurred while processing reference {ReferenceId} of type {ReferenceType}",
                    referenceId, referenceType);
                await SendStatusNotificationAsync(messageId, $"Reference processing timed out, continuing without it", false);
                return (matchKey, null);
            }

            var reference = await getReferenceTask;

            if (reference != null)
            {
                return (matchKey, reference);
            }
        }
        catch (Exception ex)
        {
            await SendStatusNotificationAsync(messageId, "Failed processing reference!", false);
            _logger.LogError(ex, "Error processing reference {Id} of type {Type}", referenceId, referenceType);
        }

        return (matchKey, null);
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

        try
        {
            if (string.IsNullOrEmpty(userMessageDto.Message))
            {
                assistantMessageDto.State = ChatMessageCreationState.Complete;
                return assistantMessageDto;
            }

            // Get references for context
            var referenceItems = await _contentReferenceService.GetContentReferenceItemsFromIdsAsync(referenceItemIds);

            // If ContentText is provided, we're in content editing mode
            if (!string.IsNullOrEmpty(userMessageDto.ContentText))
            {
                // Create a special reference for content editing context
                var contentContextReference = new ContentReferenceItem
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Content Being Edited",
                    Description = "Current content node being edited",
                    ReferenceType = ContentReferenceType.GeneratedSection,
                    RagText = userMessageDto.ContentText
                };

                // Add this as the first reference for priority
                referenceItems.Insert(0, contentContextReference);

                // Important: Store the current content text to prevent circular references
                string contentForProcessing = userMessageDto.ContentText;

                // Send a brief message acknowledging the request
                assistantMessageDto.Message = "I'm analyzing your request and will provide specific updates to the content. Please wait while I process this...";
                assistantMessageDto.State = ChatMessageCreationState.Complete;

                // Send the response message first
                await SendResponseReceivedNotificationAsync(userMessageDto.ConversationId, assistantMessageDto, assistantMessageDto.Message);

                // Then delegate to the ContentChunkProcessorGrain to handle content updates
                try
                {
                    // Get the chunk processor grain
                    var contentChunkProcessorGrain = GrainFactory.GetGrain<IContentChunkProcessorGrain>(userMessageDto.Id);

                    // Start the content processing task (without await to avoid blocking)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await contentChunkProcessorGrain.ProcessContentUpdateAsync(
                                userMessageDto.ConversationId,
                                userMessageDto.Id,
                                contentForProcessing,  // Use the stored content
                                userMessageDto.Message,
                                documentProcessName,
                                systemPrompt);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in content chunk processing for message {MessageId}", userMessageDto.Id);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error delegating to ContentChunkProcessorGrain");
                    // We'll still return the initial acknowledgment message
                }

                return assistantMessageDto;
            }

            // Normal chat flow (non-content editing mode) continues here
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
            DocumentProcessInfo? documentProcessInfo = null;
            try
            {
                documentProcessInfo = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get document process with name {DocumentProcessName}, falling back to Default", documentProcessName);
                documentProcessInfo = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync("Default");

                if (documentProcessInfo == null)
                {
                    // Try to get any document process as a last resort
                    var documentProcesses = await _documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
                    if (documentProcesses.Any())
                    {
                        documentProcessInfo = documentProcesses.First();
                        documentProcessName = documentProcessInfo.ShortName;
                    }
                }
            }

            if (documentProcessInfo == null)
            {
                throw new InvalidOperationException($"No document process found. Tried {documentProcessName} and Default.");
            }

            // Initialize the Semantic Kernel with per-user context when available
            string? providerSubjectId = null;
            try
            {
                var convGrain = GrainFactory.GetGrain<IConversationGrain>(userMessageDto.ConversationId);
                var convState = await convGrain.GetStateAsync();
                providerSubjectId = convState?.StartedByProviderSubjectId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to retrieve conversation state for {ConversationId} to determine ProviderSubjectId", userMessageDto.ConversationId);
            }

            // Ensure ambient context is set during kernel creation so plugins created during enrichment see the user
            Kernel sk = await UserContextRunner.RunAsync(providerSubjectId, async () =>
            {
                return !string.IsNullOrWhiteSpace(providerSubjectId)
                    ? await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcessName, providerSubjectId)
                    : await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcessName);
            });
            var openAiSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(
                documentProcessInfo, AiTaskType.ChatReplies);

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

            // Use IChatCompletionService with ChatHistory and centralized manual tool invocation
            var chatService = sk.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(systemPrompt);
            history.AddUserMessage(userPrompt);

            var updateBlock = "";
            var responseDateSet = false;

            await SendStatusNotificationAsync(userMessageDto.Id, "Responding...", false, true);

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(7));

            await foreach (var text in SemanticKernelStreamingHelper.StreamChatWithManualToolInvocationAsync(
                chatService, history, openAiSettings, sk, cts.Token))
            {
                updateBlock += text;
                if (updateBlock.Length > 80)
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

            // Ensure the UI receives text even if no incremental updates were sent (e.g., total text < threshold)
            var finalBlock = responseDateSet ? updateBlock : assistantMessageDto.Message;
            await SendResponseReceivedNotificationAsync(userMessageDto.ConversationId, assistantMessageDto, finalBlock);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating assistant response");
            assistantMessageDto.State = ChatMessageCreationState.Failed;
            assistantMessageDto.Message = $"I'm sorry, I encountered an error while processing your message: {ex.Message}";
            await SendResponseReceivedNotificationAsync(userMessageDto.ConversationId, assistantMessageDto, assistantMessageDto.Message);
        }

        return assistantMessageDto;
    }



    private string ExtractContentSuggestion(string responseMessage)
    {
        // First clean any common marker patterns that might appear at the start
        string cleanedResponse = CleanMarkerPatterns(responseMessage);

        // First check for code blocks which often contain complete content
        var codeBlockRegex = new Regex(@"```(?:markdown|md|text|plaintext)?\s*\n([\s\S]*?)\n```", RegexOptions.Multiline);
        var codeBlockMatch = codeBlockRegex.Match(cleanedResponse);

        if (codeBlockMatch.Success && codeBlockMatch.Groups.Count > 1)
        {
            var extracted = codeBlockMatch.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(extracted))
            {
                return CleanMarkerPatterns(extracted);
            }
        }

        // If no code blocks, look for sections with common headers
        var contentMarkers = new[]
        {
        ("SUGGESTED CONTENT:", ""),
        ("REVISED CONTENT:", ""),
        ("UPDATED CONTENT:", ""),
        ("IMPROVED CONTENT:", ""),
        ("Here's the revised content:", ""),
        ("Here's my suggestion:", ""),
        ("[SUGGESTED CONTENT UPDATE]", ""),
        ("[SUGGESTED UPDATED CONTENT]", ""),
        ("[SUGGESTED REVISED CONTENT]", ""),
        ("[UPDATED CONTENT]", ""),
        ("[CONTENT UPDATE]", "")
    };

        foreach (var (startMarker, endMarker) in contentMarkers)
        {
            int startIndex = cleanedResponse.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
            if (startIndex >= 0)
            {
                startIndex += startMarker.Length;
                int endIndex = string.IsNullOrEmpty(endMarker)
                    ? cleanedResponse.Length
                    : cleanedResponse.IndexOf(endMarker, startIndex, StringComparison.OrdinalIgnoreCase);

                if (endIndex == -1) endIndex = cleanedResponse.Length;

                if (endIndex > startIndex)
                {
                    var extracted = cleanedResponse.Substring(startIndex, endIndex - startIndex).Trim();
                    if (!string.IsNullOrEmpty(extracted))
                    {
                        return CleanMarkerPatterns(extracted);
                    }
                }
            }
        }

        // Extract full content if no explicit markers are found but the message isn't too long
        // This is often the case when the AI responds with just the revised text
        var plainTextResponse = cleanedResponse.Trim();
        if (!string.IsNullOrEmpty(plainTextResponse) && plainTextResponse.Length < 256000)
        {
            return plainTextResponse;
        }

        // If no structured content found or the message is extremely long, extract the first section
        var lines = cleanedResponse.Split('\n');
        var meaningfulLines = lines.Where(l => !string.IsNullOrWhiteSpace(l) &&
            !l.StartsWith("I've reviewed") &&
            !l.StartsWith("Here are my suggestions") &&
            !l.StartsWith("Here's what I've") &&
            !l.Contains("hope this helps")).ToList();

        if (meaningfulLines.Count > 3)
        {
            return string.Join("\n", meaningfulLines);
        }

        // As a last resort, return the original message with markers cleaned
        return cleanedResponse;
    }

    private string CleanMarkerPatterns(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Remove common marker headers that might appear at the beginning
        var markerPatterns = new[]
        {
        @"^\s*\[?SUGGESTED\s+(?:CONTENT|UPDATE|UPDATED|CONTENT\s+UPDATE)\]?\s*:?\s*\n?",
        @"^\s*\[?REVISED\s+CONTENT\]?\s*:?\s*\n?",
        @"^\s*\[?UPDATED\s+CONTENT\]?\s*:?\s*\n?",
        @"^\s*\[?IMPROVED\s+CONTENT\]?\s*:?\s*\n?",
        @"^\s*Here\'s\s+(?:my\s+)?(?:the\s+)?(?:revised|suggested|updated)\s+content\s*:?\s*\n?"
    };

        // Apply each pattern 
        foreach (var pattern in markerPatterns)
        {
            text = Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        // Special case for "[SUGGESTED UPDATED CONTENT]" and variations which could be anywhere in the text
        text = Regex.Replace(text, @"\[SUGGESTED\s+(?:CONTENT|UPDATE|UPDATED|CONTENT\s+UPDATE)\]", "",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // Clean up any leftover whitespace from the removal
        return text.Trim();
    }

    private async Task<string> BuildContextWithSelectedReferencesAsync(string userQuery, List<ContentReferenceItem> allReferences, int topN)
    {
        // Delegate to the RAG context builder service
        return await _ragContextBuilder.BuildContextWithSelectedReferencesAsync(userQuery, allReferences, topN);
    }

    private async Task<string> BuildUserPromptAsync(
        string chatHistoryString,
        string previousSummariesForConversationString,
        string userMessage,
        string documentProcessName,
        string contextString)
    {
        var initialUserPrompt = await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(
            PromptNames.ChatSinglePassUserPrompt, documentProcessName);

        // Add special instruction for content editing when contextString contains CONTENT BEING EDITED
        if (contextString.Contains("CONTENT BEING EDITED"))
        {
            initialUserPrompt += "\n\n" +
                                 "When suggesting improvements to content, always provide the complete text with all changes applied. " +
                                 "Do not just describe the changes or provide partial content. The user expects to receive the entire " +
                                 "updated content that can be used as a direct replacement for the original. " +
                                 "Format your response so the suggested content is clearly marked and can be easily extracted.";
        }

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

    private async Task SendResponseReceivedNotificationAsync(Guid conversationId, ChatMessageDTO assistantMessageDto, string updateBlock)
    {
        var notification = new ChatMessageResponseReceived(conversationId, assistantMessageDto, updateBlock);

        var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
        await notifierGrain.NotifyChatMessageResponseReceivedAsync(notification);
    }

    private async Task SendReferencesUpdatedNotificationAsync(Guid conversationId, List<ContentReferenceItemInfo> referenceItems)
    {
        var notification = new ConversationReferencesUpdatedNotification(conversationId, referenceItems);

        _logger.LogInformation("Sending references updated notification for conversation {MessageId}.", conversationId);

        var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
        await notifierGrain.NotifyConversationReferencesUpdatedAsync(notification);
    }
}
