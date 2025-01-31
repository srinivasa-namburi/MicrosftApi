using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Worker.Chat.Consumers.Tests
{
    public class GenerateChatHistorySummaryConsumerTests : IClassFixture<ConnectionFactory>
    {
        private readonly ConnectionFactory connectionFactory;

        public GenerateChatHistorySummaryConsumerTests(ConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        [Fact]
        public async Task Consume_ShouldGenerateSummary_WhenThereAreEnoughMessages()
        {
            // Arrange
            var amountOfMessages = 5;
            var expectedSummaryText = "Summary";

            // Setup conversation and chat message data
            using var context = connectionFactory.CreateContext();
            var conversationId = Seed_Data(context, amountOfMessages);

            // Mock MassTransit items
            var contextMock = new Mock<ConsumeContext<GenerateChatHistorySummary>>();
            contextMock.Setup(x => x.Message).Returns(new GenerateChatHistorySummary(conversationId, DateTime.UtcNow));

            // Fake Semantic Kernel items
            var fakeChatCompletion = new Mock<IChatCompletionService>();
            fakeChatCompletion
                .Setup(x => x.GetChatMessageContentsAsync(
                    It.IsAny<ChatHistory>(),
                    It.IsAny<PromptExecutionSettings>(),
                    It.IsAny<Kernel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([new ChatMessageContent(AuthorRole.Assistant, expectedSummaryText)]);
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(fakeChatCompletion.Object);
            var fakeKernel = kernelBuilder.Build();

            var consumer = new GenerateChatHistorySummaryConsumer(context, fakeKernel);

            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            var conversationSummary = await context.ConversationSummaries.FirstAsync();
            var numOfSummaries = await context.ConversationSummaries.CountAsync();
            Assert.Equal(1, numOfSummaries);
            Assert.Equal(expectedSummaryText, conversationSummary.SummaryText);
        }

        [Fact]
        public async Task Consume_ShouldNotGenerateSummary_WhenThereAreNotEnoughMessages()
        {
            // Arrange
            var amountOfMessages = 4;

            // Setup conversation and chat message data
            using var context = connectionFactory.CreateContext();
            var conversationId = Seed_Data(context, amountOfMessages);
            var expectedNumOfSummaries = await context.ConversationSummaries.CountAsync();

            // Mock MassTransit items
            var contextMock = new Mock<ConsumeContext<GenerateChatHistorySummary>>();
            contextMock.Setup(x => x.Message).Returns(new GenerateChatHistorySummary(conversationId, DateTime.UtcNow));

            // Fake Semantic Kernel items
            var kernelBuilder = Kernel.CreateBuilder();
            var fakeKernel = kernelBuilder.Build();

            var consumer = new GenerateChatHistorySummaryConsumer(context, fakeKernel);
            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            var conversationSummary = await context.ConversationSummaries.FirstAsync();
            var numOfSummaries = await context.ConversationSummaries.CountAsync();
            Assert.Equal(expectedNumOfSummaries, numOfSummaries);
        }

        private static Guid Seed_Data(TestDocGenerationDbContext context, int amountOfMessages)
        {
            var conversation = new ChatConversation();
            context.Add(conversation);

            var chatMessages = new List<ChatMessage>();
            for (int i = 0; i < amountOfMessages; i++)
            {
                chatMessages.Add(new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversation.Id,
                    CreatedUtc = DateTime.UtcNow.AddMinutes(-i),
                    Message = $"Message {i}"
                });
            }
            context.ChatMessages.AddRange(chatMessages);
            context.SaveChanges();
            return conversation.Id;
        }
    }
}