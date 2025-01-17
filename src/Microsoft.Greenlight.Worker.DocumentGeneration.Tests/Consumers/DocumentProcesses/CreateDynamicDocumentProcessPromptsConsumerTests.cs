
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentProcesses;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Repositories;
using Microsoft.Greenlight.Shared.Testing.Mocking;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.DocumentProcesses.Tests
{
    [Collection("Tests that call AdminHelper.Initialize")]
    public sealed class CreateDynamicDocumentProcessPromptsConsumerTests : IDisposable
    {
        // Default Fakes
        private readonly DocGenerationDbContext _fakeDbContext =
            new Mock<DocGenerationDbContext>(new DbContextOptionsBuilder<DocGenerationDbContext>().Options).Object;
        private readonly IConnectionMultiplexer _fakeConnection = new Mock<IConnectionMultiplexer>().Object;
        private readonly ILogger<CreateDynamicDocumentProcessPromptsConsumer> _fakeLogger =
            new Mock<ILogger<CreateDynamicDocumentProcessPromptsConsumer>>().Object;
        private readonly CreateDynamicDocumentProcessPrompts _fakePrompts =
            new Mock<CreateDynamicDocumentProcessPrompts>(Guid.NewGuid()).Object;

        // Default Mocks
        private readonly Mock<PromptDefinitionRepository> _mockPromptDefinitionRepository;
        private readonly Mock<GenericRepository<PromptImplementation>> _mockPromptImplementationGenericRepository;
        private readonly Mock<DynamicDocumentProcessDefinitionRepository> _mockDocumentProcessRepository;
        private readonly Mock<ConsumeContext<CreateDynamicDocumentProcessPrompts>> _mockContext;

        public CreateDynamicDocumentProcessPromptsConsumerTests()
        {
            var fakeConfiguration = new Mock<IConfiguration>().Object;
            AdminHelper.Initialize(fakeConfiguration);
            _mockPromptDefinitionRepository = new Mock<PromptDefinitionRepository>(_fakeDbContext, _fakeConnection);
            _mockPromptImplementationGenericRepository =
                new Mock<GenericRepository<PromptImplementation>>(_fakeDbContext, _fakeConnection);
            _mockDocumentProcessRepository =
                new Mock<DynamicDocumentProcessDefinitionRepository>(_fakeDbContext, _fakeConnection);
            _mockContext = new Mock<ConsumeContext<CreateDynamicDocumentProcessPrompts>>();
        }

        public void Dispose()
        {
            AdminHelper.Initialize(null);
        }

        [Fact]
        public async void Consume_WithoutDocumentProcess_CallsGetByIdAsyncOnceAndMakesNoOtherCalls()
        {
            // Arrange
            // null DynamicDocumentProcessDefinition returned
            _mockDocumentProcessRepository
                .Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Boolean>()))
                .ReturnsAsync((DynamicDocumentProcessDefinition?)null);
            _mockContext.Setup(ctx => ctx.Message).Returns(_fakePrompts);

            var unitUnderTest = new CreateDynamicDocumentProcessPromptsConsumer(
                _mockPromptDefinitionRepository.Object,
                _mockPromptImplementationGenericRepository.Object,
                _mockDocumentProcessRepository.Object,
                _fakeLogger);

            // Act
            await unitUnderTest.Consume(_mockContext.Object);

            // Assert
            _mockDocumentProcessRepository.Verify(
                repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Boolean>()),
                Times.Once);
            _mockDocumentProcessRepository.VerifyNoOtherCalls();
        }

        [Fact]
        public async void Consume_AllPrompImplementationsPresent_DoesNotCreatePromptImplementation()
        {
            // Arrange
            _mockContext.Setup(ctx => ctx.Message).Returns(_fakePrompts);
            // DocumentProcessRepository
            var guid = _fakePrompts.DocumentProcessId;
            var mockDynamicDocumentProcessDefinition = new Mock<DynamicDocumentProcessDefinition>();
            mockDynamicDocumentProcessDefinition.SetupGet(doc => doc.Id).Returns(guid);
            _mockDocumentProcessRepository
                .Setup(repo => repo.GetByIdAsync(It.Is<Guid>(g => guid.Equals(g)), It.IsAny<Boolean>()))
                .ReturnsAsync(mockDynamicDocumentProcessDefinition.Object);
            // PromptImplementationRepository
            var fakePromptImplementations = new PromptImplementation[]
            {
                new()
                {
                    PromptDefinitionId = Guid.NewGuid(),
                    DocumentProcessDefinitionId = guid,
                    Text = "PromptImplementation Text",
                    PromptDefinition = new() { ShortCode = "ChatSystemPrompt"}
                },
                new()
                {
                    PromptDefinitionId = Guid.NewGuid(),
                    DocumentProcessDefinitionId = guid,
                    Text = "PromptImplementation Text",
                    PromptDefinition = new() { ShortCode = "ChatSinglePassUserPrompt"}
                },
                new()
                {
                    PromptDefinitionId = Guid.NewGuid(),
                    DocumentProcessDefinitionId = guid,
                    Text = "PromptImplementation Text",
                    PromptDefinition = new() { ShortCode = "SectionGenerationSystemPrompt"}
                },
                new()
                {
                    PromptDefinitionId = Guid.NewGuid(),
                    DocumentProcessDefinitionId = guid,
                    Text = "PromptImplementation Text",
                    PromptDefinition = new() { ShortCode = "SectionGenerationMainPrompt"}
                },
                new()
                {
                    PromptDefinitionId = Guid.NewGuid(),
                    DocumentProcessDefinitionId = guid,
                    Text = "PromptImplementation Text",
                    PromptDefinition = new() { ShortCode = "SectionGenerationSummaryPrompt"}
                },
                new()
                {
                    PromptDefinitionId = Guid.NewGuid(),
                    DocumentProcessDefinitionId = guid,
                    Text = "PromptImplementation Text",
                    PromptDefinition = new() { ShortCode = "SectionGenerationMultiPassContinuationPrompt"}
                }
            }
            .AsQueryable();
            var mockPromptImplementationDbSet = new Mock<DbSet<PromptImplementation>>();
            MockDbSet.SetupMockDbSet(mockPromptImplementationDbSet, fakePromptImplementations);
            _mockPromptImplementationGenericRepository.Setup(repo => repo.AllRecords())
                                                      .Returns(mockPromptImplementationDbSet.Object);

            var unitUnderTest = new CreateDynamicDocumentProcessPromptsConsumer(
                _mockPromptDefinitionRepository.Object,
                _mockPromptImplementationGenericRepository.Object,
                _mockDocumentProcessRepository.Object,
                _fakeLogger);

            // Act
            await unitUnderTest.Consume(_mockContext.Object);

            // Assert
            _mockPromptImplementationGenericRepository
                .Verify(repo => repo.AddAsync(It.IsAny<PromptImplementation>(), It.IsAny<Boolean>()), Times.Never);
        }
    }
}