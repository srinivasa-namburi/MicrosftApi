using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// An interface for a service to manage prompt information.
/// </summary>
public interface IPromptInfoService
{
    /// <summary>
    /// Get a Prompt by its ID/Guid. That means it can only be a database-stored prompt, since only those have Guids.
    /// </summary>
    /// <param name="id">The unique identifier of the prompt.</param>
    /// <returns>The prompt information if found; otherwise, null.</returns>
    Task<PromptInfo?> GetPromptByIdAsync(Guid id);

    /// <summary>
    /// Gets a prompt by its short code and process name.
    /// </summary>
    /// <param name="promptShortCode">The short code of the prompt.</param>
    /// <param name="documentProcessName">The name of the document process.</param>
    /// <returns>The prompt information if found; otherwise, null.</returns>
    Task<PromptInfo?> GetPromptByShortCodeAndProcessNameAsync(string promptShortCode, string documentProcessName);

    /// <summary>
    /// Get all prompts for a process by the process's ID/Guid. Only dynamic processes have IDs, so this method is only
    /// returning prompts for dynamic processes (from the database).
    /// </summary>
    /// <param name="processId">The unique identifier of the process.</param>
    /// <returns>A list of prompt information.</returns>
    Task<List<PromptInfo>> GetPromptsByProcessIdAsync(Guid processId);

    /// <summary>
    /// Adds a new prompt.
    /// </summary>
    /// <param name="promptInfo">The prompt information to add.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous add operation.</returns>
    Task AddPromptAsync(PromptInfo promptInfo);

    /// <summary>
    /// Updates an existing prompt.
    /// </summary>
    /// <param name="promptInfo">The prompt information to update.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous update operation.</returns>
    Task UpdatePromptAsync(PromptInfo promptInfo);

    /// <summary>
    /// Deletes a prompt by its unique identifier.
    /// </summary>
    /// <param name="promptId">The unique identifier of the prompt to delete.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous delete operation.</returns>
    Task DeletePromptAsync(Guid promptId);

    /// <summary>
    /// Returns all prompts for a given process name. This can be used both for static and dynamic processes.
    /// We first determine if the process is static or dynamic, and then return the prompts accordingly.
    /// Static prompts are returned from the processes IPromptCatalogTypes, dynamic prompts are returned from the database.
    /// </summary>
    /// <param name="documentProcessName">The name of the document process.</param>
    /// <returns>A list of prompt information.</returns>
    Task<List<PromptInfo>> GetPromptsByDocumentProcessName(string documentProcessName);

    /// <summary>
    /// Gets the text of a prompt by its short code and process name.
    /// </summary>
    /// <param name="promptShortCode">The short code of the prompt.</param>
    /// <param name="documentProcessName">The name of the document process.</param>
    /// <returns>The prompt text if found; otherwise, null.</returns>
    Task<string?> GetPromptTextByShortCodeAndProcessNameAsync(string promptShortCode, string documentProcessName);
}
