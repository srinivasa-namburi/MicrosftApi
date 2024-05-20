using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Shared.Services;

public interface IPromptInfoService
{
    Task<PromptInfo?> GetPromptByIdAsync(Guid id);
    Task<List<PromptInfo>> GetPromptsByProcessIdAsync(Guid processId);
    Task AddPromptAsync(PromptInfo promptInfo);
    Task UpdatePromptAsync(PromptInfo promptInfo);
    Task DeletePromptAsync(Guid promptId);
}