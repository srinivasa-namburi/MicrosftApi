// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.DocumentProcess.Dynamic;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;

/// <summary>
/// Body text generator that uses search/RAG to gather heterogeneous source references
/// and delegates drafting to the configured IAiCompletionService for the document process.
/// Formerly known as KernelMemoryBodyTextGenerator, now generalized beyond KM.
/// </summary>
public class RagBodyTextGenerator : IBodyTextGenerator
{
    private readonly IConsolidatedSearchOptionsFactory _searchOptionsFactory;
    private readonly DynamicDocumentProcessServiceFactory _dpServiceFactory;
    private IDocumentRepository? _documentRepository;
    private string _documentProcessName = string.Empty;
    private readonly ILogger<RagBodyTextGenerator> _logger;
    private readonly IServiceProvider _sp;

    /// <summary>
    /// Creates a new RagBodyTextGenerator.
    /// </summary>
    public RagBodyTextGenerator(
        IConsolidatedSearchOptionsFactory searchOptionsFactory,
        DynamicDocumentProcessServiceFactory dpServiceFactory,
        ILogger<RagBodyTextGenerator> logger,
        IServiceProvider sp)
    {
        _searchOptionsFactory = searchOptionsFactory;
        _dpServiceFactory = dpServiceFactory;
        _logger = logger;
        _sp = sp;
    }

    /// <inheritdoc />
    public async Task<List<ContentNode>> GenerateBodyText(string contentNodeTypeString, string sectionNumber,
        string sectionTitle, string tableOfContentsString, string documentProcessName, Guid? metadataId,
        ContentNode? sectionContentNode)
    {
        _logger.LogInformation("Starting body text generation for document process {DocumentProcessName}, section {SectionNumber} - {SectionTitle}", 
            documentProcessName, sectionNumber, sectionTitle);

        try
        {
            _documentProcessName = documentProcessName;
            
            _logger.LogDebug("Getting document repository for document process {DocumentProcessName}", _documentProcessName);
            _documentRepository = await _dpServiceFactory.GetServiceAsync<IDocumentRepository>(_documentProcessName);
            
            if (_documentRepository == null)
            {
                _logger.LogError("RagBodyTextGenerator: Document repository is null for document process {DocumentProcessName}.", _documentProcessName);
                throw new InvalidOperationException($"Document repository is not available for document process {_documentProcessName}.");
            }

            _logger.LogDebug("Successfully obtained document repository for document process {DocumentProcessName}: {RepositoryType}", 
                _documentProcessName, _documentRepository.GetType().Name);

            _logger.LogDebug("Getting AI completion service for document process {DocumentProcessName}", _documentProcessName);
            var aiCompletionService = await _dpServiceFactory.GetServiceAsync<IAiCompletionService>(_documentProcessName);

            if (aiCompletionService == null)
            {
                _logger.LogError("RagBodyTextGenerator: AI completion service is null for document process {DocumentProcessName}.", _documentProcessName);
                throw new InvalidOperationException($"AI completion service is not available for document process {_documentProcessName}.");
            }

            _logger.LogDebug("Successfully obtained AI completion service for document process {DocumentProcessName}: {ServiceType}", 
                _documentProcessName, aiCompletionService.GetType().Name);

            var contentNodeType = Enum.Parse<ContentNodeType>(contentNodeTypeString);
            
            _logger.LogDebug("Searching for documents for query: section {SectionNumber} - {SectionTitle}", sectionNumber, sectionTitle);
            var documents = await GetDocumentsForQuery(contentNodeType, sectionNumber, sectionTitle);
            
            _logger.LogInformation("Found {DocumentCount} documents for section {SectionNumber} - {SectionTitle}", 
                documents.Count, sectionNumber, sectionTitle);

            _logger.LogDebug("Calling AI completion service to generate body content nodes");
            var result = await aiCompletionService.GetBodyContentNodes(
                documents,
                sectionNumber,
                sectionTitle,
                contentNodeType,
                tableOfContentsString,
                metadataId,
                sectionContentNode);

            _logger.LogInformation("Successfully generated {ContentNodeCount} body content nodes for section {SectionNumber} - {SectionTitle}", 
                result.Count, sectionNumber, sectionTitle);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating body text for document process {DocumentProcessName}, section {SectionNumber} - {SectionTitle}", 
                _documentProcessName, sectionNumber, sectionTitle);
            throw;
        }
    }

    private async Task<List<SourceReferenceItem>> GetDocumentsForQuery(ContentNodeType contentNodeType, string sectionOrTitleNumber,
        string sectionOrTitleText)
    {
        try
        {
            _logger.LogDebug("Creating search options for document process {DocumentProcessName}", _documentProcessName);
            var searchOptions =
                await _searchOptionsFactory.CreateSearchOptionsForDocumentProcessAsync(_documentProcessName);

            _logger.LogDebug("Created search options for document process {DocumentProcessName}: IndexName={IndexName}, Top={Top}, MinRelevance={MinRelevance}", 
                _documentProcessName, searchOptions.IndexName, searchOptions.Top, searchOptions.MinRelevance);

            var query = sectionOrTitleNumber + " " + sectionOrTitleText;
            _logger.LogDebug("Executing search query: '{Query}' for document process {DocumentProcessName}", query, _documentProcessName);

            // Enable progressive search for SemanticKernelVectorStore repositories automatically
            if (IsSemanticKernelVectorStoreProcess())
            {
                searchOptions.EnableProgressiveSearch = true;
                _logger.LogDebug("Enabled progressive search for SemanticKernelVectorStore process {DocumentProcessName}", _documentProcessName);
            }

            var resultItems = await _documentRepository!.SearchAsync(
                _documentProcessName, query, searchOptions);

            _logger.LogInformation("Search completed for document process {DocumentProcessName}, query '{Query}': found {ResultCount} results", 
                _documentProcessName, query, resultItems.Count);

            // For SemanticKernelVectorStore processes, the progressive search is now handled automatically
            // by the repository, so we don't need the complex fallback logic here anymore
            if (resultItems.Count == 0)
            {
                _logger.LogWarning("No search results found for document process {DocumentProcessName}, query '{Query}' even with progressive search.", 
                    _documentProcessName, query);
            }

            // Return results; downstream services accept SourceReferenceItem
            return resultItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for documents with query '{Query}' in document process {DocumentProcessName}", 
                sectionOrTitleNumber + " " + sectionOrTitleText, _documentProcessName);
            throw;
        }
    }

    private bool IsSemanticKernelVectorStoreProcess()
    {
        // We can't easily get the document process info here without an async call,
        // but we can detect if the repository is a SemanticKernelVectorStoreRepository
        return _documentRepository?.GetType().Name == "SemanticKernelVectorStoreRepository";
    }
}
