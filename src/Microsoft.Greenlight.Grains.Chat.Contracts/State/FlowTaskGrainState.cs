// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;

namespace Microsoft.Greenlight.Grains.Chat.Contracts.State;

/// <summary>
/// Persistent state for FlowTaskAgenticExecutionGrain.
/// </summary>
public class FlowTaskGrainState
{
    /// <summary>
    /// Gets or sets the ID of the parent Flow session.
    /// </summary>
    public Guid FlowSessionId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the Flow Task template being executed.
    /// </summary>
    public Guid TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the current execution state.
    /// </summary>
    public FlowTaskExecutionState CurrentState { get; set; } = FlowTaskExecutionState.NotStarted;

    /// <summary>
    /// Gets or sets the initial message that triggered this Flow Task.
    /// </summary>
    public string InitialMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user context provided at start.
    /// </summary>
    public string UserContext { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collected requirement values.
    /// Key: FieldName, Value: Collected value
    /// </summary>
    public Dictionary<string, string> CollectedValues { get; set; } = new();

    /// <summary>
    /// Gets or sets the conversation history during requirement collection.
    /// </summary>
    public List<FlowTaskMessage> ConversationHistory { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when execution started.
    /// </summary>
    public DateTime? StartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when execution completed.
    /// </summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the generated outputs.
    /// </summary>
    public List<FlowTaskOutput> GeneratedOutputs { get; set; } = new();

    /// <summary>
    /// Gets or sets any error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the ID of the current section being processed.
    /// </summary>
    public Guid? CurrentSectionId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the current requirement being collected.
    /// </summary>
    public Guid? CurrentRequirementId { get; set; }

    /// <summary>
    /// Gets or sets whether we are currently collecting optional fields.
    /// </summary>
    public bool IsCollectingOptionals { get; set; }

    /// <summary>
    /// Gets or sets the IDs of optional fields that the user has indicated they want to provide.
    /// </summary>
    public HashSet<Guid> RequestedOptionalFieldIds { get; set; } = new();

    /// <summary>
    /// Gets or sets whether we have already offered optional fields to the user.
    /// </summary>
    public bool HasOfferedOptionals { get; set; }
}

/// <summary>
/// Represents a message in the Flow Task conversation history.
/// </summary>
public class FlowTaskMessage
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is a user message (true) or assistant message (false).
    /// </summary>
    public bool IsUserMessage { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the message.
    /// </summary>
    public DateTime TimestampUtc { get; set; }
}
