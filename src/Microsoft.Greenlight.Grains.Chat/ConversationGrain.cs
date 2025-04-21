using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Grains.Chat.Contracts.Models;
using Microsoft.Greenlight.Grains.Chat.Contracts.State;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Orleans.Concurrency;
using System.Text;

#pragma warning disable SKEXP0010
namespace Microsoft.Greenlight.Grains.Chat;

[Reentrant]
public class ConversationGrain : Grain, IConversationGrain
{
    private readonly IPersistentState<ConversationState> _state;
    private readonly IMapper _mapper;
    private readonly IPromptInfoService _promptInfoService;
    private readonly ILogger<ConversationGrain> _logger;
    private readonly IKernelFactory _kernelFactory;
    private Kernel _sk;

    // Semaphore lock for concurrent access in a reentrant grain
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    public ConversationGrain(
        [PersistentState("conversation")]
        IPersistentState<ConversationState> state,
        IMapper mapper,
        IPromptInfoService promptInfoService,
        ILogger<ConversationGrain> logger,
        IKernelFactory kernelFactory)
    {
        _state = state;
        _mapper = mapper;
        _promptInfoService = promptInfoService;
        _logger = logger;

        _kernelFactory = kernelFactory;


    }

    private async Task SafeWriteStateAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            await _state.WriteStateAsync(cancellationToken);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Set default values if this is a new grain
        if (_state.State.Id == Guid.Empty)
        {
            _state.State.Id = this.GetPrimaryKey();
            _state.State.CreatedUtc = DateTime.UtcNow;
            _state.State.ModifiedUtc = DateTime.UtcNow;
            await SafeWriteStateAsync(cancellationToken);
        }

