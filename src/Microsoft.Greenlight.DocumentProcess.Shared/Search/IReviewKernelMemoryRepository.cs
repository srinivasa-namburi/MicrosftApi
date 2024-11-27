using Microsoft.KernelMemory;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.Review;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Search;

public interface IReviewKernelMemoryRepository
{
    Task StoreDocumentForReview(Guid reviewRequestId, Stream fileStream, string fileName, string documentUrl, string? userId = null);
    Task StoreDocumentForReview(Guid reviewRequestId, string fullBlobUrl, string fileName,
        string? userId = null);
    Task<List<SortedDictionary<int, Citation.Partition>>> SearchInDocumentForReview(Guid reviewRequestId, string searchText, int top = 12, double minRelevance = 0.7);


    Task<MemoryAnswer> AskInDocument(Guid reviewRequestId, ReviewQuestion reviewQuestion);
    Task<MemoryAnswer> AskInDocument(Guid reviewRequestId, ReviewQuestionInfo reviewQuestion);
}


