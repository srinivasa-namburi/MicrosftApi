using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Grains.Chat.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Contracts.Streams;
using Microsoft.Greenlight.Shared.Enums;
using Orleans.Streams;
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
    private readonly IContentReferenceVectorRepository _contentReferenceVectorRepository;
    private readonly IRagContextBuilder _ragContextBuilder;
    private readonly IKernelFactory _kernelFactory;

    public ChatMessageProcessorGrain(
        IMapper mapper,
        IPromptInfoService promptInfoService,
        ILogger<ChatMessageProcessorGrain> logger,
        IDocumentProcessInfoService documentProcessInfoService,
        IKernelFactory kernelFactory,
        IContentReferenceService contentReferenceService,
        IContentReferenceVectorRepository contentReferenceVectorRepository,
        IRagContextBuilder ragContextBuilder)
    {
        _mapper = mapper;
        _promptInfoService = promptInfoService;
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
        _kernelFactory = kernelFactory;
        _contentReferenceService = contentReferenceService;
        _contentReferenceVectorRepository = contentReferenceVectorRepository;
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

            // Check if this is a Flow-managed conversation
            var isFlowConversation = await IsFlowConversationAsync(conversationId);

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
                result.ExtractedReferences = await ExtractReferencesFromChatMessageAsync(userMessageDto, isFlowConversation, conversationId, documentProcessName);
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
                conversationSummaries,
                isFlowConversation,
                conversationId);

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



    private async Task<List<ContentReferenceItem>> ExtractReferencesFromChatMessageAsync(ChatMessageDTO messageDto, bool isFlowConversation = false, Guid? conversationId = null, string? documentProcessName = null)
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

        _logger.LogInformation("Extracting references from message {MessageId}. Message content: {MessagePreview}", 
            messageDto.Id, 
            messageDto.Message.Length > 200 ? messageDto.Message.Substring(0, 200) + "..." : messageDto.Message);

        // Extract reference patterns from message
        // Support both "#(Reference:Type:Id)" and "#Reference:Type:Id" formats
        var matches = Regex.Matches(messageDto.Message, @"#\(?Reference:(\w+):([0-9a-fA-F-]+)\)?");
        var processedReferences = new Dictionary<string, ContentReferenceItem>(); // Track by reference key

        _logger.LogInformation("Found {MatchCount} reference pattern matches in message {MessageId}", 
            matches.Count, messageDto.Id);

        if (matches.Count <= 0)
        {
            return [];
        }

        await SendStatusNotificationAsync(messageDto.Id, "Extracting references from message...", true, false, isFlowConversation, conversationId, documentProcessName);
        var contentReferences = new List<ContentReferenceItem>();
        var processingTasks = new List<Task<(string MatchKey, ContentReferenceItem? Reference)>>();

        foreach (Match match in matches)
        {
            _logger.LogInformation("Processing reference match: {MatchValue}, Type: {TypeValue}, Id: {IdValue}", 
                match.Value, match.Groups[1].Value, match.Groups[2].Value);

            if (Enum.TryParse(match.Groups[1].Value, out ContentReferenceType referenceType))
            {
                var referenceId = Guid.Parse(match.Groups[2].Value);
                string matchKey = match.Value;

                _logger.LogInformation("Successfully parsed reference: Type={ReferenceType}, Id={ReferenceId}", 
                    referenceType, referenceId);

                // Check for duplicate in this message first (exact same reference)
                if (processedReferences.ContainsKey(matchKey))
                {
                    _logger.LogInformation("Duplicate reference found, reusing: {MatchKey}", matchKey);
                    contentReferences.Add(processedReferences[matchKey]);
                    continue;
                }

                // Process each reference in parallel but with careful error handling
                processingTasks.Add(ProcessSingleReferenceAsync(messageDto.Id, referenceId, referenceType, matchKey, isFlowConversation, conversationId, documentProcessName));
            }
            else
            {
                _logger.LogWarning("Failed to parse ContentReferenceType from value: {TypeValue}", match.Groups[1].Value);
            }
        }

        // Wait for all tasks to complete
        var results = await Task.WhenAll(processingTasks);

        _logger.LogInformation("Completed processing {TaskCount} reference tasks", results.Length);

        // Process results
        foreach (var result in results)
        {
            if (result.Reference != null)
            {
                _logger.LogInformation("Adding reference to results: {ReferenceId} - {DisplayName}", 
                    result.Reference.Id, result.Reference.DisplayName);
                processedReferences[result.MatchKey] = result.Reference;
                contentReferences.Add(result.Reference);
            }
            else
            {
                _logger.LogWarning("Reference task returned null for key: {MatchKey}", result.MatchKey);
            }
        }

        _logger.LogInformation("Extracted {ReferenceCount} references from message {MessageId}", 
            contentReferences.Count, messageDto.Id);

        await SendStatusNotificationAsync(messageDto.Id, "All references processed", false, true, isFlowConversation, conversationId, documentProcessName);
        return contentReferences;
    }

    private async Task<(string MatchKey, ContentReferenceItem? Reference)> ProcessSingleReferenceAsync(
        Guid messageId,
        Guid referenceId,
        ContentReferenceType referenceType,
        string matchKey,
        bool isFlowConversation = false,
        Guid? conversationId = null,
        string? documentProcessName = null)
    {
        try
        {
            // For file-based references (ExternalFile or ExternalLinkAsset), notify user that analysis might take some time
            if (referenceType == ContentReferenceType.ExternalFile || referenceType == ContentReferenceType.ExternalLinkAsset)
            {
                var referenceItem = await _contentReferenceService.GetReferenceByIdAsync(referenceId, referenceType);
                if (referenceItem != null)
                {
                    await SendStatusNotificationAsync(messageId, $"Processing file reference {referenceItem.DisplayName}", true, false, isFlowConversation, conversationId, documentProcessName);
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
                await SendStatusNotificationAsync(messageId, $"Reference processing timed out, continuing without it", false, false, isFlowConversation, conversationId, documentProcessName);
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
            await SendStatusNotificationAsync(messageId, "Failed processing reference!", false, false, isFlowConversation, conversationId, documentProcessName);
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
        List<ConversationSummary> conversationSummaries,
        bool isFlowConversation = false,
        Guid? conversationId = null)
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

            // Opportunistically index selected references into SK vector store (non-blocking best-effort)
            if (referenceItems.Any())
            {
                _ = Task.Run(async () =>
                {
                    foreach (var r in referenceItems)
                    {
                        try { await _contentReferenceVectorRepository.IndexAsync(r); }
                        catch (Exception ex) { _logger.LogDebug(ex, "Indexing content reference {Id} failed (best-effort)", r.Id); }
                    }
                });
            }

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

            await SendStatusNotificationAsync(userMessageDto.Id, "Adjusting reference context and processing references. Might take a while.", true, false, isFlowConversation, conversationId, documentProcessName);

            // Build context with selected references
            var contextString = await BuildContextWithSelectedReferencesAsync(userMessageDto.Message, referenceItems, 5);

            // Resolve document process name & info robustly
            DocumentProcessInfo? documentProcessInfo = null;

            // If no process was provided, try fetching it from the conversation state
            if (string.IsNullOrWhiteSpace(documentProcessName))
            {
                try
                {
                    var convGrain = GrainFactory.GetGrain<IConversationGrain>(userMessageDto.ConversationId);
                    var convState = await convGrain.GetStateAsync();
                    if (!string.IsNullOrWhiteSpace(convState?.DocumentProcessName))
                    {
                        documentProcessName = convState!.DocumentProcessName!;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Unable to resolve document process from conversation state for {ConversationId}", userMessageDto.ConversationId);
                }
            }

            // Attempt resolution by short name (preferred)
            if (!string.IsNullOrWhiteSpace(documentProcessName))
            {
                try
                {
                    documentProcessInfo = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error resolving document process by short name {DocumentProcessName}", documentProcessName);
                }

                // If not found and looks like a GUID, try by id
                if (documentProcessInfo == null && Guid.TryParse(documentProcessName, out var processId))
                {
                    try
                    {
                        documentProcessInfo = await _documentProcessInfoService.GetDocumentProcessInfoByIdAsync(processId);
                        if (documentProcessInfo != null)
                        {
                            documentProcessName = documentProcessInfo.ShortName; // normalize to short name
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error resolving document process by id {DocumentProcessId}", processId);
                    }
                }
            }

            // Fallback to Default if still unresolved
            if (documentProcessInfo == null)
            {
                try
                {
                    var defaultInfo = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync("Default");
                    if (defaultInfo != null)
                    {
                        documentProcessInfo = defaultInfo;
                        documentProcessName = defaultInfo.ShortName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error resolving Default document process");
                }
            }

            // Absolute last resort: pick any available process
            if (documentProcessInfo == null)
            {
                var documentProcesses = await _documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
                if (documentProcesses.Any())
                {
                    documentProcessInfo = documentProcesses.First();
                    documentProcessName = documentProcessInfo.ShortName;
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

            await SendStatusNotificationAsync(userMessageDto.Id, "Responding...", false, true, isFlowConversation, conversationId, documentProcessName);

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

                    // Publish Flow backend conversation update stream event during streaming
                    await PublishFlowBackendConversationUpdateAsync(userMessageDto.ConversationId, assistantMessageDto, updateBlock);

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

            // Note: SendResponseReceivedNotificationAsync already calls PublishFlowBackendConversationUpdateAsync
            // so we don't need to call it again here - that was causing duplicate completion notifications
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

    private async Task SendStatusNotificationAsync(Guid messageId, string notificationMessage, bool persistent = false, bool processingComplete = false, bool isFlowConversation = false, Guid? conversationId = null, string? documentProcessName = null)
    {
        // For Flow conversations, route status messages through Orleans streams for intelligent synthesis
        if (isFlowConversation)
        {
            _logger.LogDebug("Routing status notification to Flow for conversation. Message: {Message}", notificationMessage);

            try
            {
                // Get Flow session IDs for this conversation
                var actualConversationId = conversationId ?? messageId; // Use messageId as fallback if conversationId not provided
                var registry = GrainFactory.GetGrain<IFlowBackendConversationRegistryGrain>(Guid.Empty);
                var flowSessions = await registry.GetFlowSessionsAsync(actualConversationId);

                if (flowSessions != null && flowSessions.Count > 0)
                {
                    var streamProvider = this.GetStreamProvider("StreamProvider");
                    var actualDocumentProcessName = documentProcessName ?? "chat-processor"; // Use provided or default

                    foreach (var flowSessionId in flowSessions)
                    {
                        // Send status update to each Flow session
                        var statusUpdate = new FlowBackendStatusUpdate(
                            Guid.NewGuid(),
                            flowSessionId,
                            actualConversationId,
                            messageId,
                            notificationMessage,
                            processingComplete,
                            persistent,
                            actualDocumentProcessName,
                            DateTime.UtcNow);

                        var stream = streamProvider.GetStream<FlowBackendStatusUpdate>(
                            ChatStreamNameSpaces.FlowBackendStatusUpdateNamespace,
                            flowSessionId);

                        await stream.OnNextAsync(statusUpdate);

                        _logger.LogDebug("Sent status update to Flow session {FlowSessionId}", flowSessionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error routing status notification to Flow");
            }

            return;
        }

        // For non-Flow conversations, send status notifications normally
        var notification = new ChatMessageStatusNotification(messageId, notificationMessage)
        {
            ProcessingComplete = processingComplete,
            Persistent = persistent
        };

        _logger.LogInformation("Sending status notification for message {MessageId}: {Message}", messageId, notificationMessage);

        var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
        await notifierGrain.NotifyChatMessageStatusAsync(notification);
    }

    private async Task<bool> IsFlowConversationAsync(Guid conversationId)
    {
        try
        {
            var registry = GrainFactory.GetGrain<IFlowBackendConversationRegistryGrain>(Guid.Empty);
            var flowSessions = await registry.GetFlowSessionsAsync(conversationId);
            return flowSessions != null && flowSessions.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if conversation {ConversationId} is Flow-managed", conversationId);
            return false;
        }
    }

    private async Task SendResponseReceivedNotificationAsync(Guid conversationId, ChatMessageDTO assistantMessageDto, string updateBlock)
    {
        var notification = new ChatMessageResponseReceived(conversationId, assistantMessageDto, updateBlock);

        var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
        await notifierGrain.NotifyChatMessageResponseReceivedAsync(notification);

        // Also publish Flow backend conversation updates for real-time Flow orchestration
        // This handles both streaming updates AND the final complete notification
        await PublishFlowBackendConversationUpdateAsync(conversationId, assistantMessageDto, updateBlock);
    }

    private async Task SendReferencesUpdatedNotificationAsync(Guid conversationId, List<ContentReferenceItemInfo> referenceItems)
    {
        var notification = new ConversationReferencesUpdatedNotification(conversationId, referenceItems);

        _logger.LogInformation("Sending references updated notification for conversation {MessageId}.", conversationId);

        var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
        await notifierGrain.NotifyConversationReferencesUpdatedAsync(notification);
    }

    /// <summary>
    /// Publishes Flow backend conversation updates to Orleans streams for real-time Flow orchestration.
    /// This allows Flow grains to monitor backend conversation progress without polling.
    /// Stream events are published with a 1-minute expiry to avoid buildup of unprocessed events.
    /// </summary>
    private async Task PublishFlowBackendConversationUpdateAsync(Guid conversationId, ChatMessageDTO assistantMessageDto, string updateBlock)
    {
        try
        {
            // First check if this is a Flow-managed conversation
            var registry = GrainFactory.GetGrain<IFlowBackendConversationRegistryGrain>(Guid.Empty);
            var flowSessions = await registry.GetFlowSessionsAsync(conversationId);

            if (flowSessions == null || flowSessions.Count == 0)
            {
                // Not a Flow conversation - skip publishing stream events entirely
                return;
            }

            _logger.LogInformation("Publishing Flow update for conversation {ConversationId} to {FlowSessionCount} Flow sessions",
                conversationId, flowSessions.Count);

            var documentProcessName = await GetDocumentProcessNameForConversationAsync(conversationId);
            var streamProvider = this.GetStreamProvider("StreamProvider");

            foreach (var flowSessionId in flowSessions)
            {
                var evt = new FlowBackendConversationUpdate(
                    CorrelationId: Guid.NewGuid(),
                    FlowSessionId: flowSessionId,
                    BackendConversationId: conversationId,
                    ChatMessageDto: assistantMessageDto,
                    DocumentProcessName: documentProcessName,
                    IsComplete: assistantMessageDto.State == ChatMessageCreationState.Complete);

                _logger.LogDebug("Publishing stream event to Flow session {FlowSessionId}, IsComplete: {IsComplete}",
                    flowSessionId, evt.IsComplete);

                var stream = streamProvider.GetStream<FlowBackendConversationUpdate>(
                    "FlowBackendConversationUpdate",
                    flowSessionId);

                await stream.OnNextAsync(evt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error publishing Flow backend conversation update for conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Attempts to retrieve the document process name for a conversation.
    /// </summary>
    private async Task<string> GetDocumentProcessNameForConversationAsync(Guid conversationId)
    {
        try
        {
            var conversationGrain = GrainFactory.GetGrain<IConversationGrain>(conversationId);
            var conversationState = await conversationGrain.GetStateAsync();
            return conversationState?.DocumentProcessName ?? "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not retrieve document process name for conversation {ConversationId}", conversationId);
            return "Unknown";
        }
    }
}
