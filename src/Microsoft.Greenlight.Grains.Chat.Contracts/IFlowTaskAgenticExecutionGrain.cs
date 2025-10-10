// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;
using Orleans;

namespace Microsoft.Greenlight.Grains.Chat.Contracts;

/// <summary>
/// Interface for the grain that handles agentic Flow Task execution using multi-agent orchestration.
/// Provides conversational, natural requirement gathering with branching conversations.
/// This interface mirrors IFlowTaskExecutionGrain but uses a different implementation approach.
/// </summary>
public interface IFlowTaskAgenticExecutionGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Starts a new agentic Flow Task execution session.
    /// </summary>
    /// <param name="flowSessionId">The ID of the parent Flow session.</param>
    /// <param name="templateId">The ID of the Flow Task template to execute.</param>
    /// <param name="initialMessage">The initial message that triggered this Flow Task.</param>
    /// <param name="userContext">Additional context about the user and session.</param>
    /// <returns>The execution result containing initial state and prompts.</returns>
    Task<FlowTaskExecutionResult> StartExecutionAsync(
        Guid flowSessionId,
        Guid templateId,
        string initialMessage,
        string userContext);

    /// <summary>
    /// Processes a user message within the agentic Flow Task execution.
    /// </summary>
    /// <param name="message">The user's message.</param>
    /// <returns>The execution result with updated state and response.</returns>
    Task<FlowTaskExecutionResult> ProcessMessageAsync(string message);

    /// <summary>
    /// Gets the current execution state of the Flow Task.
    /// </summary>
    /// <returns>The current execution state enum value.</returns>
    Task<FlowTaskExecutionState> GetCurrentStateAsync();

    /// <summary>
    /// Checks if the Flow Task execution is complete.
    /// </summary>
    /// <returns>True if the Flow Task has completed, false otherwise.</returns>
    Task<bool> IsCompleteAsync();

    /// <summary>
    /// Cancels the current Flow Task execution.
    /// </summary>
    /// <returns>A task representing the cancellation operation.</returns>
    Task CancelAsync();

    /// <summary>
    /// Gets a summary of the collected requirements and their values.
    /// </summary>
    /// <returns>A summary of the Flow Task execution.</returns>
    Task<FlowTaskExecutionSummary> GetSummaryAsync();

    /// <summary>
    /// Validates the collected requirements against the template rules.
    /// </summary>
    /// <returns>A validation result indicating any missing or invalid requirements.</returns>
    Task<FlowTaskValidationResult> ValidateRequirementsAsync();

    /// <summary>
    /// Executes the output templates to generate results.
    /// </summary>
    /// <returns>The generated outputs from the Flow Task.</returns>
    Task<FlowTaskOutputResult> ExecuteOutputsAsync();
}
