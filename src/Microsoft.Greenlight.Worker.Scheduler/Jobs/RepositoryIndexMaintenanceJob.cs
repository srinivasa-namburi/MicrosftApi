using Azure.Search.Documents.Indexes;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Quartz;

namespace Microsoft.Greenlight.Worker.Scheduler.Jobs;

/// <summary>
/// Quartz job that handles maintenance/creation of repository indexes for
/// document processes and document libraries.
/// </summary>
public class RepositoryIndexMaintenanceJob : IJob
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<RepositoryIndexMaintenanceJob> _logger;
    private readonly SearchIndexClient _searchIndexClient;

    /// <summary>
    /// Construct a new instance of the <see cref="RepositoryIndexMaintenanceJob"/> class.
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="logger"></param>
    /// <param name="searchIndexClient"></param>
    public RepositoryIndexMaintenanceJob(IServiceProvider sp,
        ILogger<RepositoryIndexMaintenanceJob> logger,
        SearchIndexClient searchIndexClient)
    {
        _sp = sp;
        _logger = logger;
        _searchIndexClient = searchIndexClient;
    }

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        // Necessary to use scoped services
        var documentProcessInfoService = _sp.GetRequiredService<IDocumentProcessInfoService>();
        var documentLibraryInfoService = _sp.GetRequiredService<IDocumentLibraryInfoService>();
        var documentLibraryRepository = _sp.GetRequiredService<IAdditionalDocumentLibraryKernelMemoryRepository>();

        var indexNames = new HashSet<string>();
        await foreach (var indexName in _searchIndexClient.GetIndexNamesAsync(context.CancellationToken))
        {
            indexNames.Add(indexName);
        }

        var indexNamesList = indexNames.ToList();

        await CreateKernelMemoryIndexes(documentProcessInfoService, documentLibraryInfoService,
            documentLibraryRepository, indexNamesList, context.CancellationToken);
    }

    private async Task CreateKernelMemoryIndexes(
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService,
        IAdditionalDocumentLibraryKernelMemoryRepository additionalDocumentLibraryKernelMemoryRepository,
        IReadOnlyList<string> indexNames,
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
                var fileStream = File.OpenRead("DummyDocument.pdf");
                await kernelMemoryRepository.StoreContentAsync(documentProcess.ShortName, repository, fileStream, dummyDocumentCreatedFileName, null);
                fileStream.Close();

                _logger.LogInformation("Index created for Document Process {DocumentProcess} and Repository {Repository}", documentProcess.ShortName, repository);
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

        foreach (var documentLibrary in documentLibraries)
        {
            if (indexNames.Contains(documentLibrary.IndexName))
            {
                _logger.LogInformation("Index {IndexName} already exists for Document Library {DocumentLibrary}. Skipping creation.", documentLibrary.IndexName, documentLibrary.ShortName);
                continue;
            }

            _logger.LogInformation("Creating index for Document Library {DocumentLibrary}", documentLibrary.ShortName);
            var fileStream = File.OpenRead("DummyDocument.pdf");
            await additionalDocumentLibraryKernelMemoryRepository.StoreContentAsync(documentLibrary.ShortName, documentLibrary.IndexName, fileStream, "DummyDocument.pdf", null);
            fileStream.Close();
            _logger.LogInformation("Index created for Document Library {DocumentLibrary}", documentLibrary.ShortName);
            await additionalDocumentLibraryKernelMemoryRepository.DeleteContentAsync(documentLibrary.ShortName, documentLibrary.IndexName, "DummyDocument.pdf");
        }

        _logger.LogInformation("Indexes updated or created for {Count} Document Libraries", documentLibraries.Count);
    }
}