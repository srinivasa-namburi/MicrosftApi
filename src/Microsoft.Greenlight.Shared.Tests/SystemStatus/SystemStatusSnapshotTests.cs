// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.DTO.SystemStatus;
using Microsoft.Greenlight.Shared.Enums;
using Xunit;

namespace Microsoft.Greenlight.Shared.Tests.SystemStatus;

public class SystemStatusSnapshotTests
{
    [Fact]
    public void SystemStatusSnapshot_Summary_CalculatesCorrectStatistics()
    {
        // Arrange
        var systemStatus = new SystemStatusSnapshot
        {
            LastUpdatedUtc = DateTime.UtcNow,
            OverallStatus = SystemHealthStatus.Warning,
            Subsystems = new List<SubsystemStatus>
            {
                new SubsystemStatus
                {
                    Source = "VectorStore",
                    DisplayName = "Vector Store",
                    OverallStatus = SystemHealthStatus.Healthy,
                    Items = new List<ItemStatus>
                    {
                        new ItemStatus { ItemKey = "Index1", Status = "Healthy", Severity = SystemStatusSeverity.Info, LastUpdatedUtc = DateTime.UtcNow },
                        new ItemStatus { ItemKey = "Index2", Status = "Healthy", Severity = SystemStatusSeverity.Info, LastUpdatedUtc = DateTime.UtcNow }
                    },
                    LastUpdatedUtc = DateTime.UtcNow
                },
                new SubsystemStatus
                {
                    Source = "WorkerThreads",
                    DisplayName = "Worker Threads",
                    OverallStatus = SystemHealthStatus.Warning,
                    Items = new List<ItemStatus>
                    {
                        new ItemStatus { ItemKey = "Thread1", Status = "Warning", Severity = SystemStatusSeverity.Warning, LastUpdatedUtc = DateTime.UtcNow },
                        new ItemStatus { ItemKey = "Thread2", Status = "Critical", Severity = SystemStatusSeverity.Critical, LastUpdatedUtc = DateTime.UtcNow }
                    },
                    LastUpdatedUtc = DateTime.UtcNow
                }
            },
            ActiveAlerts = new List<SystemAlert>
            {
                new SystemAlert 
                { 
                    Id = "alert1", 
                    Severity = SystemStatusSeverity.Warning, 
                    Title = "Test Warning", 
                    Message = "Warning message", 
                    Source = "Test", 
                    CreatedUtc = DateTime.UtcNow 
                },
                new SystemAlert 
                { 
                    Id = "alert2", 
                    Severity = SystemStatusSeverity.Critical, 
                    Title = "Test Critical", 
                    Message = "Critical message", 
                    Source = "Test", 
                    CreatedUtc = DateTime.UtcNow 
                }
            }
        };

        // Act
        var summary = systemStatus.Summary;

        // Assert
        Assert.Equal(2, summary.TotalSubsystems);
        Assert.Equal(1, summary.HealthySubsystems);
        Assert.Equal(1, summary.WarningSubsystems);
        Assert.Equal(0, summary.CriticalSubsystems);
        
        Assert.Equal(4, summary.TotalItems);
        Assert.Equal(2, summary.HealthyItems);
        Assert.Equal(1, summary.WarningItems);
        Assert.Equal(1, summary.CriticalItems);
        
        Assert.Equal(2, summary.ActiveAlerts);
        Assert.Equal(1, summary.CriticalAlerts);
        Assert.Equal(1, summary.WarningAlerts);
    }

    [Fact]
    public void SystemStatusSnapshot_Summary_HandlesEmptySubsystems()
    {
        // Arrange
        var systemStatus = new SystemStatusSnapshot
        {
            LastUpdatedUtc = DateTime.UtcNow,
            OverallStatus = SystemHealthStatus.Healthy,
            Subsystems = new List<SubsystemStatus>(),
            ActiveAlerts = new List<SystemAlert>()
        };

        // Act
        var summary = systemStatus.Summary;

        // Assert
        Assert.Equal(0, summary.TotalSubsystems);
        Assert.Equal(0, summary.HealthySubsystems);
        Assert.Equal(0, summary.WarningSubsystems);
        Assert.Equal(0, summary.CriticalSubsystems);
        
        Assert.Equal(0, summary.TotalItems);
        Assert.Equal(0, summary.HealthyItems);
        Assert.Equal(0, summary.WarningItems);
        Assert.Equal(0, summary.CriticalItems);
        
        Assert.Equal(0, summary.ActiveAlerts);
        Assert.Equal(0, summary.CriticalAlerts);
        Assert.Equal(0, summary.WarningAlerts);
    }

