using MassTransit;
using Microsoft.Extensions.Options;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;
using ProjectVico.V2.Shared.Helpers;

namespace ProjectVico.V2.Worker.DocumentIngestion.Consumers.KernelMemoryDocumentIngestionSaga;

public class KernelMemoryCreateIngestedDocumentConsumer : IConsumer<KernelMemoryCreateIngestedDocument>
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private DocumentProcessOptions? _documentProcessOptions;
    private IKernelMemoryRepository? _kernelMemoryRepository;
    private readonly ILogger<KernelMemoryCreateIngestedDocumentConsumer> _logger;
    private readonly AzureFileHelper _azureFileHelper;
    private readonly IServiceProvider _sp;


    public KernelMemoryCreateIngestedDocumentConsumer(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions, 
        ILogger<KernelMemoryCreateIngestedDocumentConsumer> logger, 
        AzureFileHelper azureFileHelper,
        IServiceProvider sp
        )
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _logger = logger;
        _azureFileHelper = azureFileHelper;
        _sp = sp;
    }
    public async Task Consume(ConsumeContext<KernelMemoryCreateIngestedDocument> context)
    {
        var message = context.Message;
        _logger.LogInformation("KernelMemoryCreateIngestedDocumentConsumer : Received message with ID : {CorrelationID} for Document Process {DocumentProcessName}", message.CorrelationId, message.DocumentProcessName);

        _documentProcessOptions = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses.FirstOrDefault(x => x.Name == message.DocumentProcessName);

        if (_documentProcessOptions == null)
        {
            _logger.LogError("KernelMemoryCreateIngestedDocumentConsumer : Document Process {DocumentProcessName} not found in configuration, aborting ingestion processing for Document ID {CorrelationID}", message.DocumentProcessName, message.CorrelationId);
            await context.Publish(new KernelMemoryDocumentIngestionFailed(message.CorrelationId));
            return;
        }

        _kernelMemoryRepository = _sp.GetKeyedService<IKernelMemoryRepository>(_documentProcessOptions.Name + "-IKernelMemoryRepository");
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
            _documentProcessOptions.Repositories[0],
            fileStream,
            message.FileName,
            message.OriginalDocumentUrl, 
            message.UploadedByUserOid);

        await context.Publish(new KernelMemoryDocumentCreated(message.CorrelationId));

    }
}