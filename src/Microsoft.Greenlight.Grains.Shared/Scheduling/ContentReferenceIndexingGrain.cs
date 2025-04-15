using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling
{
    [Reentrant]
    public class ContentReferenceIndexingGrain : Grain, IContentReferenceIndexingGrain
    {
        private readonly ILogger<ContentReferenceIndexingGrain> _logger;
        private readonly IServiceProvider _sp;

        public ContentReferenceIndexingGrain(
            ILogger<ContentReferenceIndexingGrain> logger,
            IServiceProvider sp)
        {
            _logger = logger;
            _sp = sp;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Content reference indexing job started at {time}", DateTimeOffset.Now);

            try
            {
                // Necessary to use scoped services
                using var scope = _sp.CreateScope();
                var referenceService = scope.ServiceProvider.GetRequiredService<IContentReferenceService>();
                
                try
                {
                    await referenceService.ScanAndUpdateReferencesAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Content reference indexing experienced inconsistencies");
                }

                _logger.LogInformation("Content reference indexing completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during content reference indexing");
            }
        }
    }
}