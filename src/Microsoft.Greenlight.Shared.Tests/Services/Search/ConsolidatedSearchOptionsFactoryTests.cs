using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Moq;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Shared.Tests.Services.Search
{
    public class ConsolidatedSearchOptionsFactoryTests
    {
        private readonly Mock<IDocumentProcessInfoService> _mockDocumentProcessInfoService;
        private readonly ConsolidatedSearchOptionsFactory _factory;

        public ConsolidatedSearchOptionsFactoryTests()
        {
            _mockDocumentProcessInfoService = new Mock<IDocumentProcessInfoService>();
            _factory = new ConsolidatedSearchOptionsFactory(_mockDocumentProcessInfoService.Object);
        }

        [Fact]
        public async Task CreateSearchOptionsForDocumentProcessAsync_ByName_WhenDocumentProcessNotFound_ReturnsDefaultOptions()
        {
            // Arrange
            _mockDocumentProcessInfoService
                .Setup(service => service.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync((DocumentProcessInfo?)null);

            // Act
            var result = await _factory.CreateSearchOptionsForDocumentProcessAsync("NonExistentProcess");

            // Assert
            Assert.Equal("default", result.IndexName);
        }

        [Fact]
        public async Task CreateSearchOptionsForDocumentProcessAsync_ByName_WhenDocumentProcessFound_ReturnsOptions()
        {
            // Arrange
            var documentProcess = new DocumentProcessInfo
            {
                Repositories = new List<string> { "repo1" },
                NumberOfCitationsToGetFromRepository = 10,
                MinimumRelevanceForCitations = 0.8,
                PrecedingSearchPartitionInclusionCount = 2,
                FollowingSearchPartitionInclusionCount = 3
            };

            _mockDocumentProcessInfoService
                .Setup(service => service.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync(documentProcess);

            // Act
            var result = await _factory.CreateSearchOptionsForDocumentProcessAsync("ExistingProcess");

            // Assert
            Assert.Equal(documentProcess.Repositories[0], result.IndexName);
            Assert.Equal(documentProcess.NumberOfCitationsToGetFromRepository, result.Top);
            Assert.Equal(documentProcess.MinimumRelevanceForCitations, result.MinRelevance);
            Assert.Equal(documentProcess.PrecedingSearchPartitionInclusionCount, result.PrecedingPartitionCount);
            Assert.Equal(documentProcess.FollowingSearchPartitionInclusionCount, result.FollowingPartitionCount);
        }

        [Fact]
        public async Task CreateSearchOptionsForDocumentProcessAsync_ByDocumentProcess_ReturnsOptions()
        {
            // Arrange
            var documentProcess = new DocumentProcessInfo
            {
                Repositories = new List<string> { "repo1" },
                NumberOfCitationsToGetFromRepository = 10,
                MinimumRelevanceForCitations = 0.8,
                PrecedingSearchPartitionInclusionCount = 2,
                FollowingSearchPartitionInclusionCount = 3
            };

            // Act
            var result = await _factory.CreateSearchOptionsForDocumentProcessAsync(documentProcess);

            // Assert
            Assert.Equal(documentProcess.Repositories[0], result.IndexName);
            Assert.Equal(documentProcess.NumberOfCitationsToGetFromRepository, result.Top);
            Assert.Equal(documentProcess.MinimumRelevanceForCitations, result.MinRelevance);
            Assert.Equal(documentProcess.PrecedingSearchPartitionInclusionCount, result.PrecedingPartitionCount);
            Assert.Equal(documentProcess.FollowingSearchPartitionInclusionCount, result.FollowingPartitionCount);
        }
    }
}
