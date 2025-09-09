// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Reindexing.Events;
using Microsoft.Greenlight.Shared.Enums;
using Xunit;

namespace Microsoft.Greenlight.Shared.Tests.SystemStatus;

public class SystemStatusNotificationTests
{
    [Fact]
    public void DocumentReindexStartedNotification_GetStatusContribution_ReturnsCorrectContribution()
    {
        // Arrange
        var notification = new DocumentReindexStartedNotification(
            OrchestrationId: "test-orchestration-123",
            DocumentLibraryOrProcessName: "TestLibrary",
            Reason: "Schema validation required"
        );

        // Act
        var contribution = notification.GetStatusContribution();

        // Assert
        Assert.Equal("VectorStore", contribution.Source);
        Assert.Equal(SystemStatusType.OperationStarted, contribution.StatusType);
        Assert.Equal("TestLibrary", contribution.ItemKey);
        Assert.Equal("Reindexing", contribution.Status);
        Assert.Equal(SystemStatusSeverity.Info, contribution.Severity);
        Assert.Equal("Started reindexing: Schema validation required", contribution.StatusMessage);
        Assert.Contains("OrchestrationId", contribution.Properties);
        Assert.Contains("Reason", contribution.Properties);
        Assert.Equal("test-orchestration-123", contribution.Properties["OrchestrationId"]);
        Assert.Equal("Schema validation required", contribution.Properties["Reason"]);
    }

    [Fact]
    public void DocumentReindexProgressNotification_GetStatusContribution_ReturnsCorrectContribution()
    {
        // Arrange
        var notification = new DocumentReindexProgressNotification(
            OrchestrationId: "test-orchestration-123",
            DocumentLibraryOrProcessName: "TestLibrary",
            TotalDocuments: 100,
            ProcessedDocuments: 50,
            FailedDocuments: 0
        );

        // Act
        var contribution = notification.GetStatusContribution();

        // Assert
        Assert.Equal("VectorStore", contribution.Source);
        Assert.Equal(SystemStatusType.ProgressUpdate, contribution.StatusType);
        Assert.Equal("TestLibrary", contribution.ItemKey);
        Assert.Equal("Reindexing", contribution.Status);
        Assert.Equal(SystemStatusSeverity.Info, contribution.Severity);
        Assert.Contains("50/100", contribution.StatusMessage);
        Assert.Contains("OrchestrationId", contribution.Properties);
        Assert.Contains("ProcessedDocuments", contribution.Properties);
        Assert.Contains("TotalDocuments", contribution.Properties);
        Assert.Contains("FailedDocuments", contribution.Properties);
        Assert.Equal("test-orchestration-123", contribution.Properties["OrchestrationId"]);
        Assert.Equal("50", contribution.Properties["ProcessedDocuments"]);
        Assert.Equal("100", contribution.Properties["TotalDocuments"]);
        Assert.Equal("0", contribution.Properties["FailedDocuments"]);
    }

    [Fact]
    public void DocumentReindexCompletedNotification_GetStatusContribution_ReturnsCorrectContribution()
    {
        // Arrange
        var notification = new DocumentReindexCompletedNotification(
            OrchestrationId: "test-orchestration-123",
            DocumentLibraryOrProcessName: "TestLibrary",
            TotalDocuments: 100,
            ProcessedDocuments: 100,
            FailedDocuments: 0,
            Success: true
        );

        // Act
        var contribution = notification.GetStatusContribution();

        // Assert
        Assert.Equal("VectorStore", contribution.Source);
        Assert.Equal(SystemStatusType.OperationCompleted, contribution.StatusType);
        Assert.Equal("TestLibrary", contribution.ItemKey);
        Assert.Equal("Healthy", contribution.Status);
        Assert.Equal(SystemStatusSeverity.Info, contribution.Severity);
        Assert.Equal("Reindexing completed: 100/100 processed", contribution.StatusMessage);
        Assert.Contains("OrchestrationId", contribution.Properties);
        Assert.Contains("ProcessedDocuments", contribution.Properties);
        Assert.Contains("TotalDocuments", contribution.Properties);
        Assert.Equal("test-orchestration-123", contribution.Properties["OrchestrationId"]);
        Assert.Equal("100", contribution.Properties["ProcessedDocuments"]);
        Assert.Equal("100", contribution.Properties["TotalDocuments"]);
    }

    [Fact]
    public void DocumentReindexFailedNotification_GetStatusContribution_ReturnsCorrectContribution()
    {
        // Arrange
        var notification = new DocumentReindexFailedNotification(
            OrchestrationId: "test-orchestration-123",
            DocumentLibraryOrProcessName: "TestLibrary",
            ErrorMessage: "Connection timeout during schema validation"
        );

        // Act
        var contribution = notification.GetStatusContribution();

        // Assert
        Assert.Equal("VectorStore", contribution.Source);
        Assert.Equal(SystemStatusType.OperationFailed, contribution.StatusType);
        Assert.Equal("TestLibrary", contribution.ItemKey);
        Assert.Equal("Error", contribution.Status);
        Assert.Equal(SystemStatusSeverity.Critical, contribution.Severity);
        Assert.Equal("Reindexing failed: Connection timeout during schema validation", contribution.StatusMessage);
        Assert.Contains("OrchestrationId", contribution.Properties);
        Assert.Contains("ErrorMessage", contribution.Properties);
        Assert.Equal("test-orchestration-123", contribution.Properties["OrchestrationId"]);
        Assert.Equal("Connection timeout during schema validation", contribution.Properties["ErrorMessage"]);
    }

    [Fact]
    public void SystemStatusContribution_DefaultSeverity_IsInfo()
    {
        // Arrange & Act
        var contribution = new SystemStatusContribution
        {
            Source = "TestSource",
            StatusType = SystemStatusType.Information,
            ItemKey = "TestItem",
            Status = "TestStatus"
        };

        // Assert
        Assert.Equal(SystemStatusSeverity.Info, contribution.Severity);
    }

    [Fact]
    public void SystemStatusContribution_DefaultProperties_IsEmpty()
    {
        // Arrange & Act
        var contribution = new SystemStatusContribution
        {
            Source = "TestSource",
            StatusType = SystemStatusType.Information,
            ItemKey = "TestItem",
            Status = "TestStatus"
        };

        // Assert
        Assert.NotNull(contribution.Properties);
        Assert.Empty(contribution.Properties);
    }
}