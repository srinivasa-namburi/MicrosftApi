using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

[Reentrant]
public class RepositoryIndexMaintenanceGrain : Grain, IRepositoryIndexMaintenanceGrain
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<RepositoryIndexMaintenanceGrain> _logger;
    private readonly SearchIndexClient _searchIndexClient;
    private readonly AzureFileHelper _fileHelper;
    private const string DummyDocumentContainer = "admin";
    private const string DummyDocumentName = "DummyDocument.pdf";

    public RepositoryIndexMaintenanceGrain(
        IServiceProvider sp,
        ILogger<RepositoryIndexMaintenanceGrain> logger,
        SearchIndexClient searchIndexClient,
        AzureFileHelper fileHelper)
    {
        _sp = sp;
        _logger = logger;
        _searchIndexClient = searchIndexClient;
        _fileHelper = fileHelper;
    }

    public async Task ExecuteAsync()
    {
        var documentProcessInfoService = _sp.GetRequiredService<IDocumentProcessInfoService>();
        var documentLibraryInfoService = _sp.GetRequiredService<IDocumentLibraryInfoService>();
        var documentLibraryRepository = _sp.GetRequiredService<IAdditionalDocumentLibraryKernelMemoryRepository>();

        var indexNames = new HashSet<string>();
        await foreach (var indexName in _searchIndexClient.GetIndexNamesAsync(CancellationToken.None))
        {
            indexNames.Add(indexName);
        }

        var indexNamesList = indexNames.ToList();

        await CreateKernelMemoryIndexes(
            documentProcessInfoService, 
            documentLibraryInfoService,
            documentLibraryRepository, 
            indexNamesList);
    }

    private async Task CreateKernelMemoryIndexes(
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService,
        IAdditionalDocumentLibraryKernelMemoryRepository additionalDocumentLibraryKernelMemoryRepository,
        IReadOnlyList<string> indexNames)
    {
        var documentProcesses = await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
        var kernelMemoryDocumentProcesses = documentProcesses.Where(x => x.LogicType == DocumentProcessLogicType.KernelMemory).ToList();

        if (kernelMemoryDocumentProcesses.Count == 0)
        {
            _logger.LogInformation("No Kernel Memory-based Document Processes found. Skipping index creation.");
            return;
        }

        _logger.LogInformation("Creating Kernel Memory indexes for {Count} Document Processes", kernelMemoryDocumentProcesses.Count);

        // Get dummy document stream from blob storage or upload it if it doesn't exist
        Stream? dummyDocumentStream = await GetOrCreateDummyDocumentAsync();
        if (dummyDocumentStream == null)
        {
            _logger.LogError("Failed to get dummy document. Cannot create indexes.");
            return;
        }

        foreach (var documentProcess in kernelMemoryDocumentProcesses)
        {
            var kernelMemoryRepository = _sp
                .GetServiceForDocumentProcess<IKernelMemoryRepository>(documentProcess.ShortName);

            if (kernelMemoryRepository == null)
            {
                _logger.LogError("No Kernel Memory repository registered for Document Process {DocumentProcess} - skipping", documentProcess.ShortName);
                continue;
            }

            foreach (var repository in documentProcess.Repositories)
            {
                if (indexNames.Contains(repository))
                {
                    _logger.LogInformation("Index {IndexName} already exists for Document Process {DocumentProcess}. Skipping creation.", repository, documentProcess.ShortName);
                    continue;
                }

                var currentTimeUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var dummyDocumentCreatedFileName = $"DummyDocument-{currentTimeUnixTime}.pdf";
                _logger.LogInformation("Creating index for Document Process {DocumentProcess} and Repository {Repository}", documentProcess.ShortName, repository);
                
                // Reset stream position
                dummyDocumentStream.Position = 0;
                
                await kernelMemoryRepository.StoreContentAsync(documentProcess.ShortName, repository, dummyDocumentStream, dummyDocumentCreatedFileName, null);
                
                _logger.LogInformation("Index created for Document Process {DocumentProcess} and Repository {Repository}", documentProcess.ShortName, repository);
                await kernelMemoryRepository.DeleteContentAsync(documentProcess.ShortName, repository, dummyDocumentCreatedFileName);
            }
        }

        _logger.LogInformation("Kernel Memory indexes created for {Count} Document Processes", kernelMemoryDocumentProcesses.Count);

        var documentLibraries = await documentLibraryInfoService.GetAllDocumentLibrariesAsync();

        if (documentLibraries.Count == 0)
        {
            _logger.LogInformation("No Document Libraries found. Skipping index creation.");
            dummyDocumentStream.Dispose();
            return;
        }

        _logger.LogInformation("Updating or creating indexes for {Count} Document Libraries", documentLibraries.Count);

        foreach (var documentLibrary in documentLibraries)
        {
            if (indexNames.Contains(documentLibrary.IndexName))
            {
                _logger.LogInformation("Index {IndexName} already exists for Document Library {DocumentLibrary}. Skipping creation.", documentLibrary.IndexName, documentLibrary.ShortName);
                continue;
            }

            _logger.LogInformation("Creating index for Document Library {DocumentLibrary}", documentLibrary.ShortName);
            
            // Reset stream position
            dummyDocumentStream.Position = 0;
            
            await additionalDocumentLibraryKernelMemoryRepository.StoreContentAsync(
                documentLibrary.ShortName, 
                documentLibrary.IndexName, 
                dummyDocumentStream, 
                "DummyDocument.pdf", 
                null);
            
            _logger.LogInformation("Index created for Document Library {DocumentLibrary}", documentLibrary.ShortName);
            await additionalDocumentLibraryKernelMemoryRepository.DeleteContentAsync(
                documentLibrary.ShortName, 
                documentLibrary.IndexName, 
                "DummyDocument.pdf");
        }

        dummyDocumentStream.Dispose();
        _logger.LogInformation("Indexes updated or created for {Count} Document Libraries", documentLibraries.Count);
    }

    private async Task<Stream?> GetOrCreateDummyDocumentAsync()
    {
        try
        {
            // First try to get the document from blob storage
            try
            {
                var stream = await _fileHelper.GetFileAsStreamFromContainerAndBlobName(
                    DummyDocumentContainer, 
                    DummyDocumentName);
                
                if (stream != null)
                {
                    _logger.LogInformation("Found DummyDocument.pdf in admin container");
                    return stream;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve DummyDocument.pdf from blob storage. Will attempt to create it.");
            }

            // If we get here, we need to create and upload a new dummy document
            // Create a simple PDF with minimal content
            using (var memoryStream = new MemoryStream())
            {
                await using (var writer = new StreamWriter(memoryStream, leaveOpen: true))
                {
                    await writer.WriteLineAsync("%PDF-1.4");
                    await writer.WriteLineAsync("1 0 obj");
                    await writer.WriteLineAsync("<< /Type /Catalog /Pages 2 0 R >>");
                    await writer.WriteLineAsync("endobj");
                    await writer.WriteLineAsync("2 0 obj");
                    await writer.WriteLineAsync("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
                    await writer.WriteLineAsync("endobj");
                    await writer.WriteLineAsync("3 0 obj");
                    await writer.WriteLineAsync("<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
                    await writer.WriteLineAsync("endobj");
                    await writer.WriteLineAsync("4 0 obj");
                    await writer.WriteLineAsync("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
                    await writer.WriteLineAsync("endobj");
                    await writer.WriteLineAsync("5 0 obj");
                    await writer.WriteLineAsync("<< /Length 44 >>");
                    await writer.WriteLineAsync("stream");
                    await writer.WriteLineAsync("BT /F1 12 Tf 72 712 Td (Dummy Document) Tj ET");
                    await writer.WriteLineAsync("endstream");
                    await writer.WriteLineAsync("endobj");
                    await writer.WriteLineAsync("xref");
                    await writer.WriteLineAsync("0 6");
                    await writer.WriteLineAsync("0000000000 65535 f");
                    await writer.WriteLineAsync("0000000009 00000 n");
                    await writer.WriteLineAsync("0000000063 00000 n");
                    await writer.WriteLineAsync("0000000122 00000 n");
                    await writer.WriteLineAsync("0000000228 00000 n");
                    await writer.WriteLineAsync("0000000296 00000 n");
                    await writer.WriteLineAsync("trailer");
                    await writer.WriteLineAsync("<< /Size 6 /Root 1 0 R >>");
                    await writer.WriteLineAsync("startxref");
                    await writer.WriteLineAsync("385");
                    await writer.WriteLineAsync("%%EOF");
                }

                // Reset the position to the beginning
                memoryStream.Position = 0;

                // Upload to blob storage
                try
                {
                    _logger.LogInformation("Uploading new DummyDocument.pdf to admin container");
                    await _fileHelper.UploadFileToBlobAsync(
                        memoryStream, 
                        DummyDocumentName, 
                        DummyDocumentContainer, 
                        true);
                    
                    // Get a fresh stream for the newly uploaded document
                    var uploadedStream = await _fileHelper.GetFileAsStreamFromContainerAndBlobName(
                        DummyDocumentContainer, 
                        DummyDocumentName);
                    
                    if (uploadedStream != null)
                    {
                        _logger.LogInformation("Successfully created and uploaded DummyDocument.pdf");
                        return uploadedStream;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading DummyDocument.pdf to blob storage");
                    
                    // Return the in-memory stream as a fallback
                    memoryStream.Position = 0;
                    
                    // Create a new memory stream with the content to avoid disposal issues
                    var fallbackStream = new MemoryStream();
                    await memoryStream.CopyToAsync(fallbackStream);
                    fallbackStream.Position = 0;
                    
                    _logger.LogWarning("Using in-memory fallback for DummyDocument.pdf");
                    return fallbackStream;
                }
            }

            _logger.LogError("Failed to create or retrieve DummyDocument.pdf");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrCreateDummyDocumentAsync");
            return null;
        }
    }
}