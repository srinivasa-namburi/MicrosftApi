// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.FlowTasks;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

/// <summary>
/// API client interface for Flow Task template operations.
/// </summary>
public interface IFlowTaskApiClient : IServiceClient
{
    /// <summary>
    /// Gets all Flow Task templates with optional filtering.
    /// </summary>
    /// <param name="category">Optional category filter.</param>
    /// <param name="isActive">Optional active state filter.</param>
    /// <param name="skip">Number of records to skip.</param>
    /// <param name="take">Number of records to take.</param>
    /// <returns>A list of Flow Task template summaries.</returns>
    Task<List<FlowTaskTemplateInfo>> GetAllFlowTaskTemplatesAsync(
        string? category = null,
        bool? isActive = null,
        int skip = 0,
        int take = 100);

    /// <summary>
    /// Gets a Flow Task template by its ID with full details.
    /// </summary>
    /// <param name="id">The template identifier.</param>
    /// <returns>The Flow Task template details if found; otherwise, null.</returns>
    Task<FlowTaskTemplateDetailDto?> GetFlowTaskTemplateByIdAsync(Guid id);

    /// <summary>
    /// Gets distinct categories from all Flow Task templates.
    /// </summary>
    /// <returns>A list of distinct categories.</returns>
    Task<List<string>> GetCategoriesAsync();

    /// <summary>
    /// Creates a new Flow Task template.
    /// </summary>
    /// <param name="template">The template details.</param>
    /// <returns>The created template.</returns>
    Task<FlowTaskTemplateDetailDto> CreateFlowTaskTemplateAsync(FlowTaskTemplateDetailDto template);

    /// <summary>
    /// Updates an existing Flow Task template.
    /// </summary>
    /// <param name="id">The template identifier.</param>
    /// <param name="template">The updated template details.</param>
    /// <returns>The updated template.</returns>
    Task<FlowTaskTemplateDetailDto> UpdateFlowTaskTemplateAsync(Guid id, FlowTaskTemplateDetailDto template);

    /// <summary>
    /// Deletes a Flow Task template.
    /// </summary>
    /// <param name="id">The template identifier.</param>
    /// <returns>True if deleted successfully; otherwise, false.</returns>
    Task<bool> DeleteFlowTaskTemplateAsync(Guid id);

    /// <summary>
    /// Imports a Flow Task template from JSON.
    /// </summary>
    /// <param name="template">The template data to import.</param>
    /// <returns>The imported template.</returns>
    Task<FlowTaskTemplateDetailDto> ImportFlowTaskTemplateAsync(FlowTaskTemplateDetailDto template);

    /// <summary>
    /// Exports a Flow Task template as JSON.
    /// </summary>
    /// <param name="id">The template identifier.</param>
    /// <returns>The exported template.</returns>
    Task<FlowTaskTemplateDetailDto?> ExportFlowTaskTemplateAsync(Guid id);
}