        await base.OnActivateAsync(cancellationToken);
    }

    public async Task<ConversationState> GetStateAsync()
    {
        return _state.State;
    }

    public async Task InitializeAsync(string documentProcessName, string systemPrompt)
    {
        _state.State.DocumentProcessName = documentProcessName;
        _state.State.SystemPrompt = systemPrompt;

        if (string.IsNullOrEmpty(_state.State.SystemPrompt))
        {
            var systemPromptText = await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(
                PromptNames.ChatSystemPrompt, documentProcessName);
            _state.State.SystemPrompt = systemPromptText;
        }

        await SafeWriteStateAsync();
    }

    /// <summary>
    /// Remove a conversation reference from the conversation
    /// </summary>
    /// <param name="conversationReferenceId"></param>
    /// <returns></returns>
    public async Task<bool> RemoveConversationReference(Guid conversationReferenceId)
    {
        if (!_state.State.ReferenceItemIds.Remove(conversationReferenceId))
        {
            return false;
        }

        await SafeWriteStateAsync();
        return true;
    }

    public async Task<List<ChatMessageDTO>> GetMessagesAsync()
    {
        return _state.State.Messages
            .Select(m =>
            {
                var dto = _mapper.Map<ChatMessageDTO>(m);
                if (m.AuthorUserInformation != null)
                {
                    dto.UserId = m.AuthorUserInformation.ProviderSubjectId;
                    dto.UserFullName = m.AuthorUserInformation.FullName;
                }
                return dto;
            })
            .OrderBy(x => x.CreatedUtc)
            .ToList();
    }

    public async Task<ChatMessageDTO> ProcessMessageAsync(ChatMessageDTO userMessageDto)
    {
        try
        {
            _logger.LogInformation("Processing message {MessageId} for conversation {ConversationId}",
                userMessageDto.Id, userMessageDto.ConversationId);

            // 1. Store the user message first (important - this establishes the message in the conversation)
            var chatMessageEntity = _mapper.Map<ChatMessage>(userMessageDto);

            if (userMessageDto.Source == ChatMessageSource.User && !string.IsNullOrEmpty(userMessageDto.UserId))
            {
                chatMessageEntity.AuthorUserInformation = new UserInformation
                {
                    ProviderSubjectId = userMessageDto.UserId,
                    FullName = userMessageDto.UserFullName
                };
            }
            else
            {
                chatMessageEntity.AuthorUserInformation = null;
            }

            _state.State.Messages.Add(chatMessageEntity);
            _state.State.ModifiedUtc = DateTime.UtcNow;
            await SafeWriteStateAsync();

            // 2. Fire-and-forget: Dispatch message processing to the ChatMessageProcessorGrain
            var messageProcessorGrain = GrainFactory.GetGrain<IChatMessageProcessorGrain>(userMessageDto.Id);
            _= messageProcessorGrain.ProcessMessageAsync(
                userMessageDto,
                this.GetPrimaryKey(), // Pass the conversation ID
                _state.State.DocumentProcessName, // Pass the document process name
                _state.State.SystemPrompt, // Pass the system promptø
                _state.State.ReferenceItemIds, // Pass the reference item IDs
                _state.State.Messages, // Pass the conversation messages
                _state.State.Summaries // Pass the conversation summaries
            );

            // 3. Return the user message DTO immediately
            return userMessageDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {Id} for conversation {ConversationId}",
                userMessageDto.Id, userMessageDto.ConversationId);
            throw;
        }
    }

    // New method to handle the callback from ChatMessageProcessorGrain
    public async Task OnMessageProcessingComplete(ProcessMessageResult result)
    {
        try
        {
            _logger.LogInformation("Message processing complete for {MessageId} in conversation {ConversationId}",
                result.UserMessageEntity.Id, _state.State.Id);

            // Add any new references to the conversation
            bool conversationModified = false;
            foreach (var reference in result.ExtractedReferences)
            {
                if (!_state.State.ReferenceItemIds.Contains(reference.Id))
                {
                    _state.State.ReferenceItemIds.Add(reference.Id);
                    conversationModified = true;
                }
            }

            // Store the assistant message
            _state.State.Messages.Add(result.AssistantMessageEntity);
            _state.State.ModifiedUtc = DateTime.UtcNow;

            if (conversationModified || result.AssistantMessageEntity != null)
            {
                await SafeWriteStateAsync();
            }

            // Trigger summary generation if needed
            var messagesCount = _state.State.Messages.Count;
            if (messagesCount > 10 && messagesCount % 5 == 0)
            {
                _ = GenerateSummaryAsync(DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message processing completion for conversation {ConversationId}", _state.State.Id);
        }
    }
    
    public async Task GenerateSummaryAsync(DateTime summaryTime)
    {
        try
        {
            // Find messages that haven't been summarized yet and are before the summary time
            var messagesToSummarize = _state.State.Messages
                .Where(x => x.CreatedUtc < summaryTime &&
                            x.SummarizedByConversationSummary == null)
                .ToList();

            // If there are not enough messages to summarize, return
            if (messagesToSummarize.Count < 5)
            {
                return;
            }

            // Create a new summary
            var conversationSummary = new ConversationSummary
            {
                Id = Guid.NewGuid(),
                ConversationId = _state.State.Id,
                CreatedAt = DateTime.UtcNow,
                SummarizedChatMessages = new List<ChatMessage>()
            };

            // Associate messages with this summary
            foreach (var message in messagesToSummarize)
            {
                conversationSummary.SummarizedChatMessages.Add(message);
                message.SummarizedByConversationSummary = conversationSummary;
            }

            // Generate the summary text
            conversationSummary.SummaryText = await SummarizeChatHistoryMessages(conversationSummary.SummarizedChatMessages);

            // Add summary to state
            _state.State.Summaries.Add(conversationSummary);
            await SafeWriteStateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating summary for conversation {ConversationId}", _state.State.Id);
        }
    }

    private async Task<string> SummarizeChatHistoryMessages(List<ChatMessage> chatHistory)
    {
        var chatHistoryBuilder = new StringBuilder();

        foreach (var chatMessage in chatHistory)
        {
            chatHistoryBuilder
                .AppendLine("role:" + chatMessage.Source.ToString())
                .AppendLine("content:" + chatMessage.Message);
        }

        var chatHistoryString = chatHistoryBuilder.ToString();

        var prompt = $"""
                      Please summarize the following chat history:

                      [ChatHistory]
                      {chatHistoryString}
                      [/ChatHistory]
                      """;

        // Create a temporary kernel for the summary if needed
        var summaryKernel = await _kernelFactory.GetKernelForDocumentProcessAsync(_state.State.DocumentProcessName);

        var executionSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(
            _state.State.DocumentProcessName,
            AiTaskType.Summarization);

        var kernelArguments = new KernelArguments(executionSettings);
        
        var response = await summaryKernel.InvokePromptAsync(prompt, kernelArguments);

        _logger.LogInformation("Generated summary: {Summary}", response.ToString());

        return response.ToString();
    }
}