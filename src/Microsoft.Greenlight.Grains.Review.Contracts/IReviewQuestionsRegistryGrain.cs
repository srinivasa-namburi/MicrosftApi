using Microsoft.Greenlight.Shared.Contracts.DTO;
using Orleans;

namespace Microsoft.Greenlight.Grains.Review.Contracts
{
    public interface IReviewQuestionsRegistryGrain : IGrainWithGuidKey
    {
        Task RegisterQuestionsAsync(List<ReviewQuestionInfo> questions);
        Task<List<ReviewQuestionInfo>> GetQuestionsAsync();
    }
}