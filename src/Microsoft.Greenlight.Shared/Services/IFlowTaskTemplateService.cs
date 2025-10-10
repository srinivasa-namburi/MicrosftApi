// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.FlowTasks;

namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// Service interface for managing Flow Task templates.
/// </summary>
public interface IFlowTaskTemplateService
{
    /// <summary>
    /// Gets all active Flow Task templates.
    /// </summary>
    /// <returns>A list of Flow Task template information objects.</returns>
    Task<List<FlowTaskTemplateInfo>> GetActiveFlowTaskTemplatesAsync();

    /// <summary>
    /// Gets a Flow Task template by its ID.
    /// </summary>
    /// <param name="templateId">The ID of the template to retrieve.</param>
    /// <returns>The Flow Task template information if found; otherwise, null.</returns>
    Task<FlowTaskTemplateInfo?> GetFlowTaskTemplateByIdAsync(Guid templateId);

    /// <summary>
    /// Gets a Flow Task template by its unique name.
    /// </summary>
    /// <param name="name">The unique name of the template.</param>
    /// <returns>The Flow Task template information if found; otherwise, null.</returns>
    Task<FlowTaskTemplateInfo?> GetFlowTaskTemplateByNameAsync(string name);

    /// <summary>
    /// Gets Flow Task templates by category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>A list of Flow Task template information objects in the specified category.</returns>
    Task<List<FlowTaskTemplateInfo>> GetFlowTaskTemplatesByCategoryAsync(string category);

    /// <summary>
    /// Synchronizes a Flow Task template from a Document Process's metadata fields.
    /// Creates or updates a Flow Task template that maps to the document generation workflow.
    /// </summary>
    /// <param name="documentProcessId">The ID of the Document Process to sync from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the created or updated Flow Task template.</returns>
    Task<Guid> SyncFlowTaskFromDocumentProcessAsync(Guid documentProcessId, CancellationToken cancellationToken = default);
}
