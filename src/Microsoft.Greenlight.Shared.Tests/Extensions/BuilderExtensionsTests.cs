using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.KernelMemory;
using Moq;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Shared.Tests.Extensions
{
    public class BuilderExtensionsTests
    {
        private readonly Mock<IDocumentProcessInfoService> _mockDocumentProcessInfoService;

        public BuilderExtensionsTests()
        {
            _mockDocumentProcessInfoService = new Mock<IDocumentProcessInfoService>();

            IServiceCollection serviceCollection = new ServiceCollection()
                .AddScoped(provider => _mockDocumentProcessInfoService.Object);
        }

        [Fact]
        public void GetRequiredServiceForDocumentProcess_WhenServiceExists_ShouldReturnService()
        {
            // Arrange
            var documentProcessInfo = new DocumentProcessInfo { ShortName = "TestProcess" };
            _mockDocumentProcessInfoService.Setup(s => s.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>())).ReturnsAsync(documentProcessInfo);
            var mockService = new Mock<IKernelMemory>();

            IServiceCollection serviceCollection = new ServiceCollection()
                .AddScoped(provider => _mockDocumentProcessInfoService.Object)
                .AddScoped(provider => mockService.Object);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act
            var result = serviceProvider.GetRequiredServiceForDocumentProcess<IKernelMemory>(documentProcessInfo);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(mockService.Object,result);
        }

        [Fact]
        public void GetRequiredServiceForDocumentProcess_WhenServiceDoesNotExist_ShouldThrowException()
        {
            // Arrange
            var documentProcessInfo = new DocumentProcessInfo { ShortName = "TestProcess" };
            _mockDocumentProcessInfoService.Setup(s => s.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>())).ReturnsAsync(documentProcessInfo);

            IServiceCollection serviceCollection = new ServiceCollection()
                .AddScoped(provider => _mockDocumentProcessInfoService.Object);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => serviceProvider.GetRequiredServiceForDocumentProcess<IKernelMemory>(documentProcessInfo) );
        }

        [Fact]
        public void GetServiceForDocumentProcess_WhenServiceExists_ShouldReturnService()
        {
            // Arrange
            var documentProcessInfo = new DocumentProcessInfo { ShortName = "TestProcess" };
            _mockDocumentProcessInfoService.Setup(s => s.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>())).ReturnsAsync(documentProcessInfo);
            var mockService = new Mock<IKernelMemory>();

            IServiceCollection serviceCollection = new ServiceCollection()
                .AddScoped(provider => _mockDocumentProcessInfoService.Object)
                .AddScoped(provider => mockService.Object);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act
            var result = serviceProvider.GetServiceForDocumentProcess<IKernelMemory>(documentProcessInfo);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(mockService.Object, result);
        }

        [Fact]
        public void GetServiceForDocumentProcess_WhenServiceDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var documentProcessInfo = new DocumentProcessInfo { ShortName = "TestProcess" };
            _mockDocumentProcessInfoService.Setup(s => s.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>())).ReturnsAsync(documentProcessInfo);
            IServiceCollection serviceCollection = new ServiceCollection()
                .AddScoped(provider => _mockDocumentProcessInfoService.Object);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act
            var result = serviceProvider.GetServiceForDocumentProcess<IKernelMemory>(documentProcessInfo);

            // Assert
            Assert.Null(result);
        }
    }
}
