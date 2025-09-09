// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Reindexing.Events;

/// <summary>
/// Notification sent when document reindexing starts.
/// </summary>
public record DocumentReindexStartedNotification(
    /// <summary>
    /// The orchestration ID for the reindexing operation.
    /// </summary>
    string OrchestrationId,
    
    /// <summary>
    /// The name of the document library or process being reindexed.
    /// </summary>
    string DocumentLibraryOrProcessName,
    
    /// <summary>
    /// The reason for reindexing.
    /// </summary>
    string Reason
) : ISystemStatusNotification
{
    /// <inheritdoc />
    public SystemStatusContribution GetStatusContribution() => new()
    {
        Source = "VectorStore",
        StatusType = SystemStatusType.OperationStarted,
        ItemKey = DocumentLibraryOrProcessName,
        Status = "Reindexing",
        StatusMessage = $"Started reindexing: {Reason}",
        Severity = SystemStatusSeverity.Info,
        Properties = new Dictionary<string, string>
        {
            ["OrchestrationId"] = OrchestrationId,
            ["Reason"] = Reason
        }
    };
}

/// <summary>
/// Notification sent when document reindexing progress updates.
/// </summary>
public record DocumentReindexProgressNotification(
    /// <summary>
    /// The orchestration ID for the reindexing operation.
    /// </summary>
    string OrchestrationId,
    
    /// <summary>
    /// The name of the document library or process being reindexed.
    /// </summary>
    string DocumentLibraryOrProcessName,
    
    /// <summary>
    /// Total number of documents to reindex.
    /// </summary>
    int TotalDocuments,
    
    /// <summary>
    /// Number of documents successfully reindexed.
    /// </summary>
    int ProcessedDocuments,
    
    /// <summary>
    /// Number of documents that failed reindexing.
    /// </summary>
    int FailedDocuments
) : ISystemStatusNotification
{
    /// <inheritdoc />
    public SystemStatusContribution GetStatusContribution() => new()
    {
        Source = "VectorStore",
        StatusType = SystemStatusType.ProgressUpdate,
        ItemKey = DocumentLibraryOrProcessName,
        Status = "Reindexing",
        StatusMessage = $"Progress: {ProcessedDocuments}/{TotalDocuments} processed, {FailedDocuments} failed",
        Severity = FailedDocuments > 0 ? SystemStatusSeverity.Warning : SystemStatusSeverity.Info,
        Properties = new Dictionary<string, string>
        {
            ["OrchestrationId"] = OrchestrationId,
            ["TotalDocuments"] = TotalDocuments.ToString(),
            ["ProcessedDocuments"] = ProcessedDocuments.ToString(),
            ["FailedDocuments"] = FailedDocuments.ToString(),
            ["PercentComplete"] = TotalDocuments > 0 ? ((double)ProcessedDocuments / TotalDocuments * 100).ToString("F1") : "0"
        }
    };
}

/// <summary>
/// Notification sent when document reindexing completes.
/// </summary>
public record DocumentReindexCompletedNotification(
    /// <summary>
    /// The orchestration ID for the reindexing operation.
    /// </summary>
    string OrchestrationId,
    
    /// <summary>
    /// The name of the document library or process being reindexed.
    /// </summary>
    string DocumentLibraryOrProcessName,
    
    /// <summary>
    /// Total number of documents that were reindexed.
    /// </summary>
    int TotalDocuments,
    
    /// <summary>
    /// Number of documents successfully reindexed.
    /// </summary>
    int ProcessedDocuments,
    
    /// <summary>
    /// Number of documents that failed reindexing.
    /// </summary>
    int FailedDocuments,
    
    /// <summary>
    /// Whether the reindexing operation completed successfully.
    /// </summary>
    bool Success
) : ISystemStatusNotification
{
    /// <inheritdoc />
    public SystemStatusContribution GetStatusContribution() => new()
    {
        Source = "VectorStore",
        StatusType = SystemStatusType.OperationCompleted,
        ItemKey = DocumentLibraryOrProcessName,
        Status = Success ? "Healthy" : "Warning",
        StatusMessage = Success 
            ? $"Reindexing completed: {ProcessedDocuments}/{TotalDocuments} processed"
            : $"Reindexing completed with issues: {ProcessedDocuments}/{TotalDocuments} processed, {FailedDocuments} failed",
        Severity = Success && FailedDocuments == 0 ? SystemStatusSeverity.Info : SystemStatusSeverity.Warning,
        Properties = new Dictionary<string, string>
        {
            ["OrchestrationId"] = OrchestrationId,
            ["TotalDocuments"] = TotalDocuments.ToString(),
            ["ProcessedDocuments"] = ProcessedDocuments.ToString(),
            ["FailedDocuments"] = FailedDocuments.ToString(),
            ["Success"] = Success.ToString()
        }
    };
}

/// <summary>
/// Notification sent when document reindexing fails.
/// </summary>
public record DocumentReindexFailedNotification(
    /// <summary>
    /// The orchestration ID for the reindexing operation.
    /// </summary>
    string OrchestrationId,
    
    /// <summary>
    /// The name of the document library or process being reindexed.
    /// </summary>
    string DocumentLibraryOrProcessName,
    
    /// <summary>
    /// The error message describing why reindexing failed.
    /// </summary>
    string ErrorMessage
) : ISystemStatusNotification
{
    /// <inheritdoc />
    public SystemStatusContribution GetStatusContribution() => new()
    {
        Source = "VectorStore",
        StatusType = SystemStatusType.OperationFailed,
        ItemKey = DocumentLibraryOrProcessName,
        Status = "Error",
        StatusMessage = $"Reindexing failed: {ErrorMessage}",
        Severity = SystemStatusSeverity.Critical,
        Properties = new Dictionary<string, string>
        {
            ["OrchestrationId"] = OrchestrationId,
            ["ErrorMessage"] = ErrorMessage
        }
    };
}