// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.DTO.SystemStatus;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Reindexing.Events;
using Microsoft.Greenlight.Shared.Enums;
using Xunit;

namespace Microsoft.Greenlight.Shared.Tests.SystemStatus;

public class SystemStatusIntegrationTests
{
    [Fact]
    public void SystemStatusWorkflow_CompleteReindexingScenario_WorksEndToEnd()
    {
        // This test demonstrates the full workflow of system status monitoring
        // from notification creation to status aggregation
        
        // Arrange - Simulate a reindexing operation lifecycle
        var libraryName = "TestDocumentLibrary";
        var orchestrationId = Guid.NewGuid().ToString();

        // Act & Assert - Step 1: Reindexing starts
        var startedNotification = new DocumentReindexStartedNotification(
            OrchestrationId: orchestrationId,
            DocumentLibraryOrProcessName: libraryName,
            Reason: "Schema validation detected incompatible changes"
        );

        var startContribution = startedNotification.GetStatusContribution();
        Assert.Equal("VectorStore", startContribution.Source);
        Assert.Equal(SystemStatusType.OperationStarted, startContribution.StatusType);
        Assert.Equal(libraryName, startContribution.ItemKey);
        Assert.Equal("Reindexing", startContribution.Status);
        Assert.Equal(SystemStatusSeverity.Info, startContribution.Severity);

        // Act & Assert - Step 2: Progress updates
        var progressNotification1 = new DocumentReindexProgressNotification(
            OrchestrationId: orchestrationId,
            DocumentLibraryOrProcessName: libraryName,
            TotalDocuments: 100,
            ProcessedDocuments: 25,
            FailedDocuments: 0
        );

        var progress1Contribution = progressNotification1.GetStatusContribution();
        Assert.Equal(SystemStatusType.ProgressUpdate, progress1Contribution.StatusType);
        Assert.Contains("25/100", progress1Contribution.StatusMessage);
        Assert.Contains("Progress:", progress1Contribution.StatusMessage);

        var progressNotification2 = new DocumentReindexProgressNotification(
            OrchestrationId: orchestrationId,
            DocumentLibraryOrProcessName: libraryName,
            TotalDocuments: 100,
            ProcessedDocuments: 75,
            FailedDocuments: 0
        );

        var progress2Contribution = progressNotification2.GetStatusContribution();
        Assert.Contains("75/100", progress2Contribution.StatusMessage);
        Assert.Contains("Progress:", progress2Contribution.StatusMessage);

        // Act & Assert - Step 3: Completion
        var completedNotification = new DocumentReindexCompletedNotification(
            OrchestrationId: orchestrationId,
            DocumentLibraryOrProcessName: libraryName,
            TotalDocuments: 100,
            ProcessedDocuments: 100,
            FailedDocuments: 0,
            Success: true
        );

        var completedContribution = completedNotification.GetStatusContribution();
        Assert.Equal(SystemStatusType.OperationCompleted, completedContribution.StatusType);
        Assert.Equal("Healthy", completedContribution.Status);
        Assert.Equal(SystemStatusSeverity.Info, completedContribution.Severity);
        Assert.Contains("completed:", completedContribution.StatusMessage);
        Assert.Contains("100/100", completedContribution.StatusMessage);
    }

    [Fact]
    public void SystemStatusWorkflow_FailedReindexingScenario_GeneratesCorrectAlerts()
    {
        // Arrange - Simulate a failed reindexing operation
        var libraryName = "ProblematicLibrary";
        var orchestrationId = Guid.NewGuid().ToString();
        var errorMessage = "Connection timeout to vector store after 30 seconds";

        // Act & Assert - Failed operation
        var failedNotification = new DocumentReindexFailedNotification(
            OrchestrationId: orchestrationId,
            DocumentLibraryOrProcessName: libraryName,
            ErrorMessage: errorMessage
        );

        var failedContribution = failedNotification.GetStatusContribution();
        
        // Verify the failure generates a critical status
        Assert.Equal("VectorStore", failedContribution.Source);
        Assert.Equal(SystemStatusType.OperationFailed, failedContribution.StatusType);
        Assert.Equal(libraryName, failedContribution.ItemKey);
        Assert.Equal("Error", failedContribution.Status);
        Assert.Equal(SystemStatusSeverity.Critical, failedContribution.Severity);
        Assert.Contains(errorMessage, failedContribution.StatusMessage);
        Assert.Contains("OrchestrationId", failedContribution.Properties);
        Assert.Contains("ErrorMessage", failedContribution.Properties);
    }

