// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Text.Json;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Grains.Chat.Contracts.State;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Chat;

/// <summary>
/// Orleans grain for managing structured state during agentic Flow Task execution.
/// Stores requirement values and provides access to agents via FlowTaskStatePlugin.
/// </summary>
[Reentrant]
public class FlowTaskStateGrain : Grain, IFlowTaskStateGrain
{
    private readonly IPersistentState<FlowTaskStateGrainState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskStateGrain"/> class.
    /// </summary>
    /// <param name="state">The persistent state.</param>
    public FlowTaskStateGrain([PersistentState("flowTaskState")] IPersistentState<FlowTaskStateGrainState> state)
    {
        _state = state;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(FlowTaskTemplateDetailDto template)
    {
        _state.State.Template = template;
        _state.State.CollectedValues.Clear();
        _state.State.RevisedFields.Clear();
        _state.State.CurrentSection = template.Sections.FirstOrDefault()?.Name ?? string.Empty;
        _state.State.LastUpdatedUtc = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    /// <inheritdoc/>
    public async Task SetRequirementValueAsync(string fieldName, object? value)
    {
        _state.State.CollectedValues[fieldName] = value;
        _state.State.LastUpdatedUtc = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    /// <inheritdoc/>
    public Task<object?> GetRequirementValueAsync(string fieldName)
    {
        _state.State.CollectedValues.TryGetValue(fieldName, out var value);
        return Task.FromResult(value);
    }

    /// <inheritdoc/>
    public Task<string> GetCollectedValuesAsJsonAsync()
    {
        var json = JsonSerializer.Serialize(_state.State.CollectedValues, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        return Task.FromResult(json);
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, object?>> GetCollectedValuesAsync()
    {
        return Task.FromResult(_state.State.CollectedValues);
    }

    /// <inheritdoc/>
    public Task<string> GetPendingRequiredFieldsAsync()
    {
        if (_state.State.Template == null)
        {
            return Task.FromResult("[]");
        }

        var allRequiredFields = _state.State.Template.Sections
            .SelectMany(s => s.Requirements ?? new List<FlowTaskRequirementDto>())
            .Where(r => r.IsRequired)
            .Select(r => r.FieldName)
            .ToList();

        var pendingFields = allRequiredFields
            .Where(fieldName => !_state.State.CollectedValues.ContainsKey(fieldName) ||
                               _state.State.CollectedValues[fieldName] == null ||
                               string.IsNullOrWhiteSpace(_state.State.CollectedValues[fieldName]?.ToString()))
            .ToList();

        var json = JsonSerializer.Serialize(pendingFields);
        return Task.FromResult(json);
    }

    /// <inheritdoc/>
    public Task<string> GetPendingOptionalFieldsAsync()
    {
        if (_state.State.Template == null)
        {
            return Task.FromResult("[]");
        }

        var allOptionalFields = _state.State.Template.Sections
            .SelectMany(s => s.Requirements ?? new List<FlowTaskRequirementDto>())
            .Where(r => !r.IsRequired)
            .Select(r => r.FieldName)
            .ToList();

        var pendingFields = allOptionalFields
            .Where(fieldName => !_state.State.CollectedValues.ContainsKey(fieldName) ||
                               _state.State.CollectedValues[fieldName] == null ||
                               string.IsNullOrWhiteSpace(_state.State.CollectedValues[fieldName]?.ToString()))
            .ToList();

        var json = JsonSerializer.Serialize(pendingFields);
        return Task.FromResult(json);
    }

    /// <inheritdoc/>
    public async Task MarkFieldAsRevisedAsync(string fieldName)
    {
        _state.State.RevisedFields.Add(fieldName);
        _state.State.LastUpdatedUtc = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    /// <inheritdoc/>
    public Task<string> GetCurrentSectionAsync()
    {
        return Task.FromResult(_state.State.CurrentSection);
    }

    /// <inheritdoc/>
    public async Task SetCurrentSectionAsync(string sectionName)
    {
        _state.State.CurrentSection = sectionName;
        _state.State.LastUpdatedUtc = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    /// <inheritdoc/>
    public Task<FlowTaskTemplateDetailDto?> GetTemplateAsync()
    {
        return Task.FromResult(_state.State.Template);
    }

    /// <inheritdoc/>
    public async Task ClearAsync()
    {
        _state.State.CollectedValues.Clear();
        _state.State.RevisedFields.Clear();
        _state.State.CurrentSection = _state.State.Template?.Sections.FirstOrDefault()?.Name ?? string.Empty;
        _state.State.LastUpdatedUtc = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    /// <inheritdoc/>
    public async Task ClearAndDeactivateAsync()
    {
        _state.State.CollectedValues.Clear();
        _state.State.RevisedFields.Clear();
        _state.State.CurrentSection = string.Empty;
        _state.State.Template = null;
        _state.State.LastUpdatedUtc = DateTime.UtcNow;
        await _state.WriteStateAsync();
        DeactivateOnIdle();
    }
}
