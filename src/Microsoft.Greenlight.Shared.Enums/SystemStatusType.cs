// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Types of system status updates.
/// </summary>
public enum SystemStatusType
{
    /// <summary>An operation or process has started</summary>
    OperationStarted,
    /// <summary>An operation or process has completed successfully</summary>
    OperationCompleted,
    /// <summary>An operation or process has failed</summary>
    OperationFailed,
    /// <summary>Progress update for an ongoing operation</summary>
    ProgressUpdate,
    /// <summary>Component health status update</summary>
    HealthUpdate,
    /// <summary>Worker thread status update</summary>
    WorkerStatus,
    /// <summary>Configuration or settings change</summary>
    ConfigurationChange,
    /// <summary>Resource utilization update</summary>
    ResourceUpdate,
    /// <summary>General informational update</summary>
    Information
}