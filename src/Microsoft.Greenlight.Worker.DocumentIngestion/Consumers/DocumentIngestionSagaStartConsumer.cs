using MassTransit;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers;

public class DocumentIngestionSagaStartConsumer : IConsumer<DocumentIngestionRequest>
{
    private readonly ILogger<DocumentIngestionSagaStartConsumer> _logger;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;

    public DocumentIngestionSagaStartConsumer(ILogger<DocumentIngestionSagaStartConsumer> logger,
        IDocumentProcessInfoService documentProcessInfoService)
    {
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
    }

    public async Task Consume(ConsumeContext<DocumentIngestionRequest> context)
    {
        var documentProcess =
            await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(context.Message.DocumentProcessName);

        if (documentProcess == null)
        {
            _logger.LogError("DocumentIngestionSagaStartConsumer : Document Process {DocumentProcessName} not found in configuration, aborting ingestion processing for Document ID {CorrelationID}", context.Message.DocumentProcessName, context.Message.Id);
            return;
        }

        if (documentProcess.LogicType == DocumentProcessLogicType.KernelMemory)
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
