using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.Shared.Services.Search;

public interface IReviewKernelMemoryRepository
{
    Task StoreDocumentForReview(Guid reviewRequestId, Stream fileStream, string fileName, string documentUrl, string? userId = null);
   
    Task<MemoryAnswer> AskInDocument(Guid reviewRequestId, ReviewQuestion reviewQuestion);
    Task<MemoryAnswer> AskInDocument(Guid reviewRequestId, ReviewQuestionInfo reviewQuestion);
    Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchInDocumentForReview(Guid reviewRequestId, string searchText, int top = 12, double minRelevance = 0.7);
}


