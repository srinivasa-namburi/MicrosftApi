using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers;

/// <summary>
/// A consumer class for the <see cref="GenerateReportContent"/> message.
/// </summary>
public class GenerateReportContentConsumer : IConsumer<GenerateReportContent>
{
    private readonly ILogger<GenerateReportContentConsumer> _logger;
    private readonly DocGenerationDbContext _dbContext;
    private int _titleCount;

    /// <summary>
    /// Initializes a new instance of the GenerateReportContentConsumer class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> instance for this class.</param>
    /// <param name="dbContext">The <see cref="DocGenerationDbContext"/> database context.</param>
    public GenerateReportContentConsumer(
        ILogger<GenerateReportContentConsumer> logger,
        DocGenerationDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Consumes the <see cref="GenerateReportContent"/> context.
    /// </summary>
    /// <param name="context">The <see cref="GenerateReportContent"/> context.</param>
    /// <returns>The long running consuming <see cref="Task"/>.</returns>
    public async Task Consume(ConsumeContext<GenerateReportContent> context)
    {
        var message = context.Message;
        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(message.GeneratedDocumentJson));
        var reportContent = await JsonSerializer.DeserializeAsync<GeneratedDocument>(memoryStream);

        var generationMessages = new List<GenerateReportTitleSection>();
        var noContentNodes = new List<ContentNode>();

        _titleCount = 0;

        // Throws a null reference exception if reportContent is null.
        // This will never be null because the GeneratedDocumentJson will always be present and successfullly
        // deserialized at this point.
        var documentContentNodesJson = JsonSerializer.Serialize(reportContent!.ContentNodes);

        // Process each top-level content node recursively
        foreach (var title in reportContent.ContentNodes)
        {
            await ProcessContentNodeRecursive(
                title, documentContentNodesJson, context, noContentNodes, generationMessages);
        }

        await context.Publish(new ReportContentGenerationSubmitted(context.Message.CorrelationId)
        {
            NumberOfContentNodesToGenerate = _titleCount,
            AuthorOid = message.AuthorOid!
        });

        // Save changes to the database - this is nodes that have RenderTitleOnly set to true
        await _dbContext.SaveChangesAsync();

        //Publish finished message for all sections that have RenderTitleOnly set to true
        foreach (var node in noContentNodes)
        {
            await context.Publish<ContentNodeGenerated>(
                new ContentNodeGenerated(message.CorrelationId)
                {
                    ContentNodeId = node.Id,
                    IsSuccessful = true,
                    AuthorOid = message.AuthorOid!
                });

            await context.Publish<ContentNodeGenerationStateChanged>(
                new ContentNodeGenerationStateChanged(message.CorrelationId)
                {
                    ContentNodeId = node.Id,
                    GenerationState = ContentNodeGenerationState.Completed
                });
        }

        // Publish generation messages for all sections that have body content
        foreach (var generationMessage in generationMessages)
        {
            await context.Publish(generationMessage);
        }
    }

    private async Task ProcessContentNodeRecursive(ContentNode node, string documentOutlineJson,
        ConsumeContext<GenerateReportContent> context, List<ContentNode> noContentNodes,
        List<GenerateReportTitleSection> generationMessages)
    {
        // Increment count for each node with body text
        _titleCount++;

        // Don't generate content for ContentNodes that have the ReportTitleOnly property set to true
        // These are sections that have no body content and are only used to generate headings
        if (node.RenderTitleOnly)
        {
            var dbNode = await _dbContext.ContentNodes.FindAsync(node.Id);
            if (dbNode != null)
            {
                dbNode.GenerationState = ContentNodeGenerationState.Completed;
                noContentNodes.Add(dbNode);
            }
        }
        else
        {
            var contentNodeJson = JsonSerializer.Serialize(node);
            var generationMessage = new GenerateReportTitleSection(context.Message.CorrelationId)
            {
                ContentNodeJson = contentNodeJson,
                AuthorOid = context.Message.AuthorOid!,
                DocumentOutlineJson = documentOutlineJson,
                MetadataId = context.Message.MetadataId
            };

            generationMessages.Add(generationMessage);
        }

        // Recursively process each child node
        foreach (var child in node.Children)
        {
            await ProcessContentNodeRecursive(child, documentOutlineJson, context, noContentNodes, generationMessages);
        }
    }
}
