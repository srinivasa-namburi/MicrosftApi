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
    /// <summary>
    /// Service for managing prompt information.
    /// </summary>
    public class PromptInfoService : IPromptInfoService
    {
        private readonly GenericRepository<PromptDefinition> _promptDefinitionRepository;
        private readonly GenericRepository<PromptImplementation> _promptImplementationRepository;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="PromptInfoService"/> class.
        /// </summary>
        /// <param name="promptDefinitionRepository">The repository for prompt definitions.</param>
        /// <param name="promptImplementationRepository">The repository for prompt implementations.</param>
        /// <param name="documentProcessInfoService">The service for document process information.</param>
        /// <param name="serviceProvider">The service provider.</param>
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

        /// <inheritdoc/>
        public async Task<PromptInfo?> GetPromptByIdAsync(Guid id)
        {
            var implementation = await _promptImplementationRepository.AllRecords()
                .Include(pi => pi.PromptDefinition)
                .Include(pi => pi.DocumentProcessDefinition)
                .FirstOrDefaultAsync(pi => pi.Id == id);

            if (implementation == null)
                return null;

            if(implementation.PromptDefinition == null || implementation.DocumentProcessDefinition == null)
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

        /// <inheritdoc/>
        public async Task<PromptInfo?> GetPromptByShortCodeAndProcessNameAsync(string promptShortCode, string documentProcessName)
        {
            var prompts = await GetPromptsByDocumentProcessName(documentProcessName);
            return prompts.FirstOrDefault(p => p.ShortCode == promptShortCode);
        }

        /// <inheritdoc/>
        public async Task<string?> GetPromptTextByShortCodeAndProcessNameAsync(string promptShortCode, string documentProcessName)
        {
            var prompt = await GetPromptByShortCodeAndProcessNameAsync(promptShortCode, documentProcessName);
            return prompt?.Text ?? "";
        }

        /// <inheritdoc/>
        public async Task<List<PromptInfo>> GetPromptsByProcessIdAsync(Guid processId)
        {
            var implementations = await _promptImplementationRepository.AllRecords()
                .Where(pi => pi.DocumentProcessDefinitionId == processId)
                .Include(pi => pi.PromptDefinition)
                .Include(pi => pi.DocumentProcessDefinition)
                .ToListAsync();

            if (implementations == null || implementations.Count == 0)
                return new List<PromptInfo>();

            implementations = implementations.Where(pi => pi.PromptDefinition != null && pi.DocumentProcessDefinition != null).ToList();

            return implementations.Select(pi => new PromptInfo
            {
                Id = pi.Id,
                ShortCode = pi.PromptDefinition!.ShortCode,
                DocumentProcessName = pi.DocumentProcessDefinition!.ShortName,
                DocumentProcessId = pi.DocumentProcessDefinitionId,
                Description = pi.PromptDefinition.Description,
                Text = pi.Text
            }).ToList();
        }

        /// <inheritdoc/>
        public async Task<List<PromptInfo>> GetPromptsByDocumentProcessName(string documentProcessName)
        {
            var documentProcess =
                await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);

            if (documentProcess == null)
                return new List<PromptInfo>();

            if (documentProcess.Source == ProcessSource.Static)
            {
                var scope = _serviceProvider.CreateScope();
                var promptCatalogTypes = scope.ServiceProvider.GetRequiredServiceForDocumentProcess<IPromptCatalogTypes>(documentProcess!);

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
                return await GetPromptsByProcessIdAsync(documentProcess.Id);
            }
        }

        /// <inheritdoc/>
        public async Task<Guid> AddPromptAsync(PromptInfo promptInfo)
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
            return promptImplementation.Id;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
