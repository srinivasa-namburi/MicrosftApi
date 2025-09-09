// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.DocumentLibrary;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Shared.Services.FileStorage;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Factory for creating the appropriate document repository implementation based on LogicType.
/// </summary>
public interface IDocumentRepositoryFactory
{
    /// <summary>
    /// Creates a document repository for the specified document process.
    /// </summary>
    /// <param name="documentProcess">Document process information.</param>
    /// <returns>Appropriate document repository implementation.</returns>
    Task<IDocumentRepository> CreateForDocumentProcessAsync(DocumentProcessInfo documentProcess);

    /// <summary>
    /// Creates a document repository for the specified document library.
    /// </summary>
    /// <param name="documentLibraryName">Document library name.</param>
    /// <returns>Appropriate document repository implementation.</returns>
    Task<IDocumentRepository> CreateForDocumentLibraryAsync(string documentLibraryName);
}

/// <summary>
/// Factory implementation that chooses between Kernel Memory and Semantic Kernel Vector Store.
/// </summary>
public class DocumentRepositoryFactory : IDocumentRepositoryFactory
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly ILogger<DocumentRepositoryFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentRepositoryFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies.</param>
    /// <param name="documentLibraryInfoService">Document library info service.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="dbContextFactory">EF DbContext factory for backend model access.</param>
    public DocumentRepositoryFactory(
        IServiceProvider serviceProvider,
        IDocumentLibraryInfoService documentLibraryInfoService,
        ILogger<DocumentRepositoryFactory> logger,
        IDbContextFactory<DocGenerationDbContext>? dbContextFactory = null)
    {
        _scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _documentLibraryInfoService = documentLibraryInfoService;
        _logger = logger;
        if (dbContextFactory != null)
        {
            _dbContextFactory = dbContextFactory;
        }
        else
        {
            using var scope = _scopeFactory.CreateScope();
            _dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DocGenerationDbContext>>();
        }
    }

    /// <summary>
    /// Creates a document repository for the specified document process.
    /// </summary>
    public async Task<IDocumentRepository> CreateForDocumentProcessAsync(DocumentProcessInfo documentProcess)
    {
        _logger.LogInformation("Creating document repository for process {DocumentProcessName} with LogicType {LogicType}",
            documentProcess.ShortName, documentProcess.LogicType);

        return documentProcess.LogicType switch
        {
            DocumentProcessLogicType.KernelMemory => CreateKernelMemoryRepository(documentProcess.ShortName),
            DocumentProcessLogicType.SemanticKernelVectorStore => await CreateSemanticKernelVectorStoreRepositoryAsync(documentProcess.ShortName),
            DocumentProcessLogicType.Classic => throw new NotSupportedException("Classic document processing logic type is not supported by document repositories"),
            _ => throw new ArgumentException($"Unknown document process logic type: {documentProcess.LogicType}")
        };
    }

    /// <summary>
    /// Creates a document repository for the specified document library.
    /// </summary>
    public async Task<IDocumentRepository> CreateForDocumentLibraryAsync(string documentLibraryName)
    {
        _logger.LogInformation("Creating document repository for library {DocumentLibraryName}", documentLibraryName);

        var documentLibrary = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryName);

        if (documentLibrary == null)
        {
            _logger.LogWarning("Document library {DocumentLibraryName} not found, defaulting to Kernel Memory", documentLibraryName);
            return CreateKernelMemoryRepository(documentLibraryName);
        }

        _logger.LogInformation("Document library {DocumentLibraryName} found with LogicType {LogicType}",
            documentLibraryName, documentLibrary.LogicType);

        return documentLibrary.LogicType switch
        {
            DocumentProcessLogicType.KernelMemory => CreateKernelMemoryRepository(documentLibraryName),
            DocumentProcessLogicType.SemanticKernelVectorStore => await CreateSemanticKernelVectorStoreRepositoryAsync(documentLibraryName),
            DocumentProcessLogicType.Classic => throw new NotSupportedException("Classic logic type not supported for document libraries"),
            _ => CreateKernelMemoryRepository(documentLibraryName)
        };
    }

    private IDocumentRepository CreateKernelMemoryRepository(string contextName)
    {
        _logger.LogDebug("Creating Kernel Memory repository for context {ContextName}", contextName);
        using var scope = _scopeFactory.CreateScope();
        var kmRepository = scope.ServiceProvider.GetRequiredService<IKernelMemoryRepository>();
        return new KernelMemoryDocumentRepositoryAdapter(kmRepository);
    }

    private async Task<IDocumentRepository> CreateSemanticKernelVectorStoreRepositoryAsync(string contextName)
    {
        _logger.LogInformation("Creating Semantic Kernel Vector Store repository for context {ContextName}", contextName);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var logger = sp.GetRequiredService<ILogger<SemanticKernelVectorStoreRepository>>();
            var rootOptions = sp.GetRequiredService<IOptionsSnapshot<ServiceConfigurationOptions>>();
            var globalOptions = rootOptions.Value.GreenlightServices.VectorStore;

            _logger.LogDebug("Retrieved global vector store options for context {ContextName}", contextName);

            VectorStoreDocumentProcessOptions? processOptions = null;
            TextChunkingMode effectiveChunkingMode = TextChunkingMode.Simple;
            DocumentLibraryType? documentLibraryType = null;

            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync();

                // 1) Prefer backend DynamicDocumentProcessDefinition when contextName is a document process
                DynamicDocumentProcessDefinition? processModel = await db.DynamicDocumentProcessDefinitions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ShortName == contextName);
                if (processModel != null && processModel.LogicType == DocumentProcessLogicType.SemanticKernelVectorStore)
                {
                    processOptions = globalOptions.FromDocumentProcess(processModel);
                    effectiveChunkingMode = processOptions.GetEffectiveChunkingMode();
                    documentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary;
                    _logger.LogDebug("Using DP model options for {ContextName}: ChunkSize={ChunkSize}, ChunkOverlap={ChunkOverlap}, Mode={Mode}",
                        contextName, processOptions.GetEffectiveChunkSize(), processOptions.GetEffectiveChunkOverlap(), effectiveChunkingMode);
                }

                // 2) If not a document process, check if this is a document library and prepare library-scoped options
                if (processOptions == null)
                {
                    DocumentLibrary? libraryModel = await db.DocumentLibraries
                        .AsNoTracking()
                        .FirstOrDefaultAsync(l => l.ShortName == contextName);
                    if (libraryModel != null && libraryModel.LogicType == DocumentProcessLogicType.SemanticKernelVectorStore)
                    {
                        processOptions = globalOptions.FromDocumentLibrary(libraryModel);
                        effectiveChunkingMode = processOptions.GetEffectiveChunkingMode();
                        documentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary;
                        _logger.LogDebug("Using library model options for {ContextName}: ChunkSize={ChunkSize}, ChunkOverlap={ChunkOverlap}, Mode={Mode}",
                            contextName, processOptions.GetEffectiveChunkSize(), processOptions.GetEffectiveChunkOverlap(), effectiveChunkingMode);
                    }
                }

                if (processOptions == null)
                {
                    _logger.LogWarning("No specific options found for context {ContextName}, using global defaults", contextName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get backend model configuration for {ContextName}, falling back to defaults", contextName);
            }

            _logger.LogDebug("Resolving required services for Semantic Kernel Vector Store repository");

            var aiEmbeddingService = sp.GetRequiredService<IAiEmbeddingService>();
            var provider = sp.GetRequiredService<ISemanticKernelVectorStoreProvider>();
            var textExtractionService = sp.GetRequiredService<ITextExtractionService>();

            ITextChunkingService textChunkingService = effectiveChunkingMode == Microsoft.Greenlight.Shared.Enums.TextChunkingMode.Semantic
                ? sp.GetRequiredService<SemanticTextChunkingService>()
                : sp.GetRequiredService<ChunkingService>();

            var kernelFactory = sp.GetService<IKernelFactory>();
            var fileUrlResolver = sp.GetRequiredService<IFileUrlResolverService>();

            _logger.LogDebug("All required services resolved successfully for context {ContextName}", contextName);

            IDocumentRepository repo = new SemanticKernelVectorStoreRepository(
                logger,
                rootOptions,
                aiEmbeddingService,
                provider,
                textExtractionService,
                textChunkingService,
                fileUrlResolver,
                processOptions,
                kernelFactory,
                documentLibraryType);

            _logger.LogInformation("Successfully created Semantic Kernel Vector Store repository for context {ContextName} with type {DocumentLibraryType}", contextName, documentLibraryType);
            return repo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Semantic Kernel Vector Store repository for context {ContextName}", contextName);
            throw;
        }
    }
}
