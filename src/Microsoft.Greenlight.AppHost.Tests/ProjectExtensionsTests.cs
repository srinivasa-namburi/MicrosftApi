using Assert = Xunit.Assert;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using Aspire.Hosting;

namespace Microsoft.Greenlight.AppHost.Tests
{
    public class ProjectExtensionsTests
    {
        [Fact]
        public void WithConfigSection_Throws_WhenProjectIsNull()
        {
            // Arrange
            var fakeConfigSection = new Mock<IConfigurationSection>().Object;

            // Act
#pragma warning disable CS8625 // Intentionally testing for null
            void testCode() => ProjectExtensions.WithConfigSection(null, fakeConfigSection);
#pragma warning restore CS8625
            var actual = Record.Exception(testCode);

            // Assert
            Assert.NotNull(actual);
            Assert.IsType<ArgumentNullException>(actual);
        }

        [Fact]
        public void WithConfigSection_Throws_WhenConfigSectionIsNull()
        {
            // Arrange
            var fakeBuilder = new Mock<IResourceBuilder<ProjectResource>>().Object;

            // Act
#pragma warning disable CS8625 // Intentionally testing for null
            void testCode() => ProjectExtensions.WithConfigSection(fakeBuilder, null);
#pragma warning restore CS8625
            var actual = Record.Exception(testCode);

            // Assert
            Assert.NotNull(actual);
            Assert.IsType<ArgumentNullException>(actual);
        }

        [Fact]
        public void WithConfigSection_AddsKey_WhenSectionIsLeaf()
        {
            // Arrange
            string expectedKey = "key";
            string expectedValue = "value";

            var fakeConfigSectionMock = new Mock<IConfigurationSection>();
            fakeConfigSectionMock.SetupGet(m => m.Key).Returns(expectedKey);
            fakeConfigSectionMock.SetupGet(m => m.Value).Returns(expectedValue);

            // The Project to use is irrelevant as long as that when it builds it implements the IProjectMetadata
            // interface. We just need a IResourceBuilder<ProjectResource> to test the extension method.
            var unitUnderTest =
                DistributedApplication.CreateBuilder()
                                      .AddProject<Projects.Microsoft_Greenlight_API_Main>("test");

            // Act
            unitUnderTest.WithConfigSection(fakeConfigSectionMock.Object);
#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
            var envVariables =
                unitUnderTest.Resource
                             .GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish)
                             .Result; // this is a non-blocking synchronous call
#pragma warning restore xUnit1031 // Do not use blocking task operations in test method

            // Assert
            Assert.True(envVariables.ContainsKey(expectedKey));
            Assert.True(envVariables[expectedKey] == expectedValue);
        }

        [Fact]
        public void WithConfigSection_AddsFullKey_WhenSectionIsBranch()
        {
            // Arrange
            string expectedFirstKey = "key";
            string expectedSecondKey = "key2";
            string expectedValue = "value";

            var fakeSecondConfigSectionMock = new Mock<IConfigurationSection>();
            fakeSecondConfigSectionMock.SetupGet(m => m.Key).Returns(expectedSecondKey);
            fakeSecondConfigSectionMock.SetupGet(m => m.Value).Returns(expectedValue);

            var fakeConfigSectionMock = new Mock<IConfigurationSection>();
            fakeConfigSectionMock.SetupGet(m => m.Key).Returns(expectedFirstKey);
            fakeConfigSectionMock.SetupGet(m => m.Value).Returns((string?)null);
            fakeConfigSectionMock.Setup(m => m.GetChildren()).Returns([fakeSecondConfigSectionMock.Object]);

            // The Project to use is irrelevant as long as that when it builds it implements the IProjectMetadata
            // interface. We just need a IResourceBuilder<ProjectResource> to test the extension method.
            var unitUnderTest =
                DistributedApplication.CreateBuilder()
                                      .AddProject<Projects.Microsoft_Greenlight_API_Main>("test");

            // Act
            unitUnderTest.WithConfigSection(fakeConfigSectionMock.Object);
#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
            var envVariables =
                unitUnderTest.Resource
                             .GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish)
                             .Result; // this is a non-blocking synchronous call
#pragma warning restore xUnit1031 // Do not use blocking task operations in test method
            var expectedKey = $"{expectedFirstKey}__{expectedSecondKey}";

            // Assert
            Assert.True(envVariables.ContainsKey(expectedKey));
            Assert.True(envVariables[expectedKey] == expectedValue);
        }
    }
}