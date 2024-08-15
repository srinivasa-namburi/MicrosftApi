using AutoMapper;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.Shared.Prompts;
using ProjectVico.V2.Shared.Contracts.Chat;
using ProjectVico.V2.Shared.Contracts.Messages.Chat.Commands;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.API.Main.Controllers;

public class ChatController : BaseController
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IServiceProvider _sp;

    public ChatController(
        DocGenerationDbContext dbContext,
        IMapper mapper,
        IPublishEndpoint publishEndpoint,
        IServiceProvider sp)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
        _sp = sp;
    }

    [HttpPost("")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    public async Task<IActionResult> SendChatMessage([FromBody] ChatMessageDTO chatMessageDto)
    {
        await _publishEndpoint.Publish<ProcessChatMessage>(new ProcessChatMessage(chatMessageDto.ConversationId, chatMessageDto));
        return Ok();
    }

    [HttpGet("{documentProcessName}/{conversationId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetChatMessages(string documentProcessName, Guid conversationId)
    {
        if (conversationId == Guid.Empty || string.IsNullOrEmpty(documentProcessName))
        {
            return BadRequest("Document Process Name and Conversation ID are both required");
        }

        var chatMessages = new List<ChatMessageDTO>();

        var chatMessageModels = await _dbContext.ChatMessages
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt)
            .Include(x=>x.AuthorUserInformation)
            .ToListAsync();

        if (chatMessageModels.Count == 0)
        {
            // if there is no existing conversation, create a new one, and then return a 404
            await CreateChatConversationAsync(documentProcessName, conversationId);
            return NotFound();
        }

        foreach (var chatMessageModel in chatMessageModels){
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

    private async Task<ChatConversation> CreateChatConversationAsync(string documentProcessName, Guid conversationId)
    {
        var conversation = new ChatConversation
        {
            Id = conversationId,
            CreatedAt = DateTime.UtcNow,
            DocumentProcessName = documentProcessName
        };

        var promptCatalogTypes = _sp.GetRequiredServiceForDocumentProcess<IPromptCatalogTypes>(documentProcessName);

        conversation.SystemPrompt = promptCatalogTypes.ChatSystemPrompt;

        _dbContext.ChatConversations.Add(conversation);
        await _dbContext.SaveChangesAsync();
        return conversation;
    }
}