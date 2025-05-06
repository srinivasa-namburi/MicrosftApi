using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling
{
    [Reentrant]
    public class PromptDefinitionsUpdateGrain : Grain, IPromptDefinitionsUpdateGrain
    {
        private readonly ILogger<PromptDefinitionsUpdateGrain> _logger;
        private readonly IPromptDefinitionService _promptDefinitionService;
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly DefaultPromptCatalogTypes _defaultPromptCatalogTypes;

        public PromptDefinitionsUpdateGrain(
            ILogger<PromptDefinitionsUpdateGrain> logger,
            IPromptDefinitionService promptDefinitionService,
            IDbContextFactory<DocGenerationDbContext> dbContextFactory)
        {
            _logger = logger;
            _promptDefinitionService = promptDefinitionService;
            _dbContextFactory = dbContextFactory;
            _defaultPromptCatalogTypes = new DefaultPromptCatalogTypes();
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Prompt definitions update job started at {time}", DateTimeOffset.Now);

            try
            {
                // First, ensure prompt definitions exist
                await _promptDefinitionService.EnsurePromptDefinitionsAsync();
                _logger.LogInformation("Prompt definitions update completed successfully");

                // Next, ensure prompt implementations exist for all document processes
                await EnsurePromptImplementationsForAllDocumentProcessesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating prompt definitions");
            }
        }

        private async Task EnsurePromptImplementationsForAllDocumentProcessesAsync()
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Get all document processes
            var documentProcesses = await dbContext.DynamicDocumentProcessDefinitions
                .ToListAsync();

            if (documentProcesses.Count == 0)
            {
                _logger.LogInformation("No document processes found. Skipping prompt implementation creation.");
                return;
            }

            // Get all prompt definitions
            var promptDefinitions = await dbContext.PromptDefinitions
                .ToListAsync();

            if (promptDefinitions.Count == 0)
            {
                _logger.LogInformation("No prompt definitions found. Skipping prompt implementation creation.");
                return;
            }

            // Get all existing prompt implementations to check for duplicates
            var existingImplementations = await dbContext.PromptImplementations
                .ToListAsync();

            // Count for logging
            int totalImplementationsAdded = 0;

            foreach (var documentProcess in documentProcesses)
            {
                // Create a counter to track new implementations for this document process
                int implementationsAddedForProcess = 0;

                foreach (var promptDefinition in promptDefinitions)
                {
                    // Check if implementation already exists for this document process and prompt definition
                    bool implementationExists = existingImplementations.Any(pi => 
                        pi.PromptDefinitionId == promptDefinition.Id && 
                        pi.DocumentProcessDefinitionId == documentProcess.Id);

                    if (implementationExists)
                    {
                        // Implementation already exists, skip
                        continue;
                    }

                    // Get default text for this prompt from DefaultPromptCatalogTypes if available
                    string defaultText = string.Empty;
                    var promptCatalogProperty = _defaultPromptCatalogTypes.GetType()
                        .GetProperties()
                        .FirstOrDefault(p => p.PropertyType == typeof(string) && p.Name == promptDefinition.ShortCode);

                    if (promptCatalogProperty != null)
                    {
                        defaultText = promptCatalogProperty.GetValue(_defaultPromptCatalogTypes)?.ToString() ?? string.Empty;
                    }

                    // Create a new prompt implementation
                    var promptImplementation = new PromptImplementation
                    {
                        Id = Guid.NewGuid(),
                        DocumentProcessDefinitionId = documentProcess.Id,
                        PromptDefinitionId = promptDefinition.Id,
                        Text = defaultText
                    };

                    dbContext.PromptImplementations.Add(promptImplementation);
                    implementationsAddedForProcess++;
                }

                if (implementationsAddedForProcess > 0)
                {
                    _logger.LogInformation(
                        "Created {Count} prompt implementations for document process {ProcessName}",
                        implementationsAddedForProcess,
                        documentProcess.ShortName);
                    
                    totalImplementationsAdded += implementationsAddedForProcess;
                }
            }

            if (totalImplementationsAdded > 0)
            {
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Created a total of {Count} prompt implementations across all document processes", totalImplementationsAdded);
            }
            else
            {
                _logger.LogInformation("All document processes already have all prompt implementations. No new implementations created.");
            }
        }
    }
}