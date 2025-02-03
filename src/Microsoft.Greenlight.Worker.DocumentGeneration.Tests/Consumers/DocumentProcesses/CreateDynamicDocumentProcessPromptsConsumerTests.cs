using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.DocumentProcess.Shared.Prompts;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentProcesses;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Repositories;
using Microsoft.Greenlight.Shared.Testing.Mocking;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.DocumentProcesses.Tests
{
    public sealed class CreateDynamicDocumentProcessPromptsConsumerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();

        public DocGenerationDbContext DocGenerationDbContext { get; }

        public CreateDynamicDocumentProcessPromptsConsumerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();

            // Add the default prompt definitions to the context
            typeof(DefaultPromptCatalogTypes).GetProperties().Where(prop => prop.PropertyType == typeof(string))
                .ToList()
                .ForEach(prop =>
                    DocGenerationDbContext.Add<PromptDefinition>(
                        new PromptDefinition() { Id = Guid.NewGuid(), ShortCode = prop.Name }));
            DocGenerationDbContext.SaveChanges();
        }

        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    [Collection("Tests that call AdminHelper.Initialize")]
    public sealed class CreateDynamicDocumentProcessPromptsConsumerTests :
        IDisposable, IClassFixture<CreateDynamicDocumentProcessPromptsConsumerFixture>
    {
        private readonly List<string> _promptDefinitions =            
            typeof(DefaultPromptCatalogTypes)
                .GetProperties()
                .Where(prop => prop.PropertyType == typeof(string))
                .Select(prop => prop.Name)
                .ToList();

        // Default Fakes
        private readonly IConnectionMultiplexer _fakeConnection = new Mock<IConnectionMultiplexer>().Object;
        private readonly ILogger<CreateDynamicDocumentProcessPromptsConsumer> _fakeLogger =
            new Mock<ILogger<CreateDynamicDocumentProcessPromptsConsumer>>().Object;
        private readonly CreateDynamicDocumentProcessPrompts _fakePrompts =
            new Mock<CreateDynamicDocumentProcessPrompts>(Guid.NewGuid()).Object;

        // Default Mocks
        private readonly Mock<PromptDefinitionRepository> _mockPromptDefinitionRepository;
        private readonly Mock<DynamicDocumentProcessDefinitionRepository> _mockDocumentProcessRepository;
        private readonly Mock<ConsumeContext<CreateDynamicDocumentProcessPrompts>> _mockContext;
        private readonly Mock<GenericRepository<PromptImplementation>> _mockPromptImplementationGenericRepository;

        public CreateDynamicDocumentProcessPromptsConsumerTests(
            CreateDynamicDocumentProcessPromptsConsumerFixture fixture)
        {
            var fakeConfiguration = new Mock<IConfiguration>().Object;
            AdminHelper.Initialize(fakeConfiguration);

            _mockDocumentProcessRepository =
                new Mock<DynamicDocumentProcessDefinitionRepository>(fixture.DocGenerationDbContext, _fakeConnection);
            _mockContext = new Mock<ConsumeContext<CreateDynamicDocumentProcessPrompts>>();
            _mockPromptImplementationGenericRepository =
                new Mock<GenericRepository<PromptImplementation>>(fixture.DocGenerationDbContext, _fakeConnection);

            _mockPromptDefinitionRepository =
                new Mock<PromptDefinitionRepository>(fixture.DocGenerationDbContext, _fakeConnection);
            _mockPromptDefinitionRepository.Setup(repo => repo.AllRecords()).CallBase();
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
        public async void Consume_AllPromptImplementationsPresent_DoesNotCreatePromptImplementation()
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
            var fakePromptImplementations =
                _promptDefinitions
                    .Select(name =>
                        new PromptImplementation()
                        {
                            PromptDefinitionId = Guid.NewGuid(),
                            DocumentProcessDefinitionId = guid,
                            Text = "PromptImplementation Text",
                            PromptDefinition = new() { ShortCode = name }
                        })
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

        [Fact]
        public async void Consume_MissingPromptImplementation_CreatesPromptImplementation()
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
            var fakePromptImplementations =
                _promptDefinitions
                    .Take(_promptDefinitions.Count - 1)
                    .Select(name =>
                        new PromptImplementation()
                        {
                            PromptDefinitionId = Guid.NewGuid(),
                            DocumentProcessDefinitionId = guid,
                            Text = "PromptImplementation Text",
                            PromptDefinition = new() { ShortCode = name }
                        })
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
                .Verify(repo => repo.AddAsync(It.IsAny<PromptImplementation>(), It.IsAny<Boolean>()), Times.Once);
        }

        [Fact]
        public async void Consume_MissingMultiplePromptImplementations_CreatesMissingPromptImplementations()
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
            // Remove a random sampling between 2 and the number of prompt definitions
            var missingCount = new Random().Next(_promptDefinitions.Count - 1) + 2;
            var fakePromptImplementations =
                _promptDefinitions
                    .Take(_promptDefinitions.Count - missingCount)
                    .Select(name =>
                        new PromptImplementation()
                        {
                            PromptDefinitionId = Guid.NewGuid(),
                            DocumentProcessDefinitionId = guid,
                            Text = "PromptImplementation Text",
                            PromptDefinition = new() { ShortCode = name }
                        })
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
                .Verify(repo =>
                    repo.AddAsync(It.IsAny<PromptImplementation>(), It.IsAny<Boolean>()), Times.Exactly(missingCount));
        }
    }
}