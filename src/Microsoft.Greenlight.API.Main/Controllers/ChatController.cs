using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatController"/> class.
        /// </summary>
        public ChatController(
            IMapper mapper,
            IClusterClient clusterClient,
            IPromptInfoService promptInfoService,
            ILogger<ChatController> logger)
        {
            _mapper = mapper;
            _clusterClient = clusterClient;
            _promptInfoService = promptInfoService;
            _logger = logger;
        }

        /// <summary>
        /// Sends a chat message directly to the conversation grain.
        /// </summary>
        /// <param name="chatMessageDto">The chat message DTO.</param>
        /// <returns>An <see cref="IActionResult"/> representing the result of the operation.</returns>
        [HttpPost("")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [Consumes("application/json")]
        public async Task<IActionResult> SendChatMessage([FromBody] ChatMessageDTO chatMessageDto)
        {
            try
            {
                // Get the conversation grain
                var grain = _clusterClient.GetGrain<IConversationGrain>(chatMessageDto.ConversationId);
                
                // Process the message asynchronously (fire and forget)
                // We don't wait for the full response as it will be streamed back via Orleans streams
                _ = grain.ProcessMessageAsync(chatMessageDto);
                
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
        /// <param name="documentProcessName">The name of the document process.</param>
        /// <param name="conversationId">The ID of the conversation.</param>
        /// <returns>An <see cref="ActionResult"/> containing the chat messages.</returns>
        [HttpGet("{documentProcessName}/{conversationId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Produces("application/json")]
        public async Task<ActionResult<List<ChatMessageDTO>>> GetChatMessages(string documentProcessName, Guid conversationId)
        {
            if (conversationId == Guid.Empty || string.IsNullOrEmpty(documentProcessName))
            {
                return BadRequest("Document Process Name and Conversation ID are both required");
            }

            // Get the conversation grain
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
}