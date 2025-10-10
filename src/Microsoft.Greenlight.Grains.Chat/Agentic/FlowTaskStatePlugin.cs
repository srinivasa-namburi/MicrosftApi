// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.SemanticKernel;
using Orleans;

namespace Microsoft.Greenlight.Grains.Chat.Agentic;

/// <summary>
/// Semantic Kernel plugin providing agents with access to Flow Task state.
/// Wraps IFlowTaskStateGrain to enable structured requirement collection during agentic conversations.
/// </summary>
public class FlowTaskStatePlugin
{
    private readonly IGrainFactory _grainFactory;
    private readonly Guid _executionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskStatePlugin"/> class.
    /// </summary>
    /// <param name="grainFactory">The Orleans grain factory.</param>
    /// <param name="executionId">The Flow Task execution ID (grain key).</param>
    public FlowTaskStatePlugin(IGrainFactory grainFactory, Guid executionId)
    {
        _grainFactory = grainFactory;
        _executionId = executionId;
    }

    /// <summary>
    /// Sets the value for a specific requirement field.
    /// </summary>
    /// <param name="fieldName">The name of the requirement field to set.</param>
    /// <param name="value">The value to store for the field.</param>
    /// <returns>A confirmation message.</returns>
    [KernelFunction, Description("Set the value for a specific requirement field")]
    public async Task<string> SetRequirementValueAsync(
        [Description("The name of the requirement field")] string fieldName,
        [Description("The value to store for the field")] string value)
    {
        // Normalize field name: Remove spaces to handle cases where agent uses display name instead of field name
        // This ensures "Plant Name" gets normalized to "PlantName" to match the template's FieldName property
        var normalizedFieldName = fieldName.Replace(" ", string.Empty);

        var grain = _grainFactory.GetGrain<IFlowTaskStateGrain>(_executionId);
        await grain.SetRequirementValueAsync(normalizedFieldName, value);
        return $"Successfully stored {normalizedFieldName}={value}";
    }

    /// <summary>
    /// Gets the value for a specific requirement field.
    /// </summary>
    /// <param name="fieldName">The name of the requirement field to retrieve.</param>
    /// <returns>The value of the field, or a message indicating it's not set.</returns>
    [KernelFunction, Description("Get the value of a specific requirement field")]
    public async Task<string> GetRequirementValueAsync(
        [Description("The name of the requirement field")] string fieldName)
    {
        // Normalize field name to handle display names with spaces
        var normalizedFieldName = fieldName.Replace(" ", string.Empty);

        var grain = _grainFactory.GetGrain<IFlowTaskStateGrain>(_executionId);
        var value = await grain.GetRequirementValueAsync(normalizedFieldName);

        if (value == null)
        {
            return $"Field '{normalizedFieldName}' has not been set yet.";
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Gets all collected requirement values as a JSON string.
    /// </summary>
    /// <returns>JSON string containing all collected values.</returns>
    [KernelFunction, Description("Get all collected requirement values as JSON")]
    public async Task<string> GetCollectedValuesAsync()
    {
        var grain = _grainFactory.GetGrain<IFlowTaskStateGrain>(_executionId);
        return await grain.GetCollectedValuesAsJsonAsync();
    }

    /// <summary>
    /// Gets a list of pending required fields that still need values.
    /// </summary>
    /// <returns>JSON array of pending required field names.</returns>
    [KernelFunction, Description("Get list of pending required fields that still need values")]
    public async Task<string> GetPendingRequiredFieldsAsync()
    {
        var grain = _grainFactory.GetGrain<IFlowTaskStateGrain>(_executionId);
        return await grain.GetPendingRequiredFieldsAsync();
    }

    /// <summary>
    /// Gets a list of pending optional fields that haven't been collected.
    /// </summary>
    /// <returns>JSON array of pending optional field names.</returns>
    [KernelFunction, Description("Get list of pending optional fields that haven't been collected yet")]
    public async Task<string> GetPendingOptionalFieldsAsync()
    {
        var grain = _grainFactory.GetGrain<IFlowTaskStateGrain>(_executionId);
        return await grain.GetPendingOptionalFieldsAsync();
    }

    /// <summary>
    /// Marks a field as having been revised by the user.
    /// </summary>
    /// <param name="fieldName">The name of the field that was revised.</param>
    /// <returns>A confirmation message.</returns>
    [KernelFunction, Description("Mark a field as having been revised by the user")]
    public async Task<string> MarkFieldAsRevisedAsync(
        [Description("The name of the field that was revised")] string fieldName)
    {
        // Normalize field name to handle display names with spaces
        var normalizedFieldName = fieldName.Replace(" ", string.Empty);

        var grain = _grainFactory.GetGrain<IFlowTaskStateGrain>(_executionId);
        await grain.MarkFieldAsRevisedAsync(normalizedFieldName);
        return $"Marked {normalizedFieldName} as revised";
    }

    /// <summary>
    /// Gets the current section name being processed.
    /// </summary>
    /// <returns>The current section name.</returns>
    [KernelFunction, Description("Get the current section name being processed")]
    public async Task<string> GetCurrentSectionAsync()
    {
        var grain = _grainFactory.GetGrain<IFlowTaskStateGrain>(_executionId);
        return await grain.GetCurrentSectionAsync();
    }

    /// <summary>
    /// Sets the current section being processed.
    /// </summary>
    /// <param name="sectionName">The name of the section.</param>
    /// <returns>A confirmation message.</returns>
    [KernelFunction, Description("Set the current section being processed")]
    public async Task<string> SetCurrentSectionAsync(
        [Description("The name of the section")] string sectionName)
    {
        // Normalize section name to handle display names with spaces
        var normalizedSectionName = sectionName.Replace(" ", string.Empty);

        var grain = _grainFactory.GetGrain<IFlowTaskStateGrain>(_executionId);
        await grain.SetCurrentSectionAsync(normalizedSectionName);
        return $"Current section set to {normalizedSectionName}";
    }

    /// <summary>
    /// Clears all collected values and resets the state (for "start over" scenarios).
    /// </summary>
    /// <returns>A confirmation message.</returns>
    [KernelFunction, Description("Clear all collected values and reset the state (for 'start over' scenarios)")]
    public async Task<string> ClearAllValuesAsync()
    {
        var grain = _grainFactory.GetGrain<IFlowTaskStateGrain>(_executionId);
        await grain.ClearAsync();
        return "All collected values have been cleared. Starting fresh.";
    }
}
