// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Possible states for a document reindexing orchestration operation.
/// </summary>
public enum ReindexOrchestrationState
{
    /// <summary>
    /// The reindexing operation has not been started.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// The vector store is being cleared prior to reindexing.
    /// </summary>
    ClearingVectorStore = 1,

    /// <summary>
    /// The reindexing operation is currently running.
    /// </summary>
    Running = 2,

    /// <summary>
    /// The reindexing operation has completed successfully.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// The reindexing operation has failed.
    /// </summary>
    Failed = 4
}
