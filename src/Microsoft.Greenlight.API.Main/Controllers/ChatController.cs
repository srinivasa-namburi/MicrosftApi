using AutoMapper;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Prompts;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for handling chat-related operations.
/// </summary>
public class ChatController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IServiceProvider _sp;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    /// <param name="publishEndpoint">The publish endpoint for messaging.</param>
    /// <param name="sp">The service provider.</param>
    public ChatController(
        DocGenerationDbContext dbContext,
        IMapper mapper,
        IPublishEndpoint publishEndpoint,
        IServiceProvider sp
    )
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
        _sp = sp;
    }

    /// <summary>
    /// Sends a chat message.
    /// </summary>
    /// <param name="chatMessageDto">The chat message DTO.</param>
    /// <returns>An <see cref="IActionResult"/> representing the result of the operation.
    /// Produces Status Codes:
    ///     204 No content: When completed sucessfully
    /// </returns>
    [HttpPost("")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [Consumes("application/json")]
    public async Task<IActionResult> SendChatMessage([FromBody] ChatMessageDTO chatMessageDto)
    {
        await _publishEndpoint.Publish(new ProcessChatMessage(chatMessageDto.ConversationId, chatMessageDto));
        return NoContent();
    }

    /// <summary>
    /// Gets chat messages for a specific conversation.
    /// </summary>
    /// <param name="documentProcessName">The name of the document process.</param>
    /// <param name="conversationId">The ID of the conversation.</param>
    /// <returns>An <see cref="ActionResult"/> containing the chat messages.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     400 Bad Request: When a required parameter is not provided. 
    ///     404 Not found: When no chat messages are found for the provided Conversation Id
    /// </returns>
    [HttpGet("{documentProcessName}/{conversationId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Produces<List<ChatMessageDTO>>]
    public async Task<ActionResult<List<ChatMessageDTO>>> GetChatMessages(string documentProcessName, Guid conversationId)
    {
        if (conversationId == Guid.Empty || string.IsNullOrEmpty(documentProcessName))
        {
            return BadRequest("Document Process Name and Conversation ID are both required");
        }

        var chatMessages = new List<ChatMessageDTO>();

        var chatMessageModels = await _dbContext.ChatMessages
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedUtc)
            .Include(x=>x.AuthorUserInformation)
            .ToListAsync();

        if (chatMessageModels.Count == 0)
        {
            // if there is no existing conversation, create a new one, and then return a 404
            await CreateChatConversationAsync(documentProcessName, conversationId);
            return NotFound();
        }

        foreach (var chatMessageModel in chatMessageModels)
        {
            var chatMessageDto = _mapper.Map<ChatMessageDTO>(chatMessageModel);
            if (chatMessageModel.AuthorUserInformation != null)
            {
                chatMessageDto.UserId = chatMessageModel.AuthorUserInformation.ProviderSubjectId;
                chatMessageDto.UserFullName = chatMessageModel.AuthorUserInformation.FullName;
            }
            chatMessages.Add(chatMessageDto);
        }

        return Ok(chatMessages);
    }

    /// <summary>
    /// Creates a new chat conversation.
    /// </summary>
    /// <param name="documentProcessName">The name of the document process.</param>
    /// <param name="conversationId">The ID of the conversation.</param>
    /// <returns>The created <see cref="ChatConversation"/>.</returns>
    private async Task<ChatConversation> CreateChatConversationAsync(string documentProcessName, Guid conversationId)
    {
        var conversation = new ChatConversation
        {
            Id = conversationId,
            CreatedUtc = DateTime.UtcNow,
            DocumentProcessName = documentProcessName
        };

        var promptCatalogTypes = _sp.GetRequiredServiceForDocumentProcess<IPromptCatalogTypes>(documentProcessName);

        conversation.SystemPrompt = promptCatalogTypes.ChatSystemPrompt;

        _dbContext.ChatConversations.Add(conversation);
        await _dbContext.SaveChangesAsync();
        return conversation;
    }
}