    [Fact]
    public void SystemStatusSnapshot_MultipleSubsystemsScenario_CalculatesCorrectOverallStatus()
    {
        // This test simulates a complete system status with multiple subsystems
        // to verify the aggregation logic works correctly

        // Arrange - Create a realistic system status scenario
        var now = DateTime.UtcNow;
        var systemStatus = new SystemStatusSnapshot
        {
            LastUpdatedUtc = now,
            OverallStatus = SystemHealthStatus.Warning, // Overall determined by worst subsystem
            Subsystems = new List<SubsystemStatus>
            {
                // Healthy vector store
                new SubsystemStatus
                {
                    Source = "VectorStore",
                    DisplayName = "Vector Store",
                    OverallStatus = SystemHealthStatus.Healthy,
                    Items = new List<ItemStatus>
                    {
                        new ItemStatus 
                        { 
                            ItemKey = "DocumentLibrary1", 
                            Status = "Healthy", 
                            Severity = SystemStatusSeverity.Info, 
                            LastUpdatedUtc = now,
                            StatusMessage = "Index schema up to date, 1,250 documents indexed"
                        },
                        new ItemStatus 
                        { 
                            ItemKey = "DocumentLibrary2", 
                            Status = "Healthy", 
                            Severity = SystemStatusSeverity.Info, 
                            LastUpdatedUtc = now,
                            StatusMessage = "Index schema up to date, 847 documents indexed"
                        }
                    },
                    LastUpdatedUtc = now
                },

                // Warning state worker threads
                new SubsystemStatus
                {
                    Source = "WorkerThreads",
                    DisplayName = "Worker Threads",
                    OverallStatus = SystemHealthStatus.Warning,
                    Items = new List<ItemStatus>
                    {
                        new ItemStatus 
                        { 
                            ItemKey = "IngestionWorker", 
                            Status = "Warning", 
                            Severity = SystemStatusSeverity.Warning, 
                            LastUpdatedUtc = now,
                            StatusMessage = "High queue depth: 45 documents pending processing"
                        },
                        new ItemStatus 
                        { 
                            ItemKey = "ValidationWorker", 
                            Status = "Healthy", 
                            Severity = SystemStatusSeverity.Info, 
                            LastUpdatedUtc = now,
                            StatusMessage = "Processing normally, 12 validations in progress"
                        }
                    },
                    LastUpdatedUtc = now
                },

                // Healthy ingestion system
                new SubsystemStatus
                {
                    Source = "Ingestion",
                    DisplayName = "Document Ingestion",
                    OverallStatus = SystemHealthStatus.Healthy,
                    Items = new List<ItemStatus>
                    {
                        new ItemStatus 
                        { 
                            ItemKey = "FileProcessor", 
                            Status = "Healthy", 
                            Severity = SystemStatusSeverity.Info, 
                            LastUpdatedUtc = now,
                            StatusMessage = "All file types supported, processing rate normal"
                        }
                    },
                    LastUpdatedUtc = now
                }
            },
            ActiveAlerts = new List<SystemAlert>
            {
                new SystemAlert
                {
                    Id = "worker-queue-depth-warning",
                    Severity = SystemStatusSeverity.Warning,
                    Title = "High Worker Queue Depth",
                    Message = "Ingestion worker queue depth is above normal thresholds (45 documents). Consider scaling up workers or investigating processing delays.",
                    Source = "WorkerThreads",
                    CreatedUtc = now.AddMinutes(-15),
                    Properties = new Dictionary<string, string>
                    {
                        ["QueueDepth"] = "45",
                        ["Threshold"] = "30",
                        ["WorkerType"] = "IngestionWorker"
                    }
                }
            }
        };

        // Act
        var summary = systemStatus.Summary;

        // Assert - Verify aggregated statistics
        Assert.Equal(3, summary.TotalSubsystems);
        Assert.Equal(2, summary.HealthySubsystems); // VectorStore and Ingestion
        Assert.Equal(1, summary.WarningSubsystems); // WorkerThreads
        Assert.Equal(0, summary.CriticalSubsystems);

        Assert.Equal(5, summary.TotalItems); // 2 + 2 + 1 items across subsystems
        Assert.Equal(4, summary.HealthyItems); // Info severity items
        Assert.Equal(1, summary.WarningItems); // Warning severity items
        Assert.Equal(0, summary.CriticalItems);

        Assert.Equal(1, summary.ActiveAlerts);
        Assert.Equal(0, summary.CriticalAlerts);
        Assert.Equal(1, summary.WarningAlerts);

        // Verify subsystem status counts work correctly
        var workerSubsystem = systemStatus.Subsystems.First(s => s.Source == "WorkerThreads");
        var statusCounts = workerSubsystem.StatusCounts;
        Assert.Equal(2, statusCounts.Count);
        Assert.Equal(1, statusCounts["Warning"]);
        Assert.Equal(1, statusCounts["Healthy"]);
    }

