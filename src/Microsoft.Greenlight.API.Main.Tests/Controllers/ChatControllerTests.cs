using AutoMapper;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;

namespace Microsoft.Greenlight.API.Main.Tests.Controllers
{
    public sealed class ChatControllerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();
        public DocGenerationDbContext DocGenerationDbContext { get; }
        public ChatControllerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }
        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public class ChatControllerTests : IClassFixture<ChatControllerFixture>
    {
        private readonly IMapper _mapper;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;
        private readonly DocGenerationDbContext _docGenerationDbContext;
        private readonly Mock<IServiceProvider> _serviceProviderMock = new();
        private readonly Mock<IServiceScope> _servicesScopeMock = new();
        private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock = new();
        private readonly Mock<IServiceProvider> _scopedServiceProviderMock = new();
        private readonly Mock<IDocumentProcessInfoService> _documentProcessInfoServiceMock = new();
        private readonly Mock<IPromptInfoService> _promptInfoService = new();

        public ChatControllerTests(ChatControllerFixture fixture)
        {
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<ChatMessageProfile>()).CreateMapper();
            _publishEndpointMock = new Mock<IPublishEndpoint>();
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _promptInfoService = new Mock<IPromptInfoService>();

            _serviceScopeFactoryMock.Setup(x => x.CreateScope()).Returns(_servicesScopeMock.Object);
            _servicesScopeMock.Setup(x => x.ServiceProvider).Returns(_scopedServiceProviderMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(_serviceScopeFactoryMock.Object);
            _scopedServiceProviderMock.Setup(x => x.GetService(typeof(IDocumentProcessInfoService)))
                .Returns(_documentProcessInfoServiceMock.Object);
        }

        [Fact]
        public async Task GetChatMessages_WithEmptyConversationId_ReturnsBadRequest()
        {
            // Arrange
            var documentProcessName = "TestProcess";
            var conversationId = Guid.Empty;
            var controller = new ChatController
            (
                _docGenerationDbContext,
                _mapper,
                _publishEndpointMock.Object,
                _serviceProviderMock.Object,
                _promptInfoService.Object
            );

            // Act
            var result = await controller.GetChatMessages(documentProcessName, conversationId);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetChatMessages_WithEmptyDocumentProcessName_ReturnsBadRequest()
        {
            // Arrange
            var documentProcessName = string.Empty;
            var conversationId = Guid.NewGuid();
            var controller = new ChatController
            (
                _docGenerationDbContext,
                _mapper,
                _publishEndpointMock.Object,
                _serviceProviderMock.Object,
                _promptInfoService.Object
            );

            // Act
            var result = await controller.GetChatMessages(documentProcessName, conversationId);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetChatMessages_WithValidParameters_ReturnsExpectedChatMessages()
        {
            // Arrange
            var testMessageOne = "Test message 1";
            var testMessageTwo = "Test message 2";
            var providerSubjectIdOne = "user1";
            var providerSubjectIdTwo = "user2";
            var userFullNameOne = "User One";
            var userFullNameTwo = "User Two";
            var documentProcessName = "TestProcess";
            var conversationId = Guid.NewGuid();

            var chatConversation = new ChatConversation
            {
                Id = conversationId,
                DocumentProcessName = documentProcessName,
                SystemPrompt = "Test prompt"
            };

            _docGenerationDbContext.ChatConversations.Add(chatConversation);
            var chatMessages = new List<ChatMessage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Message = testMessageOne,
                    AuthorUserInformation = new UserInformation
                    {
                        ProviderSubjectId = providerSubjectIdOne,
                        FullName = userFullNameOne
                    }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Message = testMessageTwo,
                    AuthorUserInformation = new UserInformation
                    {
                        ProviderSubjectId = providerSubjectIdTwo,
                        FullName = userFullNameTwo
                    }
                }
            };
            _docGenerationDbContext.ChatMessages.AddRange(chatMessages);
            await _docGenerationDbContext.SaveChangesAsync();
            var controller = new ChatController
            (
                _docGenerationDbContext,
                _mapper,
                _publishEndpointMock.Object,
                _serviceProviderMock.Object,
                _promptInfoService.Object
            );

            // Act
            var result = await controller.GetChatMessages
            (
                documentProcessName,
                conversationId
            );

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedMessages = Assert.IsType<List<ChatMessageDTO>>(okResult.Value);
            Assert.Contains(returnedMessages, m =>
                m.Message == testMessageOne &&
                m.UserId == providerSubjectIdOne &&
                m.UserFullName == userFullNameOne);
            Assert.Contains(returnedMessages, m =>
                m.Message == testMessageTwo &&
                m.UserId == providerSubjectIdTwo &&
                m.UserFullName == userFullNameTwo);

            // Clean up
            _docGenerationDbContext.ChatMessages.RemoveRange(chatMessages);
            _docGenerationDbContext.ChatConversations.Remove(chatConversation);
            _docGenerationDbContext.SaveChanges();
        }
    }
}
