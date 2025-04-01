using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Quartz;

namespace Microsoft.Greenlight.Worker.Scheduler.Jobs
{
    /// <summary>
    /// Periodically scans and updates content references.
    /// </summary>
    public class ContentReferenceIndexingJob : IJob
    {
        private readonly ILogger<ContentReferenceIndexingJob> _logger;
        private readonly IContentReferenceService _referenceService;
        private readonly IOptionsSnapshot<ServiceConfigurationOptions> _options;

        /// <summary>
        /// Constructs a new instance of the <see cref="ContentReferenceIndexingJob"/> class.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="referenceService"></param>
        /// <param name="options"></param>
        public ContentReferenceIndexingJob(
            ILogger<ContentReferenceIndexingJob> logger,
            IContentReferenceService referenceService,
            IOptionsSnapshot<ServiceConfigurationOptions> options)
        {
            _logger = logger;
            _referenceService = referenceService;
            _options = options;
        }

        /// <inheritdoc />
        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("Content reference indexing job started at {time}", DateTimeOffset.Now);

            try
            {
                await _referenceService.ScanAndUpdateReferencesAsync(context.CancellationToken);
                _logger.LogInformation("Content reference indexing completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during content reference indexing");
            }
        }
    }
}