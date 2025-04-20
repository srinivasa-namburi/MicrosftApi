using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Prompts;

namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Service for managing prompt information without caching.
    /// </summary>
    public class PromptInfoService : IPromptInfoService
    {
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;
        private readonly IServiceProvider _serviceProvider;

        public PromptInfoService(
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            IDocumentProcessInfoService documentProcessInfoService,
            IServiceProvider serviceProvider)
        {
            _dbContextFactory = dbContextFactory;
            _documentProcessInfoService = documentProcessInfoService;
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public async Task<PromptInfo?> GetPromptByIdAsync(Guid id)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var implementation = await dbContext.PromptImplementations
                .Include(pi => pi.PromptDefinition)
                .Include(pi => pi.DocumentProcessDefinition)
                .FirstOrDefaultAsync(pi => pi.Id == id);

            if (implementation?.PromptDefinition == null ||
                implementation.DocumentProcessDefinition == null)
            {
                return null;
            }

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

        /// <inheritdoc />
        public async Task<PromptInfo?> GetPromptByShortCodeAndProcessNameAsync(string promptShortCode, string documentProcessName)
        {
            var prompts = await GetPromptsByDocumentProcessName(documentProcessName);
            return prompts.FirstOrDefault(p => p.ShortCode == promptShortCode);
        }

        /// <inheritdoc />
        public async Task<string?> GetPromptTextByShortCodeAndProcessNameAsync(string promptShortCode, string documentProcessName)
        {
            var prompt = await GetPromptByShortCodeAndProcessNameAsync(promptShortCode, documentProcessName);
            return prompt?.Text ?? "";
        }

        /// <inheritdoc />
        public async Task<List<PromptInfo>> GetPromptsByProcessIdAsync(Guid processId)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var implementations = await dbContext.PromptImplementations
                .Where(pi => pi.DocumentProcessDefinitionId == processId)
                .Include(pi => pi.PromptDefinition)
                .Include(pi => pi.DocumentProcessDefinition)
                .ToListAsync();

            if (implementations.Count == 0)
                return [];

            implementations = implementations
                .Where(pi => pi.PromptDefinition != null && pi.DocumentProcessDefinition != null)
                .ToList();

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

        /// <inheritdoc />
        public async Task<List<PromptInfo>> GetPromptsByDocumentProcessName(string documentProcessName)
        {
            var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);
            if (documentProcess == null)
                return [];

            // For static processes, retrieve from the prompt catalog.
            if (documentProcess.Source == ProcessSource.Static)
            {
                using var scope = _serviceProvider.CreateScope();
                var promptCatalogTypes = scope.ServiceProvider.GetRequiredServiceForDocumentProcess<IPromptCatalogTypes>(documentProcess);
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

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var promptDefinitions = await dbContext.PromptDefinitions.ToListAsync();
                foreach (var promptInfo in promptInfos)
                {
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

        /// <inheritdoc />
        public async Task<Guid> AddPromptAsync(PromptInfo promptInfo)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var promptImplementation = new PromptImplementation
            {
                Id = Guid.NewGuid(),
                PromptDefinitionId = promptInfo.DefinitionId,
                DocumentProcessDefinitionId = promptInfo.DocumentProcessId,
                StaticDocumentProcessShortCode = promptInfo.DocumentProcessName,
                Text = promptInfo.Text
            };
            await dbContext.PromptImplementations.AddAsync(promptImplementation);
            await dbContext.SaveChangesAsync();
            return promptImplementation.Id;
        }

        /// <inheritdoc />
        public async Task UpdatePromptAsync(PromptInfo promptInfo)
        {
            if (promptInfo.Id != null)
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var promptImplementation = await dbContext.PromptImplementations.FindAsync((Guid)promptInfo.Id);
                if (promptImplementation != null)
                {
                    promptImplementation.Text = promptInfo.Text;
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        /// <inheritdoc />
        public async Task DeletePromptAsync(Guid promptId)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var promptImplementation = await dbContext.PromptImplementations.FindAsync(promptId);
            if (promptImplementation != null)
            {
                dbContext.PromptImplementations.Remove(promptImplementation);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
