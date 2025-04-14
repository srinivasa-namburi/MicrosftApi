using Microsoft.Greenlight.Grains.Review.Contracts.Models;
using Microsoft.Greenlight.Grains.Review.Contracts.State;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;
using Orleans;

namespace Microsoft.Greenlight.Grains.Review.Contracts
{
    public interface IReviewExecutionOrchestrationGrain : IGrainWithGuidKey
    {
        Task<ReviewExecutionState> GetStateAsync();
        Task ExecuteReviewAsync(ExecuteReviewRequest request);
        Task OnDocumentIngestedAsync(ReviewDocumentIngestionResult ingestionResult);
        Task OnQuestionsDistributedAsync();
        Task OnQuestionAnsweredAsync(Guid questionAnswerId);
        Task OnQuestionAnswerAnalyzedAsync(Guid questionAnswerId);
    }
}