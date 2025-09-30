// Copyright (c) Microsoft Corporation. All rights reserved.
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.API.Main.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Chat.Commands;

namespace Microsoft.Greenlight.API.Main.Controllers
{
    /// <summary>
    /// Controller for handling chat-related operations using Orleans directly
    /// </summary>
    public class ChatController : BaseController
    {
        private readonly IMapper _mapper;
        private readonly IClusterClient _clusterClient;
        private readonly IPromptInfoService _promptInfoService;
        private readonly ILogger<ChatController> _logger;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatController"/> class.
        /// </summary>
        public ChatController(
            IMapper mapper,
            IClusterClient clusterClient,
            IPromptInfoService promptInfoService,
            ILogger<ChatController> logger,
            IDocumentProcessInfoService documentProcessInfoService)
        {
            _mapper = mapper;
            _clusterClient = clusterClient;
            _promptInfoService = promptInfoService;
            _logger = logger;
            _documentProcessInfoService = documentProcessInfoService;
        }

        /// <summary>
        /// Sends a chat message directly to the conversation grain.
        /// </summary>
        /// <param name="chatMessageDto">The chat message DTO.</param>
        /// <param name="useFlow">Optional parameter to route message through Flow orchestration instead of direct conversation grain.</param>
        /// <returns>An <see cref="IActionResult"/> representing the result of the operation.</returns>
        [HttpPost("")]
        [RequiresPermission(PermissionKeys.Chat)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [Consumes("application/json")]
        public async Task<IActionResult> SendChatMessage([FromBody] ChatMessageDTO chatMessageDto, [FromQuery] bool? useFlow = null)
        {
            try
            {
                if (useFlow == true)
                {
                    // Route through Flow orchestration grain using new conversation-based approach
                    var flowGrain = _clusterClient.GetGrain<IFlowOrchestrationGrain>(chatMessageDto.ConversationId);

                    // Initialize the Flow grain if needed (with user info from claims)
                    var userOid = User.FindFirst("oid")?.Value ?? User.FindFirst("sub")?.Value;
                    var userName = User.FindFirst("name")?.Value;
                    await flowGrain.InitializeAsync(userOid ?? "unknown", userName);

                    // Process via Flow conversation (fire and forget) - this will manage its own conversation
                    _ = flowGrain.ProcessMessageAsync(chatMessageDto);
                }
                else
                {
                    // Get the conversation grain (traditional approach)
                    var grain = _clusterClient.GetGrain<IConversationGrain>(chatMessageDto.ConversationId);

                    // Process the message asynchronously (fire and forget)
                    // We don't wait for the full response as it will be streamed back via Orleans streams
                    _ = grain.ProcessMessageAsync(chatMessageDto);
                }

                return Accepted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message for conversation {ConversationId}", chatMessageDto.ConversationId);
                return StatusCode(500, "An error occurred while processing the chat message");
            }
        }

        /// <summary>
        /// Gets chat messages for a specific conversation. Creates
        /// a new conversation if one does not exist.
        /// </summary>
        /// <param name="documentProcessName">The name of the document process. Use "flow" for Flow orchestration mode.</param>
        /// <param name="conversationId">The ID of the conversation.</param>
        /// <param name="useFlow">Optional parameter to use Flow orchestration instead of traditional conversation grain.</param>
        /// <returns>An <see cref="ActionResult"/> containing the chat messages.</returns>
        [HttpGet("{documentProcessName}/{conversationId}")]
        [RequiresPermission(PermissionKeys.Chat)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Produces("application/json")]
        public async Task<ActionResult<List<ChatMessageDTO>>> GetChatMessages(string documentProcessName, Guid conversationId, [FromQuery] bool? useFlow = null)
        {
            if (conversationId == Guid.Empty || string.IsNullOrEmpty(documentProcessName))
            {
                return BadRequest("Document Process Name and Conversation ID are both required");
            }

            // Determine if we should use Flow (either via explicit parameter or "flow" document process name)
            var shouldUseFlow = useFlow == true || string.Equals(documentProcessName, "flow", StringComparison.OrdinalIgnoreCase);

            if (shouldUseFlow)
            {
                // Flow mode - get user-facing conversation messages from Flow grain
                var flowGrain = _clusterClient.GetGrain<IFlowOrchestrationGrain>(conversationId);

                // Initialize Flow session if needed
                var userOid = User.FindFirst("oid")?.Value ?? User.FindFirst("sub")?.Value;
                var userName = User.FindFirst("name")?.Value;
                await flowGrain.InitializeAsync(userOid ?? "unknown", userName);

                // Get the user-facing conversation messages (not backend orchestration)
                var messages = await flowGrain.GetMessagesAsync();

                if (messages == null || !messages.Any())
                {
                    return NotFound();
                }

                return Ok(messages);
            }
            else
            {
                // Traditional conversation grain approach
                var grain = _clusterClient.GetGrain<IConversationGrain>(conversationId);

                // Check if the conversation exists
                var state = await grain.GetStateAsync();

                // If the conversation doesn't exist or has no messages, create a new one
                if (state.Id == Guid.Empty || state.Messages.Count == 0)
                {
                    // Get the system prompt
                    var systemPrompt = await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(
                        PromptNames.ChatSystemPrompt, documentProcessName);

                    // Initialize the conversation
                    await grain.InitializeAsync(documentProcessName, systemPrompt);

                    // Return an empty list with 404 status
                    return NotFound();
                }

                // Get the messages
                var messages = await grain.GetMessagesAsync();
                return Ok(messages);
            }
        }

        /// <summary>
        /// Sets or changes the Document Process for an existing conversation. Optionally updates the system prompt.
        /// </summary>
        /// <param name="conversationId">Conversation identifier.</param>
        /// <param name="request">Request containing the new document process short name and flags.</param>
        [HttpPost("conversation/{conversationId}/document-process")]
        [RequiresPermission(PermissionKeys.Chat)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetConversationDocumentProcess(Guid conversationId, [FromBody] SetConversationDocumentProcessRequest request)
        {
            if (conversationId == Guid.Empty)
            {
                return BadRequest("Conversation ID is required");
            }
            if (request == null || string.IsNullOrWhiteSpace(request.DocumentProcessName))
            {
                return BadRequest("DocumentProcessName is required");
            }

            // Validate the document process exists
            var processInfo = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(request.DocumentProcessName);
            if (processInfo == null)
            {
                return NotFound($"Document process '{request.DocumentProcessName}' not found");
            }

            var grain = _clusterClient.GetGrain<IConversationGrain>(conversationId);
            var ok = await grain.SetDocumentProcessAsync(request.DocumentProcessName, request.UpdateSystemPrompt);
            if (!ok)
            {
                return BadRequest("Failed to set document process on conversation");
            }
            return NoContent();
        }
    }
}
