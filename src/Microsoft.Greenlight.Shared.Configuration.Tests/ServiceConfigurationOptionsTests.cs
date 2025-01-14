namespace Microsoft.Greenlight.Shared.Configuration.Tests
{
    public class ServiceConfigurationOptionsTests
    {
        [Fact]
        public void Gpt4o_Or_Gpt4128KDeploymentName_When4oEmpty_ReturnsGPT4128K()
        {
            // Arrange
            var expectedValue = "value";
            var options = new ServiceConfigurationOptions.OpenAiOptions
            {
                GPT4oModelDeploymentName = string.Empty,
                GPT4128KModelDeploymentName = "value"
            };

            // Act
            var result = options.Gpt4o_Or_Gpt4128KDeploymentName;

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public void Gpt4o_Or_Gpt4128KDeploymentName_When4oIsNotEmpty_ReturnsGPT4o()
        {
            // Arrange
            var expectedValue = "value1";
            var options = new ServiceConfigurationOptions.OpenAiOptions
            {
                GPT4oModelDeploymentName = expectedValue,
                GPT4128KModelDeploymentName = "value2"
            };

            // Act
            var result = options.Gpt4o_Or_Gpt4128KDeploymentName;

            // Assert
            Assert.Equal(expectedValue, result);
        }
    }
}