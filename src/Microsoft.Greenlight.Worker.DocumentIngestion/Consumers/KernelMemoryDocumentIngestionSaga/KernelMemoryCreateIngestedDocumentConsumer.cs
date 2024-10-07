using MassTransit;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers.KernelMemoryDocumentIngestionSaga;

public class KernelMemoryCreateIngestedDocumentConsumer : IConsumer<KernelMemoryCreateIngestedDocument>
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private IKernelMemoryRepository? _kernelMemoryRepository;
    private readonly ILogger<KernelMemoryCreateIngestedDocumentConsumer> _logger;
    private readonly AzureFileHelper _azureFileHelper;
    private readonly IServiceProvider _sp;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;


    public KernelMemoryCreateIngestedDocumentConsumer(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions, 
        ILogger<KernelMemoryCreateIngestedDocumentConsumer> logger, 
        AzureFileHelper azureFileHelper,
        IServiceProvider sp,
        IDocumentProcessInfoService documentProcessInfoService
        )
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _logger = logger;
        _azureFileHelper = azureFileHelper;
        _sp = sp;
        _documentProcessInfoService = documentProcessInfoService;
    }
    public async Task Consume(ConsumeContext<KernelMemoryCreateIngestedDocument> context)
    {
        var message = context.Message;
        _logger.LogInformation("KernelMemoryCreateIngestedDocumentConsumer : Received message with ID : {CorrelationID} for Document Process {DocumentProcessName}", message.CorrelationId, message.DocumentProcessName);

        var documentProcessInfo =
            await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(message.DocumentProcessName);

        if (documentProcessInfo == null)
        {
            _logger.LogError("KernelMemoryCreateIngestedDocumentConsumer : Document Process {DocumentProcessName} not found in configuration, aborting ingestion processing for Document ID {CorrelationID}", message.DocumentProcessName, message.CorrelationId);
            await context.Publish(new KernelMemoryDocumentIngestionFailed(message.CorrelationId));
            return;
        }

        _kernelMemoryRepository = _sp.GetServiceForDocumentProcess<IKernelMemoryRepository>(message.DocumentProcessName);
        if (_kernelMemoryRepository == null)
        {
            _logger.LogError("KernelMemoryCreateIngestedDocumentConsumer : Unable to retrieve Kernel Memory repository for Document Process {DocumentProcessName}, aborting ingestion processing for Document ID {CorrelationID}", message.DocumentProcessName, message.CorrelationId);
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
            message.DocumentProcessName,
            documentProcessInfo.Repositories[0],
            fileStream,
            message.FileName,
            message.OriginalDocumentUrl, 
            message.UploadedByUserOid);

        await context.Publish(new KernelMemoryDocumentCreated(message.CorrelationId));

    }
}
