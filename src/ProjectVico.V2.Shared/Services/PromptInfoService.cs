using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Models.DocumentProcess;
using ProjectVico.V2.Shared.Repositories;

namespace ProjectVico.V2.Shared.Services
{
    public class PromptInfoService
    {
        private readonly GenericRepository<PromptDefinition> _promptDefinitionRepository;
        private readonly GenericRepository<PromptImplementation> _promptImplementationRepository;

        public PromptInfoService(GenericRepository<PromptDefinition> promptDefinitionRepository,
                                 GenericRepository<PromptImplementation> promptImplementationRepository)
        {
            _promptDefinitionRepository = promptDefinitionRepository;
            _promptImplementationRepository = promptImplementationRepository;
        }

        public async Task<PromptInfo?> GetPromptByIdAsync(Guid id)
        {
            var implementation = await _promptImplementationRepository.AllRecords()
                .Include(pi => pi.PromptDefinition)
                .FirstOrDefaultAsync(pi => pi.Id == id);

            if (implementation == null)
                return null;

            return new PromptInfo
            {
                Id = implementation.Id,
                ShortCode = implementation.PromptDefinition.ShortCode,
                Description = implementation.PromptDefinition.Description,
                Text = implementation.Text,
                DocumentProcessId = implementation.DocumentProcessDefinitionId
            };
        }

        public async Task<List<PromptInfo>> GetPromptsByProcessIdAsync(Guid processId)
        {
            var implementations = await _promptImplementationRepository.AllRecords()
                .Where(pi => pi.DocumentProcessDefinitionId == processId)
                .Include(pi => pi.PromptDefinition)
                .ToListAsync();

            return implementations.Select(pi => new PromptInfo
            {
                Id = pi.Id,
                ShortCode = pi.PromptDefinition.ShortCode,
                Description = pi.PromptDefinition.Description,
                Text = pi.Text,
                DocumentProcessId = pi.DocumentProcessDefinitionId
            }).ToList();
        }

        public async Task AddPromptAsync(PromptInfo promptInfo)
        {
            var promptImplementation = new PromptImplementation
            {
                Id = Guid.NewGuid(),
                PromptDefinitionId = promptInfo.Id, // Assuming Id here refers to the PromptDefinition ID
                DocumentProcessDefinitionId = promptInfo.DocumentProcessId,
                Text = promptInfo.Text
            };
            await _promptImplementationRepository.AddAsync(promptImplementation);
        }

        public async Task UpdatePromptAsync(PromptInfo promptInfo)
        {
            var promptImplementation = await _promptImplementationRepository.GetByIdAsync(promptInfo.Id);
            if (promptImplementation != null)
            {
                promptImplementation.Text = promptInfo.Text;
                await _promptImplementationRepository.UpdateAsync(promptImplementation);
            }
        }

        public async Task DeletePromptAsync(Guid promptId)
        {
            var promptImplementation = await _promptImplementationRepository.GetByIdAsync(promptId);
            if (promptImplementation != null)
            {
                await _promptImplementationRepository.DeleteAsync(promptImplementation);
            }
        }

       
    }
}
