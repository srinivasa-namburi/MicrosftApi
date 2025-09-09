// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Reindexing.Events;

/// <summary>
/// Notification sent when content reference reindexing starts.
/// </summary>
public record ContentReferenceReindexStartedNotification(
    string OrchestrationId,
    ContentReferenceType ReferenceType,
    string Reason
) : ISystemStatusNotification
{
    public Microsoft.Greenlight.Shared.Contracts.Messages.SystemStatusContribution GetStatusContribution() => new()
    {
        Source = "VectorStore",
        StatusType = SystemStatusType.OperationStarted,
        ItemKey = $"CR:{ReferenceType}",
        Status = "Reindexing",
        StatusMessage = $"Started content reference reindexing: {Reason}",
        Severity = SystemStatusSeverity.Info,
        Properties = new Dictionary<string, string>
        {
            ["OrchestrationId"] = OrchestrationId,
            ["ReferenceType"] = ReferenceType.ToString(),
            ["Reason"] = Reason
        }
    };
}

/// <summary>
/// Notification sent for content reference reindex progress updates.
/// </summary>
public record ContentReferenceReindexProgressNotification(
    string OrchestrationId,
    ContentReferenceType ReferenceType,
    int Total,
    int Processed,
    int Failed
) : ISystemStatusNotification
{
    public Microsoft.Greenlight.Shared.Contracts.Messages.SystemStatusContribution GetStatusContribution() => new()
    {
        Source = "VectorStore",
        StatusType = SystemStatusType.ProgressUpdate,
        ItemKey = $"CR:{ReferenceType}",
        Status = "Reindexing",
        StatusMessage = $"CR progress: {Processed}/{Total} processed, {Failed} failed",
        Severity = Failed > 0 ? SystemStatusSeverity.Warning : SystemStatusSeverity.Info,
        Properties = new Dictionary<string, string>
        {
            ["OrchestrationId"] = OrchestrationId,
            ["ReferenceType"] = ReferenceType.ToString(),
            ["Total"] = Total.ToString(),
            ["Processed"] = Processed.ToString(),
            ["Failed"] = Failed.ToString(),
            ["PercentComplete"] = Total > 0 ? ((double)Processed / Total * 100).ToString("F1") : "0"
        }
    };
}

/// <summary>
/// Notification sent when content reference reindex completes.
/// </summary>
public record ContentReferenceReindexCompletedNotification(
    string OrchestrationId,
    ContentReferenceType ReferenceType,
    int Total,
    int Processed,
    int Failed,
    bool Success
) : ISystemStatusNotification
{
    public Microsoft.Greenlight.Shared.Contracts.Messages.SystemStatusContribution GetStatusContribution() => new()
    {
        Source = "VectorStore",
        StatusType = Success ? SystemStatusType.OperationCompleted : SystemStatusType.OperationFailed,
        ItemKey = $"CR:{ReferenceType}",
        Status = Success ? "Healthy" : "Error",
        StatusMessage = Success
            ? $"CR reindex completed: {Processed}/{Total} processed"
            : $"CR reindex completed with issues: {Processed}/{Total} processed, failures: {Failed}",
        Severity = Success && Failed == 0 ? SystemStatusSeverity.Info : SystemStatusSeverity.Warning,
        Properties = new Dictionary<string, string>
        {
            ["OrchestrationId"] = OrchestrationId,
            ["ReferenceType"] = ReferenceType.ToString(),
            ["Total"] = Total.ToString(),
            ["Processed"] = Processed.ToString(),
            ["Failed"] = Failed.ToString(),
            ["Success"] = Success.ToString()
        }
    };
}

/// <summary>
/// Notification sent when content reference reindex fails.
/// </summary>
public record ContentReferenceReindexFailedNotification(
    string OrchestrationId,
    ContentReferenceType ReferenceType,
    string ErrorMessage
) : ISystemStatusNotification
{
    public Microsoft.Greenlight.Shared.Contracts.Messages.SystemStatusContribution GetStatusContribution() => new()
    {
        Source = "VectorStore",
        StatusType = SystemStatusType.OperationFailed,
        ItemKey = $"CR:{ReferenceType}",
        Status = "Error",
        StatusMessage = $"CR reindex failed: {ErrorMessage}",
        Severity = SystemStatusSeverity.Critical,
        Properties = new Dictionary<string, string>
        {
            ["OrchestrationId"] = OrchestrationId,
            ["ReferenceType"] = ReferenceType.ToString(),
            ["ErrorMessage"] = ErrorMessage
        }
    };
}

