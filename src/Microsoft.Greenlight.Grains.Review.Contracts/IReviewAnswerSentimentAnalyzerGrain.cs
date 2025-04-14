using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Orleans;

namespace Microsoft.Greenlight.Grains.Review.Contracts
{
    public interface IReviewAnswerSentimentAnalyzerGrain : IGrainWithGuidKey
    {
        Task<GenericResult> AnalyzeSentimentAsync();
    }
}