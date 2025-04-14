using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Grains.Review.Contracts.State;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;

namespace Microsoft.Greenlight.Shared.Services
{
    /// <inheritdoc />
    public class ReviewService : IReviewService
    {
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<ReviewService> _logger;

        /// <summary>
        /// Constructor for the <see cref="ReviewService"/> class.
        /// </summary>
        /// <param name="clusterClient">Orleans Cluster Client</param>
        /// <param name="logger">Logger</param>
        public ReviewService(
            IClusterClient clusterClient,
            ILogger<ReviewService> logger)
        {
            _clusterClient = clusterClient;
            _logger = logger;
        }

        /// <summary>
        /// Starts the review execution process for the given review instance.
        /// </summary>
        /// <param name="reviewInstanceId">The ID of the review instance.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task<bool> ExecuteReviewAsync(Guid reviewInstanceId)
        {
            try
            {
                _logger.LogInformation("Starting review execution for review instance {ReviewInstanceId}", reviewInstanceId);

                var grain = _clusterClient.GetGrain<IReviewExecutionOrchestrationGrain>(reviewInstanceId);
                var request = new ExecuteReviewRequest(reviewInstanceId);

                _ = grain.ExecuteReviewAsync(request);

                _logger.LogInformation("Review execution started successfully for review instance {ReviewInstanceId}", reviewInstanceId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start review execution for review instance {ReviewInstanceId}", reviewInstanceId);
                return false;
            }
        }

        /// <summary>
        /// Gets the current state of the review execution process.
        /// </summary>
        /// <param name="reviewInstanceId">The ID of the review instance.</param>
        /// <returns>The current state of the review execution process.</returns>
        public async Task<ReviewExecutionState> GetReviewExecutionStateAsync(Guid reviewInstanceId)
        {
            try
            {
                _logger.LogInformation("Fetching review execution state for review instance {ReviewInstanceId}", reviewInstanceId);

                var grain = _clusterClient.GetGrain<IReviewExecutionOrchestrationGrain>(reviewInstanceId);
                var state = await grain.GetStateAsync();

                _logger.LogInformation("Fetched review execution state for review instance {ReviewInstanceId}", reviewInstanceId);
                return state;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch review execution state for review instance {ReviewInstanceId}", reviewInstanceId);
                throw;
            }
        }
    }

    /// <summary>
    /// Service to manage review execution processes.
    /// </summary>
    public interface IReviewService
    {
        /// <summary>
        /// Starts the review execution process for the given review instance.
        /// </summary>
        /// <param name="reviewInstanceId">The ID of the review instance.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<bool> ExecuteReviewAsync(Guid reviewInstanceId);

        /// <summary>
        /// Gets the current state of the review execution process.
        /// </summary>
        /// <param name="reviewInstanceId">The ID of the review instance.</param>
        /// <returns>The current state of the review execution process.</returns>
        Task<ReviewExecutionState> GetReviewExecutionStateAsync(Guid reviewInstanceId);
    }
}
