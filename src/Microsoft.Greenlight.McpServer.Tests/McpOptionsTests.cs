// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;

namespace Microsoft.Greenlight.McpServer.Tests;

public class McpOptionsTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        var options = new ServiceConfigurationOptions.McpOptions();
        
        Assert.False(options.DisableAuth);
        Assert.False(options.SecretEnabled);
        Assert.Null(options.SecretHeaderName);
        Assert.Null(options.SecretValue);
    }

    [Fact]
    public void ConfigurationBinding_WorksCorrectly()
    {
        var configDict = new Dictionary<string, string?>
        {
            ["ServiceConfiguration:Mcp:DisableAuth"] = "true",
            ["ServiceConfiguration:Mcp:SecretEnabled"] = "true",
            ["ServiceConfiguration:Mcp:SecretHeaderName"] = "X-Custom-Secret",
            ["ServiceConfiguration:Mcp:SecretValue"] = "my-secret-value"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.Configure<ServiceConfigurationOptions.McpOptions>(configuration.GetSection("ServiceConfiguration:Mcp"));

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ServiceConfigurationOptions.McpOptions>>().Value;

        Assert.True(options.DisableAuth);
        Assert.True(options.SecretEnabled);
        Assert.Equal("X-Custom-Secret", options.SecretHeaderName);
        Assert.Equal("my-secret-value", options.SecretValue);
    }

    [Fact]
    public void ConfigurationBinding_HandlesPartialConfig()
    {
        var configDict = new Dictionary<string, string?>
        {
            ["ServiceConfiguration:Mcp:SecretEnabled"] = "true",
            ["ServiceConfiguration:Mcp:SecretValue"] = "test-secret"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.Configure<ServiceConfigurationOptions.McpOptions>(configuration.GetSection("ServiceConfiguration:Mcp"));

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ServiceConfigurationOptions.McpOptions>>().Value;

        Assert.False(options.DisableAuth); // Should remain default
        Assert.True(options.SecretEnabled);
        Assert.Null(options.SecretHeaderName); // Should remain default
        Assert.Equal("test-secret", options.SecretValue);
    }

    [Fact]
    public void ConfigurationBinding_HandlesEmptyConfig()
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.Configure<ServiceConfigurationOptions.McpOptions>(configuration.GetSection("ServiceConfiguration:Mcp"));

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ServiceConfigurationOptions.McpOptions>>().Value;

        Assert.False(options.DisableAuth);
        Assert.False(options.SecretEnabled);
        Assert.Null(options.SecretHeaderName);
        Assert.Null(options.SecretValue);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    public void BooleanProperties_BindCorrectly(string configValue, bool expectedValue)
    {
        var configDict = new Dictionary<string, string?>
        {
            ["ServiceConfiguration:Mcp:DisableAuth"] = configValue,
            ["ServiceConfiguration:Mcp:SecretEnabled"] = configValue
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.Configure<ServiceConfigurationOptions.McpOptions>(configuration.GetSection("ServiceConfiguration:Mcp"));

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ServiceConfigurationOptions.McpOptions>>().Value;

        Assert.Equal(expectedValue, options.DisableAuth);
        Assert.Equal(expectedValue, options.SecretEnabled);
    }

    [Fact]
    public void ConfigurationBinding_HandlesInvalidBooleanValues()
    {
        var configDict = new Dictionary<string, string?>
        {
            ["ServiceConfiguration:Mcp:DisableAuth"] = "invalid-boolean",
            ["ServiceConfiguration:Mcp:SecretEnabled"] = "not-a-bool"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.Configure<ServiceConfigurationOptions.McpOptions>(configuration.GetSection("ServiceConfiguration:Mcp"));

        var serviceProvider = services.BuildServiceProvider();

        // Configuration binding should throw for invalid boolean values
        var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ServiceConfigurationOptions.McpOptions>>();
        Assert.Throws<InvalidOperationException>(() => optionsAccessor.Value);
    }
}