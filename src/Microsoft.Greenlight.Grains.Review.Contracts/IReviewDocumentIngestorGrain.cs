using Microsoft.Greenlight.Grains.Review.Contracts.Models;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Orleans;

namespace Microsoft.Greenlight.Grains.Review.Contracts
{
    public interface IReviewDocumentIngestorGrain : IGrainWithGuidKey
    {
        Task<GenericResult<ReviewDocumentIngestionResult>> IngestDocumentAsync();
    }
}