using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Models;
using Xunit;
using Assert = Xunit.Assert;
using Microsoft.Greenlight.Shared.Mappings;

namespace Microsoft.Greenlight.Shared.Tests.Mappings
{
    public class ChatMessageProfileTests
    {
        private readonly IMapper _mapper;

        public ChatMessageProfileTests()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<ChatMessageProfile>());
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Should_Map_ChatMessageDTO_To_ChatMessage()
        {
            // Arrange
            var dto = new ChatMessageDTO
            {
                ReplyToId = Guid.NewGuid()
            };

            // Act
            var model = _mapper.Map<ChatMessage>(dto);

            // Assert
            Assert.Equal(dto.ReplyToId, model.ReplyToChatMessageId);
        }

        [Fact]
        public void Should_Map_ChatMessage_To_ChatMessageDTO()
        {
            // Arrange
            var model = new ChatMessage
            {
                ReplyToChatMessageId = Guid.NewGuid()
            };

            // Act
            var dto = _mapper.Map<ChatMessageDTO>(model);

            // Assert
            Assert.Equal(model.ReplyToChatMessageId, dto.ReplyToId);
        }
    }
}
