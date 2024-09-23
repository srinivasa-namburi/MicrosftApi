using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Shared.Services;

public interface IPromptInfoService
{
    Task<PromptInfo?> GetPromptByIdAsync(Guid id);
    Task<PromptInfo?> GetPromptByShortCodeAndProcessNameAsync(string promptShortCode, string documentProcessName);
    Task<List<PromptInfo>> GetPromptsByProcessIdAsync(Guid processId);
    Task AddPromptAsync(PromptInfo promptInfo);
    Task UpdatePromptAsync(PromptInfo promptInfo);
    Task DeletePromptAsync(Guid promptId);

    /// <summary>
    /// Returns all prompts for a given process name. This can be used both for static and dynamic processes.
    /// We first determine if the process is static or dynamic, and then return the prompts accordingly.
    /// Static prompts are returned from the processes IPromptCatalogTypes, dynamic prompts are returned from the database.
    /// </summary>
    /// <param name="documentProcessName"></param>
    /// <returns></returns>
    Task<List<PromptInfo>> GetPromptsByDocumentProcessName(string documentProcessName);

    Task<string?> GetPromptTextByShortCodeAndProcessNameAsync(string promptShortCode, string documentProcessName);
}