using Azure;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;

namespace Microsoft.Greenlight.SetupManager.Services;
/// <summary>
/// This service is responsible for setting up the necessary indexes for the 
/// Document Processes and Document Libraries.
/// </summary>
/// <param name="sp">The service provider instance used to resolve dependencies.</param>
/// <param name="logger">The logger instance used for logging information and errors.</param>
/// <param name="searchClientFactory">The factory instance used to create search clients for indexing.</param>
public class SetupServicesInitializerService(
        IServiceProvider sp,
        ILogger<SetupServicesInitializerService> logger,
        SearchClientFactory searchClientFactory) : BackgroundService
{
    private readonly IServiceProvider _sp = sp;
    private readonly ILogger<SetupServicesInitializerService> _logger = logger;

    private readonly SearchClientFactory _searchClientFactory = searchClientFactory;
    public const string ActivitySourceName = "Services";

    /// <summary>
    /// Executes the background service to initialize setup services.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = _sp.CreateScope();

        var documentProcessInfoService = scope.ServiceProvider.GetRequiredService<IDocumentProcessInfoService>();
        var documentLibraryInfoService = scope.ServiceProvider.GetRequiredService<IDocumentLibraryInfoService>();
        var additionalDocumentLibraryKernelMemoryRepository =
                scope.ServiceProvider.GetRequiredService<IAdditionalDocumentLibraryKernelMemoryRepository>();

        await CreateKernelMemoryIndexes(documentProcessInfoService, documentLibraryInfoService,
                additionalDocumentLibraryKernelMemoryRepository, cancellationToken);
    }


    private async Task CreateKernelMemoryIndexes(
            IDocumentProcessInfoService documentProcessInfoService,
            IDocumentLibraryInfoService documentLibraryInfoService,
            IAdditionalDocumentLibraryKernelMemoryRepository additionalDocumentLibraryKernelMemoryRepository,
            CancellationToken cancellationToken)
    {
        var documentProcesses = await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
        var kernelMemoryDocumentProcesses = documentProcesses.Where(x => x.LogicType == DocumentProcessLogicType.KernelMemory).ToList();

        if (kernelMemoryDocumentProcesses.Count == 0)
        {
            _logger.LogInformation("No Kernel Memory-based Document Processes found. Skipping index creation.");
            return;
        }

        _logger.LogInformation("Creating Kernel Memory indexes for {Count} Document Processes", kernelMemoryDocumentProcesses.Count);

        // For each Kernel Memory-based Document Process, create the necessary indexes if they don't already exist
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
                // Check if the index already exists. If it does, skip it.
                var searchIndexClient = _searchClientFactory.GetSearchIndexClientForIndex(repository);
                var indexAlreadyExists = true;

                try
                {
                    var index = await searchIndexClient.GetIndexAsync(repository, cancellationToken);
                }
                catch (RequestFailedException e)
                {
                    // The AI Search API returns a 404 status code if the index does not exist
                    if (e.Status == 404)
                    {
                        indexAlreadyExists = false;
                    }
                }

                if (indexAlreadyExists)
                {
                    _logger.LogInformation("Index {IndexName} already exists for Document Process {DocumentProcess}. Skipping creation.", repository, documentProcess.ShortName);
                    continue;
                }

                var currentTimeUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var dummyDocumentCreatedFileName = $"DummyDocument-{currentTimeUnixTime}.pdf";
                _logger.LogInformation("Creating index for Document Process {DocumentProcess} and Repository {Repository}", documentProcess.ShortName, repository);
                // Create a stream from the file DummyDocument.pdf in the current directory
                var fileStream = File.OpenRead("DummyDocument.pdf");
                // The indexes are created automatically on upload of a document. Use the repository to upload the dummy document
                await kernelMemoryRepository.StoreContentAsync(documentProcess.ShortName, repository, fileStream, dummyDocumentCreatedFileName, null);
                fileStream.Close();

                _logger.LogInformation("Index created for Document Process {DocumentProcess} and Repository {Repository}", documentProcess.ShortName, repository);

                // Delete the dummy document after the index is created
                await kernelMemoryRepository.DeleteContentAsync(documentProcess.ShortName, repository, dummyDocumentCreatedFileName);
            }
        }

        _logger.LogInformation("Kernel Memory indexes created for {Count} Document Processes", kernelMemoryDocumentProcesses.Count);


        var documentLibraries = await documentLibraryInfoService.GetAllDocumentLibrariesAsync();


        if (documentLibraries.Count == 0)
        {
            _logger.LogInformation("No Document Libraries found. Skipping index creation.");
            return;
        }

        _logger.LogInformation("Updating or creating indexes for {Count} Document Libraries", documentLibraries.Count);

        // For each Document Library, create the necessary indexes if they don't already exist
        foreach (var documentLibrary in documentLibraries)
        {
            var searchIndexClient = _searchClientFactory.GetSearchIndexClientForIndex(documentLibrary.IndexName);
            var indexAlreadyExists = true;
            try
            {
                var index = await searchIndexClient.GetIndexAsync(documentLibrary.IndexName, cancellationToken);
            }
            catch (RequestFailedException e)
            {
                // The AI Search API returns a 404 status code if the index does not exist
                if (e.Status == 404)
                {
                    indexAlreadyExists = false;
                }
            }
            if (indexAlreadyExists)
            {
                _logger.LogInformation("Index {IndexName} already exists for Document Library {DocumentLibrary}. Skipping creation.", documentLibrary.IndexName, documentLibrary.ShortName);
                continue;
            }
            _logger.LogInformation("Creating index for Document Library {DocumentLibrary}", documentLibrary.ShortName);
            // Create a stream from the file DummyDocument.pdf in the current directory
            var fileStream = File.OpenRead("DummyDocument.pdf");
            // The indexes are created automatically on upload of a document. Use the repository to upload the dummy document
            await additionalDocumentLibraryKernelMemoryRepository.StoreContentAsync(documentLibrary.ShortName, documentLibrary.IndexName, fileStream, "DummyDocument.pdf", null);
            fileStream.Close();
            _logger.LogInformation("Index created for Document Library {DocumentLibrary}", documentLibrary.ShortName);
            // Delete the dummy document after the index is created
            await additionalDocumentLibraryKernelMemoryRepository.DeleteContentAsync(documentLibrary.ShortName, documentLibrary.IndexName, "DummyDocument.pdf");
        }

        _logger.LogInformation("Indexes updated or created for {Count} Document Libraries", documentLibraries.Count);
    }
}