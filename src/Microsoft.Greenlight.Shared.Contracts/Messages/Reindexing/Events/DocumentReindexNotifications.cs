// Copyright (c) Microsoft Corporation. All rights reserved.

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
);

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
);

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
);

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
);