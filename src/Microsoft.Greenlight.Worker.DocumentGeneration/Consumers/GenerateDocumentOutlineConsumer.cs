using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MassTransit;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Extensions;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers;

/// <summary>
/// Consumer class for the <see cref="GenerateDocumentOutline"/> message.
/// </summary>
public class GenerateDocumentOutlineConsumer : IConsumer<GenerateDocumentOutline>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IServiceProvider _sp;
    private readonly ILogger<GenerateDocumentOutlineConsumer> _logger;

    /// <summary>
    /// This member is always initialized in the Consume method.
    /// </summary>
    private IDocumentOutlineService _documentOutlineService = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateDocumentOutlineConsumer"/> class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> instance for this class.</param>
    /// <param name="dbContext">The <see cref="DocGenerationDbContext"/> database context.</param>
    /// <param name="sp">The <see cref="IServiceProvider"/> instance for resolving service dependencies.</param>
    public GenerateDocumentOutlineConsumer(
        ILogger<GenerateDocumentOutlineConsumer> logger,
        DocGenerationDbContext dbContext,
        IServiceProvider sp)
    {
        _dbContext = dbContext;
        _sp = sp;
        _logger = logger;
    }

    /// <summary>
    /// Consumes the <see cref="GenerateDocumentOutline"/> context.
    /// </summary>
    /// <param name="context">The <see cref="GenerateDocumentOutline"/> context.</param>
    /// <returns>The long running consuming <see cref="Task"/>.</returns>
    [Experimental("SKEXP0060")]
    public async Task Consume(ConsumeContext<GenerateDocumentOutline> context)
    {
        var message = context.Message;

        var documentProcessName = message.DocumentProcess;

        _logger.LogInformation("Received GenerateDocumentOutline event: {DocumentID}", message.CorrelationId);

        if (documentProcessName != null)
        {
            _documentOutlineService =
                _sp.GetRequiredServiceForDocumentProcess<IDocumentOutlineService>(documentProcessName);
        }
        else
        {
            _logger.LogWarning(
                "GenerateDocumentOutlineConsumer: Received message for blank document process. Stopping process for Document {DocumentId}",
                message.CorrelationId);
            await context.Publish(new DocumentOutlineGenerationFailed(message.CorrelationId));
            return;
        }

        // Find the document in the database
        var generatedDocument = await _dbContext.GeneratedDocuments.FindAsync(message.CorrelationId);

        await _documentOutlineService.GenerateDocumentOutlineForDocument(generatedDocument!);

        var jsonOutputGeneratedDocument = JsonSerializer.Serialize(generatedDocument);
        // Print the generated document to log output
        _logger.LogInformation("Generated Document: {GeneratedDocument}", jsonOutputGeneratedDocument);

        await context.Publish(new DocumentOutlineGenerated(message.CorrelationId)
        {
            GeneratedDocumentJson = jsonOutputGeneratedDocument,
            AuthorOid = message.AuthorOid
        });
    }
}
