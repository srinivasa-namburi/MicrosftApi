// Microsoft.Greenlight.Grains.Ingestion/DocumentProcessorGrain.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
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
    
    public DocumentProcessorGrain(
        ILogger<DocumentProcessorGrain> logger,
        IDocumentProcessInfoService documentProcessInfoService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
        _serviceProvider = serviceProvider;
    }
    
    public async Task ProcessDocumentAsync(
        string fileName,
        string documentUrl,
        string documentLibraryShortName,
        DocumentLibraryType documentLibraryType,
        Guid orchestrationGrainId,
        string? uploadedByUserOid = null)
    {
        // Get the orchestration grain using the passed ID instead of deriving from this grain's ID
        var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(orchestrationGrainId);
        
        try
        {
            // Determine repository based on document library type
            string repositoryName;
            
            if (documentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary)
            {
                // Get document process info to determine which repository to use
                var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentLibraryShortName);
                if (documentProcess == null)
                {
                    _logger.LogError("Document process {DocumentProcessName} not found", documentLibraryShortName);
                    await orchestrationGrain.OnIngestionFailedAsync($"Document process {documentLibraryShortName} not found");
                    return;
                }
                
                // Use the first repository by default
                repositoryName = documentProcess.Repositories.FirstOrDefault() ?? documentLibraryShortName;
                
                // Get the appropriate repository for Kernel Memory
                var kernelMemoryRepository = _serviceProvider.GetServiceForDocumentProcess<IKernelMemoryRepository>(documentLibraryShortName);
                if (kernelMemoryRepository == null)
                {
                    _logger.LogError("Failed to get Kernel Memory repository for document process {DocumentProcessName}", documentLibraryShortName);
                    await orchestrationGrain.OnIngestionFailedAsync($"Failed to get Kernel Memory repository for {documentLibraryShortName}");
                    return;
                }
                
                // Get document stream
                await using var fileStream = await GetDocumentStreamAsync(documentUrl);
                if (fileStream == null)
                {
                    _logger.LogError("Failed to get document stream for {DocumentUrl}", documentUrl);
                    await orchestrationGrain.OnIngestionFailedAsync($"Failed to get document stream for {documentUrl}");
                    return;
                }
                
                // Process the document with Kernel Memory
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
                // Get the appropriate document library repository
                var documentLibraryRepository = _serviceProvider.GetService<IAdditionalDocumentLibraryKernelMemoryRepository>();
                if (documentLibraryRepository == null)
                {
                    _logger.LogError("Additional Document Library KM Repository not found");
                    await orchestrationGrain.OnIngestionFailedAsync("Additional Document Library KM Repository not found");
                    return;
                }
                
                // Get document stream
                await using var fileStream = await GetDocumentStreamAsync(documentUrl);
                if (fileStream == null)
                {
                    _logger.LogError("Failed to get document stream for {DocumentUrl}", documentUrl);
                    await orchestrationGrain.OnIngestionFailedAsync($"Failed to get document stream for {documentUrl}");
                    return;
                }
                
                // Get document library to determine index name
                using var scope = _serviceProvider.CreateScope();
                var documentLibraryInfoService = scope.ServiceProvider.GetRequiredService<IDocumentLibraryInfoService>();
                var documentLibrary = await documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryShortName);
                
                if (documentLibrary == null)
                {
                    _logger.LogError("Document library {DocumentLibraryName} not found", documentLibraryShortName);
                    await orchestrationGrain.OnIngestionFailedAsync($"Document library {documentLibraryShortName} not found");
                    return;
                }
                
                // Process the document with Kernel Memory
                await documentLibraryRepository.StoreContentAsync(
                    documentLibraryShortName,
                    documentLibrary.IndexName,
                    fileStream,
                    fileName,
                    documentUrl,
                    uploadedByUserOid);
            }
            
            _logger.LogInformation(
                "Successfully processed document {FileName} for {DocumentLibraryType} {DocumentLibraryName}",
                fileName, documentLibraryType, documentLibraryShortName);
                
            await orchestrationGrain.OnIngestionCompletedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to process document {FileName} for {DocumentLibraryType} {DocumentLibraryName}",
                fileName, documentLibraryType, documentLibraryShortName);
                
            await orchestrationGrain.OnIngestionFailedAsync(
                $"Failed to process document {fileName}: {ex.Message}");
            throw;
        }
    }
    
    private async Task<Stream> GetDocumentStreamAsync(string documentUrl)
    {
        try
        {
            // Create helper to get file stream
            using var scope = _serviceProvider.CreateScope();
            var azureFileHelper = scope.ServiceProvider.GetRequiredService<AzureFileHelper>();
            
            // Get the file stream
            return await azureFileHelper.GetFileAsStreamFromFullBlobUrlAsync(documentUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document stream for {DocumentUrl}", documentUrl);
            return null;
        }
    }
}