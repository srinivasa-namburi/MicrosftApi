using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.DocumentProcess.Shared.Prompts;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentProcesses;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Repositories;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.DocumentProcesses;

public class CreateDynamicDocumentProcessPromptsConsumer : IConsumer<CreateDynamicDocumentProcessPrompts>
{
    private readonly PromptDefinitionRepository _promptDefinitionRepository;
    private readonly GenericRepository<PromptImplementation> _promptImplementationGenericRepository;
    private readonly DynamicDocumentProcessDefinitionRepository _documentProcessRepository;
    private readonly ILogger<CreateDynamicDocumentProcessPromptsConsumer> _logger;
    private readonly DefaultPromptCatalogTypes _defaultPromptCatalogTypes;

    public CreateDynamicDocumentProcessPromptsConsumer(
        PromptDefinitionRepository promptDefinitionRepository,
        GenericRepository<PromptImplementation> promptImplementationGenericRepository,
        DynamicDocumentProcessDefinitionRepository documentProcessRepository,
        ILogger<CreateDynamicDocumentProcessPromptsConsumer> logger)
    {
        _promptDefinitionRepository = promptDefinitionRepository;
        _promptImplementationGenericRepository = promptImplementationGenericRepository;
        _documentProcessRepository = documentProcessRepository;
        _logger = logger;
        _defaultPromptCatalogTypes = new DefaultPromptCatalogTypes();
    }

    public async Task Consume(ConsumeContext<CreateDynamicDocumentProcessPrompts> context)
    {
        var documentProcessId = context.Message.DocumentProcessId;
        await CreateMissingPromptImplementations(documentProcessId, context.CancellationToken);
    }

    private async Task CreateMissingPromptImplementations(Guid documentProcessId, CancellationToken stoppingToken)
    {
        var documentProcess = await _documentProcessRepository.GetByIdAsync(documentProcessId);

        if (documentProcess == null)
        {
            _logger.LogWarning("CreateDynamicDocumentProcessPromptsConsumer: Document Process with Id {DocumentProcessId} not found", documentProcessId);
            return;
        }

        // Get all Prompt Implementations for the Dynamic Document Process to see if they already exist
        var promptImplementationsForDocumentProcess = _promptImplementationGenericRepository.AllRecords().Where(pi => pi.DocumentProcessDefinitionId == documentProcess.Id);

        // Loop through all the properties in the DefaultPromptCatalogTypes class to see if there are any missing Prompt Implementations for this Document Process
        // We expect to have a Prompt Implementation for each property in the DefaultPromptCatalogTypes class

        var numberOfPromptImplementationsAdded = 0;
        foreach (var promptCatalogProperty in _defaultPromptCatalogTypes.GetType().GetProperties().Where(p => p.PropertyType == typeof(string)))
        {
            var promptImplementation = promptImplementationsForDocumentProcess.FirstOrDefault(pi => pi.PromptDefinition.ShortCode == promptCatalogProperty.Name);
            if (promptImplementation == null)
            {
                var promptDefinition = await _promptDefinitionRepository.AllRecords().FirstOrDefaultAsync(pd => pd.ShortCode == promptCatalogProperty.Name, cancellationToken: stoppingToken);
                if (promptDefinition != null)
                {

                    promptImplementation = new PromptImplementation
                    {
                        DocumentProcessDefinitionId = documentProcess.Id,
                        PromptDefinitionId = promptDefinition.Id,
                        Text = promptCatalogProperty.GetValue(_defaultPromptCatalogTypes)?.ToString() ?? ""
                    };

                    _logger.LogInformation("CreateDynamicDocumentProcessPromptsConsumer: Creating prompt implementation of prompt {PromptName} for DP {DocumentProcessShortname}", promptDefinition.ShortCode, documentProcess.ShortName);

                    await _promptImplementationGenericRepository.AddAsync(promptImplementation, false);
                    numberOfPromptImplementationsAdded++;
                }
            }
        }

        if (numberOfPromptImplementationsAdded > 0)
        {
            await _promptImplementationGenericRepository.SaveChangesAsync();
        }

        documentProcess.Status = DocumentProcessStatus.Active;

        await _documentProcessRepository.UpdateAsync(documentProcess, false);
        await _documentProcessRepository.SaveChangesAsync();
        _logger.LogInformation("CreateDynamicDocumentProcessPromptsConsumer: Created {NumberOfPromptImplementationsAdded} prompt implementations for DP {DocumentProcessShortname}", numberOfPromptImplementationsAdded, documentProcess.ShortName);
    }
}
