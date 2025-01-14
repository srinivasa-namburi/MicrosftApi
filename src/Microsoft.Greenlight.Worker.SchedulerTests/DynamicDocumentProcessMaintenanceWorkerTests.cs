using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.DocumentProcess.Shared.Prompts;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Repositories;
using StackExchange.Redis;
using Moq;
using Xunit;
using System.Reflection;

namespace Microsoft.Greenlight.Worker.Scheduler.Tests
{
    public class DynamicDocumentProcessMaintenanceWorkerTests
    {
        private readonly Mock<PromptDefinitionRepository> _mockPromptDefinitionRepository;
        private readonly DynamicDocumentProcessMaintenanceWorker _worker;
        private readonly IEnumerable<PropertyInfo> _properties;

        public DynamicDocumentProcessMaintenanceWorkerTests()
        {
            var options = new DbContextOptionsBuilder<DocGenerationDbContext>().Options;
            var dbContextMock = new Mock<DocGenerationDbContext>(options);
            var redisConnectionMock = new Mock<IConnectionMultiplexer>();
            _mockPromptDefinitionRepository = new Mock<PromptDefinitionRepository>(dbContextMock.Object, redisConnectionMock.Object);

            _worker = new DynamicDocumentProcessMaintenanceWorker(_mockPromptDefinitionRepository.Object);

            var promptCatalogProperties = typeof(DefaultPromptCatalogTypes).GetProperties();
            _properties = promptCatalogProperties.Where(p => p.PropertyType == typeof(string));
        }

        [Fact]
        public async Task ExecuteAsync_WhenNotPresentInRepository_ShouldCreatePromptDefinitions()
        {
            // Arrange
            var stoppingToken = new CancellationTokenSource();
            stoppingToken.CancelAfter(1000); // Cancel after 1 second to stop the loop

            _mockPromptDefinitionRepository
                .Setup(repo => repo.GetAllPromptDefinitionsAsync(It.IsAny<bool>()))
                .ReturnsAsync(new List<PromptDefinition>());

            _mockPromptDefinitionRepository
                .Setup(repo => repo.AddAsync(It.IsAny<PromptDefinition>(), It.IsAny<bool>()))
                .Returns(Task.CompletedTask);

            _mockPromptDefinitionRepository
                .Setup(repo => repo.SaveChangesAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _worker.StartAsync(stoppingToken.Token);

            // Assert
            _mockPromptDefinitionRepository.Verify(repo => repo.AddAsync(It.IsAny<PromptDefinition>(), false), Times.Exactly(_properties.Count()));
            _mockPromptDefinitionRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WhenDefinitionsExist_ShouldNotAddDefinitions()
        {
            // Arrange
            var stoppingToken = new CancellationTokenSource();
            stoppingToken.CancelAfter(1000); // Cancel after 1 second to stop the loop

            var promptCatalogProperties = typeof(DefaultPromptCatalogTypes).GetProperties();
            var stringProperties = promptCatalogProperties.Where(p => p.PropertyType == typeof(string));

            var promptDefinitions = new List<PromptDefinition>();
            foreach (var prop in stringProperties)
            {
                promptDefinitions.Add(new PromptDefinition { ShortCode = prop.Name });
            }

            _mockPromptDefinitionRepository
                .Setup(repo => repo.GetAllPromptDefinitionsAsync(It.IsAny<bool>()))
                .ReturnsAsync(promptDefinitions);

            // Act
            await _worker.StartAsync(stoppingToken.Token);

            // Assert
            _mockPromptDefinitionRepository.Verify(repo => repo.AddAsync(It.IsAny<PromptDefinition>(), false), Times.Never);
            _mockPromptDefinitionRepository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
        }
    }
}
