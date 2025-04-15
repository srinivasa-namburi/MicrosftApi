using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Services;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling
{
    [Reentrant]
    public class PromptDefinitionsUpdateGrain : Grain, IPromptDefinitionsUpdateGrain
    {
        private readonly ILogger<PromptDefinitionsUpdateGrain> _logger;
        private readonly IPromptDefinitionService _promptDefinitionService;

        public PromptDefinitionsUpdateGrain(
            ILogger<PromptDefinitionsUpdateGrain> logger,
            IPromptDefinitionService promptDefinitionService)
        {
            _logger = logger;
            _promptDefinitionService = promptDefinitionService;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Prompt definitions update job started at {time}", DateTimeOffset.Now);

            try
            {
                await _promptDefinitionService.EnsurePromptDefinitionsAsync();
                _logger.LogInformation("Prompt definitions update completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating prompt definitions");
            }
        }
    }
}