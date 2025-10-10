// Copyright (c) Microsoft Corporation. All rights reserved.

using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models.FlowTasks;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Plugins;
using Moq;
using Orleans;

namespace Microsoft.Greenlight.Shared.Tests.Controllers;

/// <summary>
/// Tests for MCP Plugin Controller Flow integration.
/// </summary>
public sealed class McpPluginsControllerTests
{
    private readonly IMapper _mapper;

    public McpPluginsControllerTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<PluginMappingProfile>();
        });
        _mapper = config.CreateMapper();
    }

    private static McpPluginsController CreateController(DocGenerationDbContext dbContext, IMapper mapper)
    {
        return new McpPluginsController(
            dbContext,
            mapper,
            null!, // AzureFileHelper not used by Flow methods
            null!, // McpPluginManager not used by Flow methods
            null!, // IGrainFactory not used by Flow methods
            null); // ILogger nullable
    }

    [Fact]
    public async Task GetFlowExposedPlugins_ReturnsOnlyExposedPlugins()
    {
        // Arrange
        var dbContext = new DocGenerationDbContext(
            new DbContextOptionsBuilder<DocGenerationDbContext>()
                .UseInMemoryDatabase($"mcp_flow_exposed_{Guid.NewGuid()}")
                .Options);

        var exposedPlugin = new McpPlugin
        {
            Id = Guid.NewGuid(),
            Name = "ExposedPlugin",
            Description = "Plugin exposed to Flow",
            SourceType = McpPluginSourceType.AzureBlobStorage,
            ExposeToFlow = true
        };

        var notExposedPlugin = new McpPlugin
        {
            Id = Guid.NewGuid(),
            Name = "NotExposedPlugin",
            Description = "Plugin not exposed to Flow",
            SourceType = McpPluginSourceType.AzureBlobStorage,
            ExposeToFlow = false
        };

        dbContext.McpPlugins.AddRange(exposedPlugin, notExposedPlugin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, _mapper);

        // Act
        var result = await controller.GetFlowExposedPlugins();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var plugins = Assert.IsAssignableFrom<List<McpPluginInfo>>(okResult.Value);
        Assert.Single(plugins);
        Assert.Equal("ExposedPlugin", plugins[0].Name);
        Assert.True(plugins[0].ExposeToFlow);
    }

    [Fact]
    public async Task GetFlowExposedPlugins_ReturnsEmptyList_WhenNoPluginsExposed()
    {
        // Arrange
        var dbContext = new DocGenerationDbContext(
            new DbContextOptionsBuilder<DocGenerationDbContext>()
                .UseInMemoryDatabase($"mcp_flow_none_{Guid.NewGuid()}")
                .Options);

        var notExposedPlugin = new McpPlugin
        {
            Id = Guid.NewGuid(),
            Name = "NotExposedPlugin",
            SourceType = McpPluginSourceType.AzureBlobStorage,
            ExposeToFlow = false
        };

        dbContext.McpPlugins.Add(notExposedPlugin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, _mapper);

        // Act
        var result = await controller.GetFlowExposedPlugins();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var plugins = Assert.IsAssignableFrom<List<McpPluginInfo>>(okResult.Value);
        Assert.Empty(plugins);
    }

    [Fact]
    public async Task UpdateExposeToFlow_EnablesFlowIntegration_WhenValid()
    {
        // Arrange
        var dbContext = new DocGenerationDbContext(
            new DbContextOptionsBuilder<DocGenerationDbContext>()
                .UseInMemoryDatabase($"mcp_flow_enable_{Guid.NewGuid()}")
                .Options);

        var plugin = new McpPlugin
        {
            Id = Guid.NewGuid(),
            Name = "TestPlugin",
            SourceType = McpPluginSourceType.AzureBlobStorage,
            ExposeToFlow = false
        };

        dbContext.McpPlugins.Add(plugin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, _mapper);

        // Act
        var result = await controller.UpdateExposeToFlow(plugin.Id, true);

        // Assert
        Assert.IsType<NoContentResult>(result);

        // Verify database was updated
        var updatedPlugin = await dbContext.McpPlugins.FindAsync(plugin.Id);
        Assert.NotNull(updatedPlugin);
        Assert.True(updatedPlugin.ExposeToFlow);
    }

    [Fact]
    public async Task UpdateExposeToFlow_ReturnsNotFound_WhenPluginDoesNotExist()
    {
        // Arrange
        var dbContext = new DocGenerationDbContext(
            new DbContextOptionsBuilder<DocGenerationDbContext>()
                .UseInMemoryDatabase($"mcp_flow_notfound_{Guid.NewGuid()}")
                .Options);

        var controller = CreateController(dbContext, _mapper);

        // Act
        var result = await controller.UpdateExposeToFlow(Guid.NewGuid(), true);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFoundResult.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateExposeToFlow_ReturnsBadRequest_WhenPluginUsedInFlowTaskTemplate()
    {
        // Arrange
        var dbContext = new DocGenerationDbContext(
            new DbContextOptionsBuilder<DocGenerationDbContext>()
                .UseInMemoryDatabase($"mcp_flow_inuse_{Guid.NewGuid()}")
                .Options);

        var plugin = new McpPlugin
        {
            Id = Guid.NewGuid(),
            Name = "UsedPlugin",
            SourceType = McpPluginSourceType.AzureBlobStorage,
            ExposeToFlow = true
        };

        var template = new FlowTaskTemplate
        {
            Id = Guid.NewGuid(),
            Name = "TestTemplate",
            DisplayName = "Test Template",
            Description = "Test",
            Category = "Test",
            InitialPrompt = "Test prompt"
        };

        var dataSource = new FlowTaskMcpToolDataSource
        {
            Id = Guid.NewGuid(),
            FlowTaskTemplateId = template.Id,
            FlowTaskTemplate = template,
            McpPluginId = plugin.Id,
            McpPlugin = plugin,
            ToolName = "TestTool"
        };

        dbContext.McpPlugins.Add(plugin);
        dbContext.FlowTaskTemplates.Add(template);
        dbContext.Set<FlowTaskMcpToolDataSource>().Add(dataSource);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, _mapper);

        // Act - try to disable Flow integration
        var result = await controller.UpdateExposeToFlow(plugin.Id, false);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("cannot disable", badRequestResult.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Test Template", badRequestResult.Value?.ToString(), StringComparison.OrdinalIgnoreCase);

        // Verify plugin state unchanged
        var unchangedPlugin = await dbContext.McpPlugins.FindAsync(plugin.Id);
        Assert.NotNull(unchangedPlugin);
        Assert.True(unchangedPlugin.ExposeToFlow);
    }

    [Fact]
    public async Task UpdateExposeToFlow_AllowsDisable_WhenPluginNotUsedInTemplates()
    {
        // Arrange
        var dbContext = new DocGenerationDbContext(
            new DbContextOptionsBuilder<DocGenerationDbContext>()
                .UseInMemoryDatabase($"mcp_flow_disable_{Guid.NewGuid()}")
                .Options);

        var plugin = new McpPlugin
        {
            Id = Guid.NewGuid(),
            Name = "UnusedPlugin",
            SourceType = McpPluginSourceType.AzureBlobStorage,
            ExposeToFlow = true
        };

        dbContext.McpPlugins.Add(plugin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, _mapper);

        // Act
        var result = await controller.UpdateExposeToFlow(plugin.Id, false);

        // Assert
        Assert.IsType<NoContentResult>(result);

        // Verify database was updated
        var updatedPlugin = await dbContext.McpPlugins.FindAsync(plugin.Id);
        Assert.NotNull(updatedPlugin);
        Assert.False(updatedPlugin.ExposeToFlow);
    }
}
