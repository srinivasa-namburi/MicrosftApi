using Microsoft.Greenlight.Shared.Services;
using Quartz;

public class CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob : IJob
{
    private readonly ILogger<CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob> _logger;
    private readonly IPromptDefinitionService _promptDefinitionService;

    public CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob(
        ILogger<CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob> logger,
        IPromptDefinitionService promptDefinitionService)
    {
        _logger = logger;
        _promptDefinitionService = promptDefinitionService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob started at {time}", DateTimeOffset.Now);

        try
        {
            await _promptDefinitionService.EnsurePromptDefinitionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob execution");
            throw; // rethrow if you want Quartz to record a job failure
        }
    }
}