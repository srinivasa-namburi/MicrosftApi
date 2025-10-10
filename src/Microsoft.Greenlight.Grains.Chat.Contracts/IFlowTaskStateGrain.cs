// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;
using Orleans;

namespace Microsoft.Greenlight.Grains.Chat.Contracts;

/// <summary>
/// Interface for the grain that manages structured state for an agentic Flow Task execution.
/// Provides storage and retrieval of requirement values during conversational requirement gathering.
/// </summary>
public interface IFlowTaskStateGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Initializes the state grain with a Flow Task template.
    /// </summary>
    /// <param name="template">The Flow Task template containing requirements and sections.</param>
    /// <returns>A task representing the initialization operation.</returns>
    Task InitializeAsync(FlowTaskTemplateDetailDto template);

    /// <summary>
    /// Sets the value for a specific requirement field.
    /// </summary>
    /// <param name="fieldName">The name of the requirement field.</param>
    /// <param name="value">The value to store for the field.</param>
    /// <returns>A task representing the operation.</returns>
    Task SetRequirementValueAsync(string fieldName, object? value);

    /// <summary>
    /// Gets the value for a specific requirement field.
    /// </summary>
    /// <param name="fieldName">The name of the requirement field.</param>
    /// <returns>The value of the field, or null if not set.</returns>
    Task<object?> GetRequirementValueAsync(string fieldName);

    /// <summary>
    /// Gets all collected requirement values as a JSON string.
    /// </summary>
    /// <returns>JSON string containing all collected values.</returns>
    Task<string> GetCollectedValuesAsJsonAsync();

    /// <summary>
    /// Gets all collected requirement values as a dictionary.
    /// </summary>
    /// <returns>Dictionary of field names to values.</returns>
    Task<Dictionary<string, object?>> GetCollectedValuesAsync();

    /// <summary>
    /// Gets a list of pending required fields that still need values.
    /// </summary>
    /// <returns>JSON array of pending required field names.</returns>
    Task<string> GetPendingRequiredFieldsAsync();

    /// <summary>
    /// Gets a list of pending optional fields that haven't been collected.
    /// </summary>
    /// <returns>JSON array of pending optional field names.</returns>
    Task<string> GetPendingOptionalFieldsAsync();

    /// <summary>
    /// Marks a field as having been revised by the user.
    /// </summary>
    /// <param name="fieldName">The name of the field that was revised.</param>
    /// <returns>A task representing the operation.</returns>
    Task MarkFieldAsRevisedAsync(string fieldName);

    /// <summary>
    /// Gets the current section name being processed.
    /// </summary>
    /// <returns>The current section name.</returns>
    Task<string> GetCurrentSectionAsync();

    /// <summary>
    /// Sets the current section being processed.
    /// </summary>
    /// <param name="sectionName">The name of the section.</param>
    /// <returns>A task representing the operation.</returns>
    Task SetCurrentSectionAsync(string sectionName);

    /// <summary>
    /// Gets the template associated with this state grain.
    /// </summary>
    /// <returns>The Flow Task template, or null if not initialized.</returns>
    Task<FlowTaskTemplateDetailDto?> GetTemplateAsync();

    /// <summary>
    /// Clears all collected values and resets the state.
    /// </summary>
    /// <returns>A task representing the operation.</returns>
    Task ClearAsync();

    /// <summary>
    /// Clears all state and deactivates the grain.
    /// </summary>
    /// <returns>A task representing the operation.</returns>
    Task ClearAndDeactivateAsync();
}
