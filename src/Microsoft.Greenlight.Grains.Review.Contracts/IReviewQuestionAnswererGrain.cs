using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Orleans;

namespace Microsoft.Greenlight.Grains.Review.Contracts
{
    public interface IReviewQuestionAnswererGrain : IGrainWithGuidKey
    {
        Task<GenericResult> AnswerQuestionAsync(Guid reviewInstanceId, ReviewQuestionInfo question);
    }
}