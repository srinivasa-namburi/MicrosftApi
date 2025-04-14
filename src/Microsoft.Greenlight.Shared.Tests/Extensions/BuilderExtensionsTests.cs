using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Extensions;
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
    }
}
