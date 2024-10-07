using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers;

public class CreateGeneratedDocumentConsumer : IConsumer<CreateGeneratedDocument>
{
    private readonly DocGenerationDbContext _dbContext;
    private ILogger<CreateGeneratedDocumentConsumer> _logger { get; }

    public CreateGeneratedDocumentConsumer(
        ILogger<CreateGeneratedDocumentConsumer> logger,
        DocGenerationDbContext dbContext
        )
    {
        _dbContext = dbContext;
        _logger = logger;
    }
    public async Task Consume(ConsumeContext<CreateGeneratedDocument> context)
    {
        var message = context.Message;

        var existingDocument = await _dbContext.GeneratedDocuments.FindAsync(message.CorrelationId);
        if (existingDocument != null)
        {
            _dbContext.GeneratedDocuments.Remove(existingDocument);
            await _dbContext.SaveChangesAsync();
        }

        var generatedDocument = new GeneratedDocument()
        {
            Id = context.Message.CorrelationId,
            Title = message.OriginalDTO.DocumentTitle,
            GeneratedDate = DateTime.UtcNow,
            RequestingAuthorOid = new Guid(message.OriginalDTO.AuthorOid!),
            DocumentProcess = message.OriginalDTO.DocumentProcessName,
            ContentNodes = new List<ContentNode>()
        };

        var metaData = new DocumentMetadata()
        {
            Id = Guid.NewGuid(),
            GeneratedDocumentId = generatedDocument.Id,
            MetadataJson = message.OriginalDTO.RequestAsJson
        };

        generatedDocument.MetadataId = metaData.Id;

        try
        {
            await _dbContext.GeneratedDocuments.AddAsync(generatedDocument);
            await _dbContext.DocumentMetadata.AddAsync(metaData);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CreateGeneratedDocumentConsumer: Error saving generated document with ID {documentId} to database", context.Message.CorrelationId);
            throw;
        }

        _logger.LogInformation("CreateGeneratedDocumentConsumer: Generated document with ID {documentId} saved to database", context.Message.CorrelationId);

        await context.Publish(new GeneratedDocumentCreated(context.Message.CorrelationId)
        {
            MetaDataId = metaData.Id
        });

    }
}
