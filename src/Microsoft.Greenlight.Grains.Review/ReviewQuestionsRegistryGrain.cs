using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Grains.Review.Contracts.State;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Review
{
    [Reentrant]
    public class ReviewQuestionsRegistryGrain : Grain, IReviewQuestionsRegistryGrain
    {
        private readonly IPersistentState<ReviewQuestionsState> _state;
        private readonly ILogger<ReviewQuestionsRegistryGrain> _logger;

        public ReviewQuestionsRegistryGrain(
            [PersistentState("reviewQuestionsRegistry")]
            IPersistentState<ReviewQuestionsState> state,
            ILogger<ReviewQuestionsRegistryGrain> logger)
        {
            _state = state;
            _logger = logger;
        }

        public Task RegisterQuestionsAsync(List<ReviewQuestionInfo> questions)
        {
            _logger.LogInformation("Registering {Count} questions for review instance {ReviewInstanceId}",
                questions.Count, this.GetPrimaryKey());
            
            _state.State.Questions = questions;
            return _state.WriteStateAsync();
        }

        public Task<List<ReviewQuestionInfo>> GetQuestionsAsync()
        {
            return Task.FromResult(_state.State.Questions);
        }
    }
}