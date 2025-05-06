using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.Shared.Services.Search;

public class ReviewKernelMemoryRepository : IReviewKernelMemoryRepository
{
    private readonly IKernelMemoryRepository _kernelMemoryRepository;
    private readonly IConsolidatedSearchOptionsFactory _searchOptionsFactory;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<ReviewKernelMemoryRepository> _logger;
    private readonly AzureFileHelper _fileHelper;
    private readonly IMapper _mapper;

    public ReviewKernelMemoryRepository(
        [FromKeyedServices("Reviews-IKernelMemoryRepository")]
        IKernelMemoryRepository kernelMemoryRepository, 
        IConsolidatedSearchOptionsFactory searchOptionsFactory,
        DocGenerationDbContext dbContext, 
        ILogger<ReviewKernelMemoryRepository> logger,
        AzureFileHelper fileHelper,
        IMapper mapper
    )
    {
        _kernelMemoryRepository = kernelMemoryRepository;
        _searchOptionsFactory = searchOptionsFactory;
        _dbContext = dbContext;
        _logger = logger;
        _fileHelper = fileHelper;
        _mapper = mapper;
    }

    public async Task StoreDocumentForReview(Guid reviewRequestId, Stream fileStream, string fileName, string documentUrl,
        string? userId = null)
    {
        
        var reviewRequest = await _dbContext.ReviewInstances.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==reviewRequestId);
        if (reviewRequest == null)
        {
            _logger.LogError("ReviewRequest not found for ReviewRequestId: {ReviewRequestId}", reviewRequestId);
            throw new Exception("ReviewRequest not found");
        }
        _logger.LogInformation("Storing document for review. ReviewRequestId: {ReviewRequestId}, FileName: {FileName}, DocumentUrl: {DocumentUrl}, UserId: {UserId}", reviewRequestId, fileName, documentUrl, userId);
        var additionalTags = new Dictionary<string, string>
        {
            {"ReviewRequestId", reviewRequestId.ToString()}
        };

        await _kernelMemoryRepository.StoreContentAsync("Reviews", "index-reviews", fileStream, fileName, documentUrl, userId, additionalTags);
        _logger.LogInformation("Document stored successfully for review. ReviewRequestId: {ReviewRequestId}", reviewRequestId);
    }

    public async Task StoreDocumentForReview(Guid reviewRequestId, string fullBlobUrl, string fileName,
        string? userId = null)
    {
        _logger.LogInformation("Storing document for review from blob URL. ReviewRequestId: {ReviewRequestId}, FullBlobUrl: {FullBlobUrl}, FileName: {FileName}, UserId: {UserId}", reviewRequestId, fullBlobUrl, fileName, userId);

        var fileStream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(fullBlobUrl);
        if (fileStream == null)
        {
            _logger.LogError("File not found for FullBlobUrl: {FullBlobUrl}", fullBlobUrl);
            throw new Exception("File not found");
        }

        fileStream.Seek(0, SeekOrigin.Begin);
        await StoreDocumentForReview(reviewRequestId, fileStream, fileName, fullBlobUrl, userId);
    }

    public async Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchInDocumentForReview(Guid reviewRequestId, string searchText, int top = 12, double minRelevance = 0.7)
    {
        var reviewRequest = await _dbContext.ReviewInstances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reviewRequestId);
        if (reviewRequest == null)
        {
            _logger.LogError("ReviewRequest not found for ReviewRequestId: {ReviewRequestId}", reviewRequestId);
            throw new Exception("ReviewRequest not found");
        }

        _logger.LogInformation("Searching in document for review. ReviewRequestId: {ReviewRequestId}, SearchText: {SearchText}, Top: {Top}, MinRelevance: {MinRelevance}", reviewRequestId, searchText, top, minRelevance);

        var searchTags = new Dictionary<string, string>
        {
            {"ReviewRequestId", reviewRequestId.ToString()}
        };

        var searchOptions = await _searchOptionsFactory.CreateSearchOptionsForReviewsAsync(searchTags);
        var searchResults = await _kernelMemoryRepository.SearchAsync("Reviews", searchText, searchOptions);

        _logger.LogInformation("Search completed for ReviewRequestId: {ReviewRequestId}. Number of results: {ResultsCount}", reviewRequestId, searchResults.Count);

        return searchResults;
    }

    public async Task<MemoryAnswer> AskInDocument(Guid reviewRequestId, ReviewQuestionInfo reviewQuestion)
    {
        var searchTags = new Dictionary<string, string>
        {
            {"ReviewRequestId", reviewRequestId.ToString()}
        };

        MemoryAnswer? memoryResult = null;
        var reviewRequest = await _dbContext.ReviewInstances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reviewRequestId);
        if (reviewRequest == null)
        {
            _logger.LogError("ReviewRequest not found for ReviewRequestId: {ReviewRequestId}", reviewRequestId);
            throw new Exception("ReviewRequest not found");
        }

        _logger.LogInformation("Asking in document for review. ReviewRequestId: {ReviewRequestId}, Question: {Question}", reviewRequestId, reviewQuestion.Question);
        if (reviewQuestion.QuestionType == ReviewQuestionType.Question)
        {
            // The ReviewQuestion is an actual question - we can send it directly to Kernel Memory AskAsync
            memoryResult = await _kernelMemoryRepository.AskAsync("Reviews", "index-reviews", searchTags, reviewQuestion.Question);
        } else if (reviewQuestion.QuestionType == ReviewQuestionType.Requirement)
        {
            var prompt = $"""
                          Please formulate the following text as a question:

                          {reviewQuestion.Question}
                          """;

            // We just ask a question for now
            memoryResult = await _kernelMemoryRepository.AskAsync("Reviews", "index-reviews", searchTags, prompt);
        }
        else {
            _logger.LogError("Invalid ReviewQuestionType: {ReviewQuestionType}", reviewQuestion.QuestionType);
            throw new Exception("Invalid ReviewQuestionType");
        }

        if (memoryResult!.NoResult)
        {
            _logger.LogWarning("No answer found for ReviewRequestId: {ReviewRequestId}, Question: {Question}", reviewRequestId, reviewQuestion.Question);
            return memoryResult;
        }

        _logger.LogInformation("Answer found for ReviewRequestId: {ReviewRequestId}, Question: {Question}, Answer: {Answer}", reviewRequestId, reviewQuestion.Question, memoryResult.Result);
        return memoryResult;
    }

    public async Task<MemoryAnswer> AskInDocument(Guid reviewRequestId, ReviewQuestion reviewQuestion)
    {
        var reviewQuestionInfo = _mapper.Map<ReviewQuestionInfo>(reviewQuestion);
        var result = await AskInDocument(reviewRequestId, reviewQuestionInfo);
        return result;
    }
}
