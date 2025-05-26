// Copyright (c) Microsoft Corporation. All rights reserved.
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Ingestion;

[Reentrant]
public class DocumentProcessorGrain : Grain, IDocumentProcessorGrain
{
    private readonly ILogger<DocumentProcessorGrain> _logger;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<Microsoft.Greenlight.Shared.Data.Sql.DocGenerationDbContext> _dbContextFactory;
    private bool _isRunning; // In-memory, not persisted
    
    public DocumentProcessorGrain(
        ILogger<DocumentProcessorGrain> logger,
        IDocumentProcessInfoService documentProcessInfoService,
        IServiceProvider serviceProvider,
        IDbContextFactory<Microsoft.Greenlight.Shared.Data.Sql.DocGenerationDbContext> dbContextFactory)
    {
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
        _serviceProvider = serviceProvider;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<DocumentProcessResult> ProcessDocumentAsync(Guid documentId)
    {
        if (_isRunning)
        {
            _logger.LogInformation("Processing already running for file {FileId}, skipping.", documentId);
            return DocumentProcessResult.Fail("Processing already running.");
        }
        _isRunning = true;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var entity = await db.IngestedDocuments.FindAsync(documentId);
            if (entity == null)
            {
                _logger.LogError("IngestedDocument with Id {Id} not found in DB for processing.", documentId);
                return DocumentProcessResult.Fail($"IngestedDocument with Id {documentId} not found in DB.");
            }
            string fileName = entity.FileName;
            string documentUrl = entity.FinalBlobUrl ?? entity.OriginalDocumentUrl;
            string documentLibraryShortName = entity.DocumentLibraryOrProcessName ?? string.Empty;
            DocumentLibraryType documentLibraryType = entity.DocumentLibraryType;
            string? uploadedByUserOid = entity.UploadedByUserOid;
            if (documentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary)
            {
                var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentLibraryShortName);
                if (documentProcess == null)
                {
                    _logger.LogError("Document process {DocumentProcessName} not found", documentLibraryShortName);
                    return DocumentProcessResult.Fail($"Document process {documentLibraryShortName} not found");
                }
                var repositoryName = documentProcess.Repositories.FirstOrDefault() ?? documentLibraryShortName;
                var kernelMemoryRepository = _serviceProvider.GetServiceForDocumentProcess<IKernelMemoryRepository>(documentLibraryShortName);
                if (kernelMemoryRepository == null)
                {
                    _logger.LogError("Failed to get Kernel Memory repository for document process {DocumentProcessName}", documentLibraryShortName);
                    return DocumentProcessResult.Fail($"Failed to get Kernel Memory repository for {documentLibraryShortName}");
                }
                await using var fileStream = await GetDocumentStreamAsync(documentUrl);
                if (fileStream == null)
                {
                    _logger.LogError("Failed to get document stream for {DocumentUrl}", documentUrl);
                    return DocumentProcessResult.Fail($"Failed to get document stream for {documentUrl}");
                }
                await kernelMemoryRepository.StoreContentAsync(
                    documentLibraryShortName,
                    repositoryName,
                    fileStream,
                    fileName,
                    documentUrl,
                    uploadedByUserOid);
            }
            else // AdditionalDocumentLibrary
            {
                var documentLibraryRepository = _serviceProvider.GetService<IAdditionalDocumentLibraryKernelMemoryRepository>();
                if (documentLibraryRepository == null)
                {
                    _logger.LogError("Additional Document Library KM Repository not found");
                    return DocumentProcessResult.Fail("Additional Document Library KM Repository not found");
                }
                await using var fileStream = await GetDocumentStreamAsync(documentUrl);
                if (fileStream == null)
                {
                    _logger.LogError("Failed to get document stream for {DocumentUrl}", documentUrl);
                    return DocumentProcessResult.Fail($"Failed to get document stream for {documentUrl}");
                }
                using var scope = _serviceProvider.CreateScope();
                var documentLibraryInfoService = scope.ServiceProvider.GetRequiredService<IDocumentLibraryInfoService>();
                var documentLibrary = await documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryShortName);
                if (documentLibrary == null)
                {
                    _logger.LogError("Document library {DocumentLibraryName} not found", documentLibraryShortName);
                    return DocumentProcessResult.Fail($"Document library {documentLibraryShortName} not found");
                }
                await documentLibraryRepository.StoreContentAsync(
                    documentLibraryShortName,
                    documentLibrary.IndexName,
                    fileStream,
                    fileName,
                    documentUrl,
                    uploadedByUserOid);
            }
            _logger.LogInformation("Successfully processed document {FileName} for {DocumentLibraryType} {DocumentLibraryName}", fileName, documentLibraryType, documentLibraryShortName);
            return DocumentProcessResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process document {DocumentId}", documentId);
            return DocumentProcessResult.Fail($"Failed to process document: {ex.Message}");
        }
        finally
        {
            _isRunning = false;
        }
    }

    private async Task<Stream> GetDocumentStreamAsync(string documentUrl)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var azureFileHelper = scope.ServiceProvider.GetRequiredService<AzureFileHelper>();
            return await azureFileHelper.GetFileAsStreamFromFullBlobUrlAsync(documentUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document stream for {DocumentUrl}", documentUrl);
            return null;
        }
    }
}