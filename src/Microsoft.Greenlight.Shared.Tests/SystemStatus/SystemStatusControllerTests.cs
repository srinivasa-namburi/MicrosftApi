// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO.SystemStatus;
using Microsoft.Greenlight.Shared.Enums;
using Moq;
using Orleans;
using Xunit;

namespace Microsoft.Greenlight.Shared.Tests.SystemStatus;

public class SystemStatusControllerTests
{
    private readonly Mock<IClusterClient> _mockClusterClient;
    private readonly Mock<ILogger<SystemStatusController>> _mockLogger;
    private readonly Mock<ISystemStatusAggregatorGrain> _mockAggregatorGrain;
    private readonly SystemStatusController _controller;

    public SystemStatusControllerTests()
    {
        _mockClusterClient = new Mock<IClusterClient>();
        _mockLogger = new Mock<ILogger<SystemStatusController>>();
        _mockAggregatorGrain = new Mock<ISystemStatusAggregatorGrain>();
        
        _mockClusterClient
            .Setup(x => x.GetGrain<ISystemStatusAggregatorGrain>(Guid.Empty, null))
            .Returns(_mockAggregatorGrain.Object);

        _controller = new SystemStatusController(_mockClusterClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetSystemStatusAsync_WhenGrainReturnsData_ReturnsOkResult()
    {
        // Arrange
        var expectedSnapshot = new SystemStatusSnapshot
        {
            LastUpdatedUtc = DateTime.UtcNow,
            OverallStatus = SystemHealthStatus.Healthy,
            Subsystems = new List<SubsystemStatus>
            {
                new SubsystemStatus
                {
                    Source = "VectorStore",
                    DisplayName = "Vector Store",
                    OverallStatus = SystemHealthStatus.Healthy,
                    Items = new List<ItemStatus>(),
                    LastUpdatedUtc = DateTime.UtcNow
                }
            },
            ActiveAlerts = new List<SystemAlert>()
        };

        _mockAggregatorGrain
            .Setup(x => x.GetSystemStatusAsync())
            .ReturnsAsync(expectedSnapshot);

        // Act
        var result = await _controller.GetSystemStatusAsync();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSnapshot = Assert.IsType<SystemStatusSnapshot>(okResult.Value);
        Assert.Equal(expectedSnapshot.OverallStatus, returnedSnapshot.OverallStatus);
        Assert.Equal(expectedSnapshot.Subsystems.Count, returnedSnapshot.Subsystems.Count);
        Assert.Equal(expectedSnapshot.ActiveAlerts.Count, returnedSnapshot.ActiveAlerts.Count);
    }

    [Fact]
    public async Task GetSystemStatusAsync_WhenExceptionOccurs_ReturnsInternalServerError()
    {
        // Arrange
        _mockAggregatorGrain
            .Setup(x => x.GetSystemStatusAsync())
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        var result = await _controller.GetSystemStatusAsync();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("Failed to get system status", statusResult.Value);
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to get system status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSubsystemStatusAsync_WhenGrainReturnsData_ReturnsOkResult()
    {
        // Arrange
        var source = "VectorStore";
        var expectedSubsystem = new SubsystemStatus
        {
            Source = source,
            DisplayName = "Vector Store",
            OverallStatus = SystemHealthStatus.Warning,
            Items = new List<ItemStatus>
            {
                new ItemStatus
                {
                    ItemKey = "Index1",
                    Status = "Reindexing",
                    Severity = SystemStatusSeverity.Warning,
                    LastUpdatedUtc = DateTime.UtcNow
                }
            },
            LastUpdatedUtc = DateTime.UtcNow
        };

        _mockAggregatorGrain
            .Setup(x => x.GetSubsystemStatusAsync(source))
            .ReturnsAsync(expectedSubsystem);

        // Act
        var result = await _controller.GetSubsystemStatusAsync(source);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSubsystem = Assert.IsType<SubsystemStatus>(okResult.Value);
        Assert.Equal(expectedSubsystem.Source, returnedSubsystem.Source);
        Assert.Equal(expectedSubsystem.DisplayName, returnedSubsystem.DisplayName);
        Assert.Equal(expectedSubsystem.OverallStatus, returnedSubsystem.OverallStatus);
        Assert.Equal(expectedSubsystem.Items.Count, returnedSubsystem.Items.Count);
    }

    [Fact]
    public async Task GetSubsystemStatusAsync_WhenGrainReturnsNull_ReturnsOkWithNull()
    {
        // Arrange
        var source = "NonExistentSubsystem";
        
        _mockAggregatorGrain
            .Setup(x => x.GetSubsystemStatusAsync(source))
            .ReturnsAsync((SubsystemStatus?)null);

        // Act
        var result = await _controller.GetSubsystemStatusAsync(source);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Null(okResult.Value);
    }

    [Fact]
    public async Task GetSubsystemStatusAsync_WhenExceptionOccurs_ReturnsInternalServerError()
    {
        // Arrange
        var source = "VectorStore";
        
        _mockAggregatorGrain
            .Setup(x => x.GetSubsystemStatusAsync(source))
            .ThrowsAsync(new TimeoutException("Grain timeout"));

        // Act
        var result = await _controller.GetSubsystemStatusAsync(source);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("Failed to get subsystem status", statusResult.Value);
        
        // Verify logging with source parameter
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to get subsystem status for VectorStore")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("VectorStore")]
    [InlineData("WorkerThreads")]
    [InlineData("Ingestion")]
    [InlineData("")]
    [InlineData("NonExistent")]
    public async Task GetSubsystemStatusAsync_WithVariousSources_CallsGrainCorrectly(string source)
    {
        // Arrange
        _mockAggregatorGrain
            .Setup(x => x.GetSubsystemStatusAsync(source))
            .ReturnsAsync((SubsystemStatus?)null);

        // Act
        await _controller.GetSubsystemStatusAsync(source);

        // Assert
        _mockAggregatorGrain.Verify(x => x.GetSubsystemStatusAsync(source), Times.Once);
    }
}