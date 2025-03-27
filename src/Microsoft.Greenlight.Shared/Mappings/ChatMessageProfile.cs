using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// Profile for mapping between <see cref="ChatMessageDTO"/> and <see cref="ChatMessage"/>.
/// </summary>
public class ChatMessageProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMessageProfile"/> class.
    /// Defining the mapping between ChatMessageDTO and ChatMessage.
    /// </summary>
    public ChatMessageProfile()
    {
        CreateMap<ChatMessageDTO, ChatMessage>()
            .ForMember(x => x.ReplyToChatMessageId, y => y.MapFrom(source => source.ReplyToId));

        CreateMap<ChatMessage, ChatMessageDTO>()
            .ForMember(x => x.ReplyToId, y => y.MapFrom(source => source.ReplyToChatMessageId));
    }
}