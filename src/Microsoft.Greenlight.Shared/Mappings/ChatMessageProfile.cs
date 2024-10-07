using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Mappings;

public class ChatMessageProfile : Profile
{
    public ChatMessageProfile()
    {
        CreateMap<ChatMessageDTO, ChatMessage>()
            .ForMember(x => x.ReplyToChatMessageId, y => y.MapFrom(source => source.ReplyToId));

        CreateMap<ChatMessage, ChatMessageDTO>()
            .ForMember(x => x.ReplyToId, y => y.MapFrom(source => source.ReplyToChatMessageId));

    }
}