    [Fact]
    public void SubsystemStatus_StatusCounts_CalculatesCorrectCounts()
    {
        // Arrange
        var subsystem = new SubsystemStatus
        {
            Source = "Test",
            DisplayName = "Test Subsystem",
            OverallStatus = SystemHealthStatus.Warning,
            Items = new List<ItemStatus>
            {
                new ItemStatus { ItemKey = "Item1", Status = "Healthy", Severity = SystemStatusSeverity.Info, LastUpdatedUtc = DateTime.UtcNow },
                new ItemStatus { ItemKey = "Item2", Status = "Healthy", Severity = SystemStatusSeverity.Info, LastUpdatedUtc = DateTime.UtcNow },
                new ItemStatus { ItemKey = "Item3", Status = "Warning", Severity = SystemStatusSeverity.Warning, LastUpdatedUtc = DateTime.UtcNow },
                new ItemStatus { ItemKey = "Item4", Status = "Failed", Severity = SystemStatusSeverity.Critical, LastUpdatedUtc = DateTime.UtcNow }
            },
            LastUpdatedUtc = DateTime.UtcNow
        };

        // Act
        var statusCounts = subsystem.StatusCounts;

        // Assert
        Assert.Equal(3, statusCounts.Count);
        Assert.Equal(2, statusCounts["Healthy"]);
        Assert.Equal(1, statusCounts["Warning"]);
        Assert.Equal(1, statusCounts["Failed"]);
    }

    [Fact]
    public void ItemStatus_DefaultProperties_IsNotNull()
    {
        // Arrange & Act
        var item = new ItemStatus
        {
            ItemKey = "Test",
            Status = "Test Status",
            Severity = SystemStatusSeverity.Info,
            LastUpdatedUtc = DateTime.UtcNow
        };

        // Assert
        Assert.NotNull(item.Properties);
        Assert.Empty(item.Properties);
    }

    [Fact]
    public void SystemAlert_DefaultProperties_IsNotNull()
    {
        // Arrange & Act
        var alert = new SystemAlert
        {
            Id = "test-alert",
            Severity = SystemStatusSeverity.Warning,
            Title = "Test Alert",
            Message = "Test message",
            Source = "Test",
            CreatedUtc = DateTime.UtcNow
        };

        // Assert
        Assert.NotNull(alert.Properties);
        Assert.Empty(alert.Properties);
    }

    [Fact]
    public void SystemStatusSnapshot_WithRecordSyntax_SupportsWithModification()
    {
        // Arrange
        var originalSnapshot = new SystemStatusSnapshot
        {
            LastUpdatedUtc = DateTime.UtcNow.AddMinutes(-5),
            OverallStatus = SystemHealthStatus.Healthy,
            Subsystems = new List<SubsystemStatus>(),
            ActiveAlerts = new List<SystemAlert>()
        };

        // Act
        var updatedSnapshot = originalSnapshot with 
        { 
            LastUpdatedUtc = DateTime.UtcNow,
            OverallStatus = SystemHealthStatus.Warning
        };

        // Assert
        Assert.NotEqual(originalSnapshot.LastUpdatedUtc, updatedSnapshot.LastUpdatedUtc);
        Assert.NotEqual(originalSnapshot.OverallStatus, updatedSnapshot.OverallStatus);
        Assert.Equal(SystemHealthStatus.Warning, updatedSnapshot.OverallStatus);
        Assert.Same(originalSnapshot.Subsystems, updatedSnapshot.Subsystems); // Reference equality for unchanged properties
    }
}