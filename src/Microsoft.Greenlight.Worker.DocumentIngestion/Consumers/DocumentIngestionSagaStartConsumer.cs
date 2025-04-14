using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers;

/// <summary>
/// Consumer for starting the document ingestion saga.
/// </summary>
public class DocumentIngestionSagaStartConsumer : IConsumer<DocumentIngestionRequest>
{
    private readonly ILogger<DocumentIngestionSagaStartConsumer> _logger;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentIngestionSagaStartConsumer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="documentProcessInfoService">The document process info service instance.</param>
    public DocumentIngestionSagaStartConsumer(ILogger<DocumentIngestionSagaStartConsumer> logger,
        IDocumentProcessInfoService documentProcessInfoService)
    {
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
    }

    /// <summary>
    /// Consumes the document ingestion request and starts the appropriate document ingestion process.
    /// </summary>
    /// <param name="context">The consume context containing the document ingestion request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Consume(ConsumeContext<DocumentIngestionRequest> context)
    {
        DocumentProcessLogicType logicType;

        if (context.Message.DocumentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary)
        {
            // For Document Processes, we need to check the Logic Type of the Document Process
            var documentProcess =
                await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(context.Message.DocumentLibraryShortName);

            if (documentProcess == null)
            {
                _logger.LogError("DocumentIngestionSagaStartConsumer : Document Process {DocumentProcessName} not found in configuration, aborting ingestion processing for Document ID {CorrelationID}", context.Message.DocumentLibraryShortName, context.Message.Id);
                return;
            }

            logicType = documentProcess.LogicType;
        }
        else
        {
            // For Document Libraries, we default to Kernel Memory as they always use Kernel Memory.
            logicType = DocumentProcessLogicType.KernelMemory;
        }

        if (logicType == DocumentProcessLogicType.KernelMemory)
        {
            _logger.LogInformation(
                context.Message.DocumentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary
                    ? "DocumentIngestionSagaStartConsumer : Received KernelMemory message with ID : {CorrelationID} for Document Process {DocumentProcessName}"
                    : "DocumentIngestionSagaStartConsumer : Received KernelMemory message with ID : {CorrelationID} for Document Library {DocumentLibraryName}",
                context.Message.Id, context.Message.DocumentLibraryShortName);

            await context.Publish(new KernelMemoryDocumentIngestionRequest(context.Message.Id)
            {
                DocumentLibraryShortName = context.Message.DocumentLibraryShortName,
                DocumentLibraryType = context.Message.DocumentLibraryType,
                FileName = context.Message.FileName,
                OriginalDocumentUrl = context.Message.OriginalDocumentUrl,
                UploadedByUserOid = context.Message.UploadedByUserOid
            });
        }
    }
}
