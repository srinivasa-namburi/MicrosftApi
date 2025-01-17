using Microsoft.Extensions.Configuration;
using Microsoft.Greenlight.Shared.Helpers;
using Moq;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Tests.Helpers
{
    [Collection("Tests that call AdminHelper.Initialize")]
    public sealed class AdminHelperTests : IDisposable
    {
        private readonly Mock<IConfiguration> _mockConfiguration;

        public AdminHelperTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
        }

        public void Dispose()
        {
            AdminHelper.Initialize(null);
        }

        [Fact]
        public void IsRunningInProduction_WhenHelperNotInitialized_ShouldThrowInvalidOperationException()
        {
            // Act & Assert
            AdminHelper.Initialize(null);
            Assert.Throws<InvalidOperationException>(() => AdminHelper.IsRunningInProduction());
        }

        [Fact]
        public void IsRunningInProduction_WhenContainerAppEnvIsSet_ShouldReturnTrue()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["CONTAINER_APP_ENV"]).Returns("some_value");
            AdminHelper.Initialize(_mockConfiguration.Object);

            // Act
            var result = AdminHelper.IsRunningInProduction();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsRunningInProduction_WhenWebsiteInstanceIdIsSet_ShouldReturnTrue()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["WEBSITE_INSTANCE_ID"]).Returns("some_value");
            AdminHelper.Initialize(_mockConfiguration.Object);

            // Act
            var result = AdminHelper.IsRunningInProduction();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsRunningInProduction_WhenEnvironmentIsProduction_ShouldReturnTrue()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Production");
            AdminHelper.Initialize(_mockConfiguration.Object);

            // Act
            var result = AdminHelper.IsRunningInProduction();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsRunningInProduction_WhenWebDocgenHttpsIsNotLocalhost_ShouldReturnTrue()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["services__web-docgen__https__0"]).Returns("https://example.com");
            AdminHelper.Initialize(_mockConfiguration.Object);

            // Act
            var result = AdminHelper.IsRunningInProduction();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsRunningInProduction_WhenApiMainHttpsIsNotLocalhost_ShouldReturnTrue()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["services__api-main__https__0"]).Returns("https://example.com");
            AdminHelper.Initialize(_mockConfiguration.Object);

            // Act
            var result = AdminHelper.IsRunningInProduction();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsRunningInProduction_WhenWebDocGenContainsLocalHost_ShouldReturnFalse()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["services__web-docgen__https__0"]).Returns("https://localhost:3000.com");
            AdminHelper.Initialize(_mockConfiguration.Object);

            // Act
            var result = AdminHelper.IsRunningInProduction();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsRunningInProduction_WhenApiMainHttpsContainsLocalhost_ShouldReturnFalse()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["services__api-main__https__0"]).Returns("https://localhost:3000.com");
            AdminHelper.Initialize(_mockConfiguration.Object);

            // Act
            var result = AdminHelper.IsRunningInProduction();

            // Assert
            Assert.False(result);
        }
    }
}
