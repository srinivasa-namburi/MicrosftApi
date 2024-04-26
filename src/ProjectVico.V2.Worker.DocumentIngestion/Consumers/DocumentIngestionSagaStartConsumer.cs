using HandlebarsDotNet;
using MassTransit;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;

namespace ProjectVico.V2.Worker.DocumentIngestion.Consumers;

public class DocumentIngestionSagaStartConsumer : IConsumer<DocumentIngestionRequest>
{
    private readonly ILogger<DocumentIngestionSagaStartConsumer> _logger;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public DocumentIngestionSagaStartConsumer(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        ILogger<DocumentIngestionSagaStartConsumer> logger)
    {
        _logger = logger;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
    }

    public async Task Consume(ConsumeContext<DocumentIngestionRequest> context)
    {

        var documentProcess = _serviceConfigurationOptions
            .ProjectVicoServices
            .DocumentProcesses
            .FirstOrDefault(x => x.Name == context.Message.DocumentProcessName);

        if (documentProcess == null)
        {
            _logger.LogError("DocumentIngestionSagaStartConsumer : Document Process {DocumentProcessName} not found in configuration, aborting ingestion processing for Document ID {CorrelationID}", context.Message.DocumentProcessName, context.Message.Id);
            return;
        }

        if (documentProcess.IngestionMethod == "KernelMemory")
        {
            _logger.LogInformation("DocumentIngestionSagaStartConsumer : Received KernelMemory message with ID : {CorrelationID} for Document Process {DocumentProcessName}", context.Message.Id, context.Message.DocumentProcessName);
            await context.Publish(new KernelMemoryDocumentIngestionRequest(context.Message.Id)
            {
                DocumentProcessName = context.Message.DocumentProcessName,
                FileName = context.Message.FileName,
                OriginalDocumentUrl = context.Message.OriginalDocumentUrl,
                UploadedByUserOid = context.Message.UploadedByUserOid,
                Plugin = context.Message.Plugin
            });
        }
        else
        {
            _logger.LogInformation("DocumentIngestionSagaStartConsumer : Received Classic message with ID : {CorrelationID} for Document Process {DocumentProcessName}", context.Message.Id, context.Message.DocumentProcessName);
            await context.Publish(new ClassicDocumentIngestionRequest(context.Message.Id)
            {
                DocumentProcessName = context.Message.DocumentProcessName,
                FileName = context.Message.FileName,
                OriginalDocumentUrl = context.Message.OriginalDocumentUrl,
                UploadedByUserOid = context.Message.UploadedByUserOid,
                Plugin = context.Message.Plugin
            });
        }
    }
}