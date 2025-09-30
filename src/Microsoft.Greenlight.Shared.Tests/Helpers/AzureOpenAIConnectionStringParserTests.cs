using Microsoft.Greenlight.Shared.Helpers;
using Xunit;

namespace Microsoft.Greenlight.Shared.Tests.Helpers;

public class AzureOpenAIConnectionStringParserTests
{
    [Theory]
    [InlineData("Endpoint=https://example.openai.azure.com/;Key=abc123", "https://example.openai.azure.com/", "abc123")]
    [InlineData("Endpoint=https://example.openai.azure.com;Key=abc123", "https://example.openai.azure.com/", "abc123")] // No trailing slash
    [InlineData("Endpoint=https://example.openai.azure.com/;Key=", "https://example.openai.azure.com/", null)]
    [InlineData("Endpoint=https://example.openai.azure.com/", "https://example.openai.azure.com/", null)]
    [InlineData("Endpoint=https://example.openai.azure.com", "https://example.openai.azure.com/", null)] // No trailing slash
    [InlineData("https://example.openai.azure.com/", "https://example.openai.azure.com/", null)]
    [InlineData("https://example.openai.azure.com", "https://example.openai.azure.com/", null)] // No trailing slash
    public void Parse_ValidConnectionStrings_ReturnsCorrectInfo(string connectionString, string expectedEndpoint, string? expectedKey)
    {
        // Act
        var result = AzureOpenAIConnectionStringParser.Parse(connectionString);
        
        // Assert
        Assert.Equal(expectedEndpoint, result.Endpoint);
        Assert.Equal(expectedKey, result.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Endpoint=;Key=abc")]
    [InlineData("Key=abc123")]
    [InlineData("invalid-url")]
    [InlineData("Endpoint=not-a-url;Key=abc")]
    public void Parse_InvalidConnectionStrings_ThrowsArgumentException(string? connectionString)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => AzureOpenAIConnectionStringParser.Parse(connectionString));
    }
}