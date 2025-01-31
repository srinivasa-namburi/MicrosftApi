using AutoMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Worker.Chat.Consumers.Tests
{
    public class ProcessChatMessageConsumerTests : IClassFixture<ConnectionFactory>
    {
        private readonly ConnectionFactory connectionFactory;
        private readonly IMapper _mapper = new Mapper(
            new MapperConfiguration(cfg => cfg.AddProfile<ChatMessageProfile>()));
        private readonly Mock<IKeyedServiceProvider> _fakeServiceProvider = new();
        private readonly Mock<IPromptInfoService> _fakePromptInfoService = new();
        private readonly Mock<ILogger<ProcessChatMessageConsumer>> _fakeLogger = new();
        private readonly Mock<IDocumentProcessInfoService> _fakeDocumentProcessInfoService = new();

        public ProcessChatMessageConsumerTests(ConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        [Fact]
        public async Task Consume_WhenMessageNotPresent_ShouldStoreAndProcessChatMessage()
        {
            // Arrange
            Guid chatMessageId = Guid.NewGuid();
            var expectedCountOfMessages = 2; // 1 for the initial message and 1 for the response

            // Setup conversation and chat message data
            using var context = connectionFactory.CreateContext();
            var chatConversation = Seed_Conversation(context);
            var chatMessageDto = new ChatMessageDTO
            {
                Id = chatMessageId,
                ConversationId = chatConversation.Id
            };
            var processChatMessage = new ProcessChatMessage(Guid.NewGuid(), chatMessageDto);

            // Mock MassTransit items
            var contextMock = new Mock<ConsumeContext<ProcessChatMessage>>();
            contextMock.Setup(x => x.Message).Returns(processChatMessage);

            // Fake Semantic Kernel items
            var fakeChatCompletion = new Mock<IChatCompletionService>();
            fakeChatCompletion
                .Setup(x => x.GetStreamingChatMessageContentsAsync(
                    It.IsAny<ChatHistory>(),
                    It.IsAny<PromptExecutionSettings>(),
                    It.IsAny<Kernel>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new List<StreamingChatMessageContent>() { new(AuthorRole.Assistant, "AI response") }.ToAsyncEnumerable());
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(fakeChatCompletion.Object);
            var fakeKernel = kernelBuilder.Build();

            // Setup fake document process info service
            _fakeDocumentProcessInfoService.Setup(x => x.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync(new DocumentProcessInfo());

            // Setup fake service provider
            var fakeScope = new Mock<IServiceScope>();
            fakeScope.Setup(x => x.ServiceProvider).Returns(_fakeServiceProvider.Object);
            var fakeServiceScopeFactory = new Mock<IServiceScopeFactory>();
            fakeServiceScopeFactory.Setup(x => x.CreateScope()).Returns(fakeScope.Object);

            _fakeServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(fakeServiceScopeFactory.Object);
            _fakeServiceProvider.Setup(x => x.GetService(typeof(IDocumentProcessInfoService)))
                .Returns(_fakeDocumentProcessInfoService.Object);
            _fakeServiceProvider.Setup(x => x.GetService(typeof(Kernel))).Returns(fakeKernel);

            // Setup the fake prompt info service
            _fakePromptInfoService.Setup(x => x.GetPromptTextByShortCodeAndProcessNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(chatConversation.SystemPrompt);

            var consumer = new ProcessChatMessageConsumer(
                context,
                _mapper,
                _fakeServiceProvider.Object,
                _fakePromptInfoService.Object,
                _fakeLogger.Object,
                _fakeDocumentProcessInfoService.Object
            );

            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            var actualChatMessage = await context.ChatMessages.FindAsync(chatMessageId);
            var actualCountOfMessages = await context.ChatMessages.CountAsync();
            // Verify chat message response received event was published
            contextMock.Verify(ctx => ctx.Publish(It.IsAny<ChatMessageResponseReceived>(), default), Times.Exactly(1));
            // Assert chat message was stored and processed by assistant
            Assert.NotNull(actualChatMessage);
            Assert.Equal(expectedCountOfMessages, actualCountOfMessages);
        }

        private static ChatConversation Seed_Conversation(TestDocGenerationDbContext context)
        {
            var conversation = new ChatConversation();
            context.Add(conversation);
            context.SaveChanges();
            return conversation;
        }
    }
}