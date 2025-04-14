using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers.KernelMemoryDocumentIngestionSaga;

/// <summary>
/// Consumer for handling the creation of ingested documents in kernel memory.
/// </summary>
public class KernelMemoryCreateIngestedDocumentConsumer : IConsumer<KernelMemoryCreateIngestedDocument>
{
    private IKernelMemoryRepository? _kernelMemoryRepository;
    private IAdditionalDocumentLibraryKernelMemoryRepository? _documentLibraryKmRepository;
    private readonly ILogger<KernelMemoryCreateIngestedDocumentConsumer> _logger;
    private readonly AzureFileHelper _azureFileHelper;
    private readonly IServiceProvider _sp;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelMemoryCreateIngestedDocumentConsumer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="azureFileHelper">The Azure file helper instance.</param>
    /// <param name="sp">The service provider instance.</param>
    /// <param name="documentProcessInfoService">The document process info service instance.</param>
    /// <param name="documentLibraryInfoService">The document library info service instance.</param>
    public KernelMemoryCreateIngestedDocumentConsumer(
        ILogger<KernelMemoryCreateIngestedDocumentConsumer> logger,
        AzureFileHelper azureFileHelper,
        IServiceProvider sp,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService
        )
    {
        _logger = logger;
        _azureFileHelper = azureFileHelper;
        _sp = sp;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
    }

    /// <summary>
    /// Consumes the specified context.
    /// </summary>
    /// <param name="context">The consume context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Consume(ConsumeContext<KernelMemoryCreateIngestedDocument> context)
    {
        if (context.Message.DocumentLibraryShortName == null)
        {
            _logger.LogError("KernelMemoryCreateIngestedDocumentConsumer : Encountered message with null document library short name - aborting ingestion");
            await context.Publish(new KernelMemoryDocumentIngestionFailed(context.Message.CorrelationId));
            return;
        }

        // Run a different process based on the document library type
        if (context.Message.DocumentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary)
        {
            await ProcessPrimaryDocumentProcessIngestion(context);
        }
        else
        {
            await ProcessAdditionalDocumentLibraryIngestion(context);
        }
    }

    /// <summary>
    /// Processes the ingestion for additional document libraries.
    /// </summary>
    /// <param name="context">The consume context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ProcessAdditionalDocumentLibraryIngestion(ConsumeContext<KernelMemoryCreateIngestedDocument> context)
    {
        var message = context.Message;
        _logger.LogInformation("KernelMemoryCreateIngestedDocumentConsumer : Received message with ID : {CorrelationID} for Additional Document Library {DocumentLibraryName}", message.CorrelationId, message.DocumentLibraryShortName);

        var documentLibraryInfo = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(message.DocumentLibraryShortName!);

        if (documentLibraryInfo == null)
        {
            _logger.LogError("KernelMemoryCreateIngestedDocumentConsumer : Additional Document Library {DocumentLibraryName} not found in configuration, aborting ingestion processing for Document ID {CorrelationID}", message.DocumentLibraryShortName, message.CorrelationId);
            await context.Publish(new KernelMemoryDocumentIngestionFailed(message.CorrelationId));
            return;
        }

        // Use the standard document library kernel memory repository
        _documentLibraryKmRepository = _sp.GetService<IAdditionalDocumentLibraryKernelMemoryRepository>();

        if (_documentLibraryKmRepository == null)
        {
            _logger.LogError("KernelMemoryCreateIngestedDocumentConsumer : Unable to retrieve the Additional Document Library KM Repository for Document Library {DocumentLibraryName}", message.DocumentLibraryShortName);
            await context.Publish(new KernelMemoryDocumentIngestionFailed(message.CorrelationId));
            return;
        }

        var fileStream = await _azureFileHelper.GetFileAsStreamFromFullBlobUrlAsync(message.OriginalDocumentUrl);
        if (fileStream == null)
        {
            _logger.LogError("KernelMemoryCreateIngestedDocumentConsumer : Unable to retrieve file stream for Document ID {CorrelationID}", message.CorrelationId);
            await context.Publish(new KernelMemoryDocumentIngestionFailed(message.CorrelationId));
            return;
        }

        await _documentLibraryKmRepository.StoreContentAsync(
            message.DocumentLibraryShortName!,
            documentLibraryInfo.IndexName,
            fileStream,
            message.FileName,
            message.OriginalDocumentUrl,
            message.UploadedByUserOid);

        await context.Publish(new KernelMemoryDocumentCreated(message.CorrelationId));
    }

    /// <summary>
    /// Processes the ingestion for primary document processes.
    /// </summary>
    /// <param name="context">The consume context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ProcessPrimaryDocumentProcessIngestion(ConsumeContext<KernelMemoryCreateIngestedDocument> context)
    {
        var message = context.Message;
        _logger.LogInformation("KernelMemoryCreateIngestedDocumentConsumer : Received message with ID : {CorrelationID} for Document Process {DocumentProcessName}", message.CorrelationId, message.DocumentLibraryShortName);

        var documentProcessInfo =
            await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(message.DocumentLibraryShortName!);

        if (documentProcessInfo == null)
        {
            _logger.LogError("KernelMemoryCreateIngestedDocumentConsumer : Document Process {DocumentProcessName} not found in configuration, aborting ingestion processing for Document ID {CorrelationID}", message.DocumentLibraryShortName, message.CorrelationId);
            await context.Publish(new KernelMemoryDocumentIngestionFailed(message.CorrelationId));
            return;
        }

        _kernelMemoryRepository = _sp.GetServiceForDocumentProcess<IKernelMemoryRepository>(message.DocumentLibraryShortName!);
        if (_kernelMemoryRepository == null)
        {
            _logger.LogError("KernelMemoryCreateIngestedDocumentConsumer : Unable to retrieve Kernel Memory repository for Document Process {DocumentProcessName}, aborting ingestion processing for Document ID {CorrelationID}", message.DocumentLibraryShortName, message.CorrelationId);
            await context.Publish(new KernelMemoryDocumentIngestionFailed(message.CorrelationId));
            return;
        }

        var fileStream = await _azureFileHelper.GetFileAsStreamFromFullBlobUrlAsync(message.OriginalDocumentUrl);
        if (fileStream == null)
        {
            _logger.LogError("KernelMemoryCreateIngestedDocumentConsumer : Unable to retrieve file stream for Document ID {CorrelationID}", message.CorrelationId);
            await context.Publish(new KernelMemoryDocumentIngestionFailed(message.CorrelationId));
            return;
        }

        await _kernelMemoryRepository.StoreContentAsync(
            message.DocumentLibraryShortName!,
            documentProcessInfo.Repositories[0],
            fileStream,
            message.FileName,
            message.OriginalDocumentUrl,
            message.UploadedByUserOid);

        await context.Publish(new KernelMemoryDocumentCreated(message.CorrelationId));
    }
}
