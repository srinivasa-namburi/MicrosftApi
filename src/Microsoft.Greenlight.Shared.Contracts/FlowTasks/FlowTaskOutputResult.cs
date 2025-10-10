// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// Represents the result of executing Flow Task outputs.
/// </summary>
public class FlowTaskOutputResult
{
    /// <summary>
    /// Gets or sets whether all outputs were executed successfully.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the generated outputs.
    /// </summary>
    public List<FlowTaskOutput> Outputs { get; set; } = new List<FlowTaskOutput>();

    /// <summary>
    /// Gets or sets any errors that occurred during output execution.
    /// </summary>
    public List<FlowTaskOutputError> Errors { get; set; } = new List<FlowTaskOutputError>();

    /// <summary>
    /// Gets or sets the total number of outputs processed.
    /// </summary>
    public int TotalOutputs { get; set; }

    /// <summary>
    /// Gets or sets the number of successful outputs.
    /// </summary>
    public int SuccessfulOutputs { get; set; }

    /// <summary>
    /// Gets or sets the number of failed outputs.
    /// </summary>
    public int FailedOutputs { get; set; }

    /// <summary>
    /// Gets or sets execution metadata.
    /// </summary>
    public Dictionary<string, object?>? ExecutionMetadata { get; set; }
}

/// <summary>
/// Represents an error that occurred during output execution.
/// </summary>
public class FlowTaskOutputError
{
    /// <summary>
    /// Gets or sets the output name that failed.
    /// </summary>
    public string OutputName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets whether this error is recoverable.
    /// </summary>
    public bool IsRecoverable { get; set; }
}