    [Fact]
    public void SystemStatusSnapshot_WithRecords_SupportsImmutableUpdates()
    {
        // This test verifies that the record-based DTOs support immutable updates
        // which is important for thread-safe status aggregation

        // Arrange
        var originalSnapshot = new SystemStatusSnapshot
        {
            LastUpdatedUtc = DateTime.UtcNow.AddMinutes(-10),
            OverallStatus = SystemHealthStatus.Healthy,
            Subsystems = new List<SubsystemStatus>(),
            ActiveAlerts = new List<SystemAlert>()
        };

        var originalItem = new ItemStatus
        {
            ItemKey = "TestItem",
            Status = "Processing",
            Severity = SystemStatusSeverity.Info,
            LastUpdatedUtc = DateTime.UtcNow.AddMinutes(-5),
            StatusMessage = "Processing document batch 1"
        };

        // Act - Create immutable updates using 'with' expressions
        var updatedSnapshot = originalSnapshot with 
        { 
            LastUpdatedUtc = DateTime.UtcNow,
            OverallStatus = SystemHealthStatus.Warning
        };

        var updatedItem = originalItem with 
        { 
            Status = "Warning",
            Severity = SystemStatusSeverity.Warning,
            LastUpdatedUtc = DateTime.UtcNow,
            StatusMessage = "Processing delayed: high memory usage detected"
        };

        // Assert - Verify original objects are unchanged (immutability)
        Assert.Equal(SystemHealthStatus.Healthy, originalSnapshot.OverallStatus);
        Assert.Equal("Processing", originalItem.Status);
        Assert.Equal(SystemStatusSeverity.Info, originalItem.Severity);

        // Assert - Verify updated objects have new values
        Assert.Equal(SystemHealthStatus.Warning, updatedSnapshot.OverallStatus);
        Assert.Equal("Warning", updatedItem.Status);
        Assert.Equal(SystemStatusSeverity.Warning, updatedItem.Severity);
        Assert.Contains("high memory usage", updatedItem.StatusMessage);

        // Assert - Verify unchanged properties remain the same
        Assert.Equal(originalItem.ItemKey, updatedItem.ItemKey);
        Assert.Same(originalSnapshot.Subsystems, updatedSnapshot.Subsystems); // Reference equality for unchanged collections
    }
}