using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Repositories;

namespace Microsoft.Greenlight.Shared.Services
{
    public class PromptInfoService : IPromptInfoService
    {
        private readonly GenericRepository<PromptDefinition> _promptDefinitionRepository;
        private readonly GenericRepository<PromptImplementation> _promptImplementationRepository;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;
        private readonly IServiceProvider _serviceProvider;

        public PromptInfoService(
            GenericRepository<PromptDefinition> promptDefinitionRepository,
            GenericRepository<PromptImplementation> promptImplementationRepository,
            IDocumentProcessInfoService documentProcessInfoService, 
            IServiceProvider serviceProvider)
        {
            _promptDefinitionRepository = promptDefinitionRepository;
            _promptDefinitionRepository.SetCacheDuration(60.Minutes());
            _promptImplementationRepository = promptImplementationRepository;
            _promptImplementationRepository.SetCacheDuration(60.Minutes());
            _documentProcessInfoService = documentProcessInfoService;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Get a Prompt by its ID/Guid. That means it can only be a database-stored prompt, since only those have Guids.
        /// </summary>
        /// <param name="id">PromptImplementation ID</param>
        /// <returns></returns>
        public async Task<PromptInfo?> GetPromptByIdAsync(Guid id)
        {
            var implementation = await _promptImplementationRepository.AllRecords()
                .Include(pi => pi.PromptDefinition)
                .Include(pi => pi.DocumentProcessDefinition)
                .FirstOrDefaultAsync(pi => pi.Id == id);

            if (implementation == null)
                return null;

            return new PromptInfo
            {
                Id = implementation.Id,
                DefinitionId = implementation.PromptDefinitionId,
                DocumentProcessId = implementation.DocumentProcessDefinitionId,
                DocumentProcessName = implementation.DocumentProcessDefinition.ShortName,
                ShortCode = implementation.PromptDefinition.ShortCode!,
                Description = implementation.PromptDefinition.Description,
                Text = implementation.Text
            };
        }

        public async Task<PromptInfo?> GetPromptByShortCodeAndProcessNameAsync(string promptShortCode, string documentProcessName)
        {
            var prompts = await GetPromptsByDocumentProcessName(documentProcessName);
            return prompts.FirstOrDefault(p => p.ShortCode == promptShortCode);
        }

        public async Task<string?> GetPromptTextByShortCodeAndProcessNameAsync(string promptShortCode, string documentProcessName)
        {
            var prompt = await GetPromptByShortCodeAndProcessNameAsync(promptShortCode, documentProcessName);
            return prompt?.Text ?? "";
        }

        /// <summary>
        /// Get all prompts for a process by the process's ID/Guid. Only dynamic processes have IDs, so this method is only
        /// returning prompts for dynamic processes (from the database).
        /// </summary>
        /// <param name="processId">DocumentProcessDefinition.Id</param>
        /// <returns></returns>
        public async Task<List<PromptInfo>> GetPromptsByProcessIdAsync(Guid processId)
        {
            var implementations = await _promptImplementationRepository.AllRecords()
                .Where(pi => pi.DocumentProcessDefinitionId == processId)
                .Include(pi => pi.PromptDefinition)
                .Include(pi=>pi.DocumentProcessDefinition)
                .ToListAsync();

            return implementations.Select(pi => new PromptInfo
            {
                Id = pi.Id,
                ShortCode = pi.PromptDefinition.ShortCode,
                DocumentProcessName = pi.DocumentProcessDefinition.ShortName,
                DocumentProcessId = pi.DocumentProcessDefinitionId,
                Description = pi.PromptDefinition.Description,
                Text = pi.Text
                
               
            }).ToList();
        }

        /// <summary>
        /// Returns all prompts for a given process name. This can be used both for static and dynamic processes.
        /// We first determine if the process is static or dynamic, and then return the prompts accordingly.
        /// Static prompts are returned from the processes IPromptCatalogTypes, dynamic prompts are returned from the database.
        /// </summary>
        /// <param name="documentProcessName"></param>
        /// <returns></returns>
        public async Task<List<PromptInfo>> GetPromptsByDocumentProcessName(string documentProcessName)
        {
            var documentProcess =
                await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);

            if (documentProcess == null)
                return new List<PromptInfo>();

            if (documentProcess.Source == ProcessSource.Static)
            {
                // render the Prompt Catalog Types and return them as PromptInfo objects
                // We still need a class to do this work

                var scope = _serviceProvider.CreateScope();

                var promptCatalogTypes =  scope.ServiceProvider.GetRequiredServiceForDocumentProcess<IPromptCatalogTypes>(documentProcess!);
                
                // for each property in the PromptCatalogTypes, create a PromptInfo object
                var promptInfos = promptCatalogTypes.GetType().GetProperties()
                    .Select(prop => new PromptInfo
                    {
                        Id = null,
                        ShortCode = prop.Name,
                        Description = "",
                        Text = prop.GetValue(promptCatalogTypes)?.ToString() ?? "",
                        DocumentProcessId = documentProcess.Id,
                        DocumentProcessName = documentProcess.ShortName
                    }).ToList();

                foreach (var promptInfo in promptInfos)
                {

                    var promptDefinitions = await _promptDefinitionRepository.GetAllAsync(useCache: true);
                    var promptDefinition = promptDefinitions.FirstOrDefault(pd => pd.ShortCode == promptInfo.ShortCode);
                    
                    if (promptDefinition != null)
                    {
                        promptInfo.Description = promptDefinition.Description;

                    }
                }

                return promptInfos;
            }
            else
            {
                // use the Document Process ID to get the prompts from the database
                return await GetPromptsByProcessIdAsync(documentProcess.Id);
            }

        }

        public async Task AddPromptAsync(PromptInfo promptInfo)
        {
            var promptImplementation = new PromptImplementation
            {
                Id = Guid.NewGuid(),
                PromptDefinitionId = promptInfo.DefinitionId, 
                DocumentProcessDefinitionId = promptInfo.DocumentProcessId,
                StaticDocumentProcessShortCode = promptInfo.DocumentProcessName,
                Text = promptInfo.Text
            };
            await _promptImplementationRepository.AddAsync(promptImplementation);
        }

        public async Task UpdatePromptAsync(PromptInfo promptInfo)
        {
            if (promptInfo.Id != null)
            {
                var promptImplementation = await _promptImplementationRepository.GetByIdAsync((Guid)promptInfo.Id);
                if (promptImplementation != null)
                {
                    promptImplementation.Text = promptInfo.Text;
                    await _promptImplementationRepository.UpdateAsync(promptImplementation);
                }
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
