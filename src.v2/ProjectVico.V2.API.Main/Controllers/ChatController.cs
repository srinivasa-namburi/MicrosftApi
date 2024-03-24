using AutoMapper;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    
    public ChatController(
        DocGenerationDbContext dbContext,
        IMapper mapper,
        IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
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

    [HttpGet("{conversationId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetChatMessages(Guid conversationId)
    {
        var chatMessages = new List<ChatMessageDTO>();

        var chatMessageModels = await _dbContext.ChatMessages
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt)
            .Include(x=>x.AuthorUserInformation)
            .ToListAsync();

        if (chatMessageModels.Count == 0)
        {
            // if there is no existing conversation, create a new one, and then return a 404
            var conversation = new ChatConversation
            {
                Id = conversationId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ChatConversations.Add(conversation);
            await _dbContext.SaveChangesAsync();


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



}