// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Batch processing service for ingesting multiple documents efficiently.
/// Provides performance optimizations for bulk document ingestion scenarios.
/// </summary>
public interface IBatchDocumentIngestionService
{
    /// <summary>
    /// Ingests multiple documents in a batch operation for better performance.
    /// </summary>
    /// <param name="documents">Collection of documents to ingest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch ingestion results.</returns>
    Task<BatchDocumentIngestionResult> IngestDocumentsBatchAsync(
        IEnumerable<BatchDocumentRequest> documents, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes multiple documents in a batch operation.
    /// </summary>
    /// <param name="deleteRequests">Collection of documents to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch deletion results.</returns>
    Task<BatchDocumentIngestionResult> DeleteDocumentsBatchAsync(
        IEnumerable<BatchDocumentDeleteRequest> deleteRequests,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request object for batch document ingestion.
/// </summary>
public class BatchDocumentRequest
{
    /// <summary>
    /// Document ID.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// File stream containing document content.
    /// </summary>
    public required Stream FileStream { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Document URL.
    /// </summary>
    public required string DocumentUrl { get; set; }

    /// <summary>
    /// Document library name.
    /// </summary>
    public required string DocumentLibraryName { get; set; }

    /// <summary>
    /// Index name.
    /// </summary>
    public required string IndexName { get; set; }

    /// <summary>
    /// User ID.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Additional metadata tags.
    /// </summary>
    public Dictionary<string, string>? AdditionalTags { get; set; }
}

/// <summary>
/// Request object for batch document deletion.
/// </summary>
public class BatchDocumentDeleteRequest
{
    /// <summary>
    /// Document library name.
    /// </summary>
    public required string DocumentLibraryName { get; set; }

    /// <summary>
    /// Index name.
    /// </summary>
    public required string IndexName { get; set; }

    /// <summary>
    /// File name to delete.
    /// </summary>
    public required string FileName { get; set; }
}

/// <summary>
/// Result of a batch document ingestion operation.
/// </summary>
public class BatchDocumentIngestionResult
{
    /// <summary>
    /// Overall success status of the batch operation.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of documents successfully processed.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of documents that failed processing.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Total number of chunks created across all documents.
    /// </summary>
    public int TotalChunkCount { get; set; }

    /// <summary>
    /// Total size in bytes of all processed documents.
    /// </summary>
    public long TotalDocumentSizeBytes { get; set; }

    /// <summary>
    /// Individual results for each document.
    /// </summary>
    public List<DocumentIngestionResult> IndividualResults { get; set; } = new();

    /// <summary>
    /// Processing time for the entire batch.
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Creates a successful batch result.
    /// </summary>
    public static BatchDocumentIngestionResult CreateSuccess(
        int successCount, 
        int totalChunkCount, 
        long totalSizeBytes, 
        TimeSpan processingTime)
    {
        return new BatchDocumentIngestionResult
        {
            Success = true,
            SuccessCount = successCount,
            FailureCount = 0,
            TotalChunkCount = totalChunkCount,
            TotalDocumentSizeBytes = totalSizeBytes,
            ProcessingTime = processingTime
        };
    }

    /// <summary>
    /// Creates a partial success batch result.
    /// </summary>
    public static BatchDocumentIngestionResult CreatePartial(
        int successCount,
        int failureCount,
        int totalChunkCount,
        long totalSizeBytes,
        TimeSpan processingTime,
        List<DocumentIngestionResult> individualResults)
    {
        return new BatchDocumentIngestionResult
        {
            Success = failureCount == 0,
            SuccessCount = successCount,
            FailureCount = failureCount,
            TotalChunkCount = totalChunkCount,
            TotalDocumentSizeBytes = totalSizeBytes,
            ProcessingTime = processingTime,
            IndividualResults = individualResults
        };
    }
}

/// <summary>
/// Implementation of batch document ingestion service.
/// </summary>
public class BatchDocumentIngestionService : IBatchDocumentIngestionService
{
    private readonly IDocumentIngestionService _documentIngestionService;
    private readonly ILogger<BatchDocumentIngestionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchDocumentIngestionService"/> class.
    /// </summary>
    /// <param name="documentIngestionService">Underlying single-document ingestion service.</param>
    /// <param name="logger">Logger instance.</param>
    public BatchDocumentIngestionService(
        IDocumentIngestionService documentIngestionService,
        ILogger<BatchDocumentIngestionService> logger)
    {
        _documentIngestionService = documentIngestionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BatchDocumentIngestionResult> IngestDocumentsBatchAsync(
        IEnumerable<BatchDocumentRequest> documents, 
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var documentsList = documents.ToList();
        
        _logger.LogInformation("Starting batch ingestion of {DocumentCount} documents", documentsList.Count);

        var individualResults = new List<DocumentIngestionResult>();
        var successCount = 0;
        var failureCount = 0;
        var totalChunkCount = 0;
        long totalSizeBytes = 0;

        // Process documents in parallel with controlled concurrency
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2); // Limit concurrent operations
        var tasks = documentsList.Select(async document =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await _documentIngestionService.IngestDocumentAsync(
                    document.DocumentId,
                    document.FileStream,
                    document.FileName,
                    document.DocumentUrl,
                    document.DocumentLibraryName,
                    document.IndexName,
                    document.UserId,
                    document.AdditionalTags);

                lock (individualResults)
                {
                    individualResults.Add(result);
                    if (result.Success)
                    {
                        successCount++;
                        totalChunkCount += result.ChunkCount;
                        totalSizeBytes += result.DocumentSizeBytes;
                    }
                    else
                    {
                        failureCount++;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                var errorResult = DocumentIngestionResult.Fail($"Batch processing error: {ex.Message}");
                
                lock (individualResults)
                {
                    individualResults.Add(errorResult);
                    failureCount++;
                }

                _logger.LogError(ex, "Failed to process document {FileName} in batch", document.FileName);
                return errorResult;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var processingTime = DateTime.UtcNow - startTime;

        _logger.LogInformation(
            "Completed batch ingestion: {SuccessCount} successful, {FailureCount} failed, {TotalChunkCount} chunks, processed in {ProcessingTime}ms",
            successCount, failureCount, totalChunkCount, processingTime.TotalMilliseconds);

    return BatchDocumentIngestionResult.CreatePartial(
            successCount,
            failureCount,
            totalChunkCount,
            totalSizeBytes,
            processingTime,
            individualResults);
    }

    /// <inheritdoc />
    public async Task<BatchDocumentIngestionResult> DeleteDocumentsBatchAsync(
        IEnumerable<BatchDocumentDeleteRequest> deleteRequests,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var requestsList = deleteRequests.ToList();
        
        _logger.LogInformation("Starting batch deletion of {DocumentCount} documents", requestsList.Count);

        var individualResults = new List<DocumentIngestionResult>();
        var successCount = 0;
        var failureCount = 0;

        // Process deletions in parallel with controlled concurrency
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2);
        var tasks = requestsList.Select(async request =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await _documentIngestionService.DeleteDocumentAsync(
                    request.DocumentLibraryName,
                    request.IndexName,
                    request.FileName);

                lock (individualResults)
                {
                    individualResults.Add(result);
                    if (result.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failureCount++;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                var errorResult = DocumentIngestionResult.Fail($"Batch deletion error: {ex.Message}");
                
                lock (individualResults)
                {
                    individualResults.Add(errorResult);
                    failureCount++;
                }

                _logger.LogError(ex, "Failed to delete document {FileName} in batch", request.FileName);
                return errorResult;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var processingTime = DateTime.UtcNow - startTime;

        _logger.LogInformation(
            "Completed batch deletion: {SuccessCount} successful, {FailureCount} failed, processed in {ProcessingTime}ms",
            successCount, failureCount, processingTime.TotalMilliseconds);

    return BatchDocumentIngestionResult.CreatePartial(
            successCount,
            failureCount,
            0, // No chunks for deletion
            0, // No size for deletion
            processingTime,
            individualResults);
    }
}
