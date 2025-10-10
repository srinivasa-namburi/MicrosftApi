// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// Service for managing system-wide prompts with database overrides.
/// If no database override exists for a prompt name, returns the default from SystemWidePromptCatalogTemplates.
/// </summary>
public interface ISystemPromptInfoService
{
    /// <summary>
    /// Gets a system prompt by its name.
    /// If a database override exists and is active, returns that; otherwise returns the default from SystemWidePromptCatalogTemplates.
    /// </summary>
    /// <param name="promptName">The name of the system prompt (e.g., "FlowBackendConversationSystemPrompt").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prompt text if found; otherwise, null.</returns>
    Task<string?> GetPromptAsync(string promptName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all system prompts from SystemWidePromptCatalogTemplates, with database overrides applied where they exist.
    /// Returns a dictionary of prompt name to prompt text.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping prompt names to their text (either default or overridden).</returns>
    Task<Dictionary<string, string>> GetAllPromptsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a system prompt override in the database.
    /// If an override with the given name already exists, updates it; otherwise creates a new one.
    /// </summary>
    /// <param name="promptName">The name of the system prompt to override.</param>
    /// <param name="promptText">The override text for the prompt.</param>
    /// <param name="isActive">Whether this override is active.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the created or updated system prompt override.</returns>
    Task<Guid> UpsertPromptOverrideAsync(string promptName, string promptText, bool isActive = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a system prompt override from the database, reverting to the default from SystemWidePromptCatalogTemplates.
    /// </summary>
    /// <param name="promptName">The name of the system prompt override to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if an override was deleted; false if no override existed.</returns>
    Task<bool> DeletePromptOverrideAsync(string promptName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a system prompt with Scriban template variables.
    /// First retrieves the prompt text (with database override if exists), then renders it with the provided variables.
    /// </summary>
    /// <param name="promptName">The name of the system prompt to render.</param>
    /// <param name="variables">Dictionary of variable names to values for Scriban rendering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered prompt text, or null if the prompt was not found.</returns>
    Task<string?> RenderPromptAsync(string promptName, Dictionary<string, object> variables, CancellationToken cancellationToken = default);
}
