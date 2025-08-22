// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// State for document ingestion orchestration runs.
/// </summary>
public enum IngestionOrchestrationState
{
    /// <summary>
    /// Orchestration not started.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// Orchestration running.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Orchestration completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Orchestration failed.
    /// </summary>
    Failed = 3
}
