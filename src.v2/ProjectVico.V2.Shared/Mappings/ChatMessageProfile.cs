using AutoMapper;
using ProjectVico.V2.Shared.Contracts.Chat;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Mappings;

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