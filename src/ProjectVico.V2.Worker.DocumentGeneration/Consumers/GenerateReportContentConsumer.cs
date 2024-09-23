using System.Text;
using System.Text.Json;
using MassTransit;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Worker.DocumentGeneration.Consumers;

public class GenerateReportContentConsumer : IConsumer<GenerateReportContent>
{
    private readonly ILogger<GenerateReportContentConsumer> _logger;
    private int _titleCount;

    public GenerateReportContentConsumer(
        ILogger<GenerateReportContentConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GenerateReportContent> context)
    {
        var message = context.Message;
        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(message.GeneratedDocumentJson));
        var reportContent = await JsonSerializer.DeserializeAsync<GeneratedDocument>(memoryStream);
        
        _titleCount = 0;
        var documentContentNodesJson = JsonSerializer.Serialize(reportContent.ContentNodes);

        // Process each top-level content node recursively
        foreach (var title in reportContent.ContentNodes)
        {
            await ProcessContentNodeRecursive(title, documentContentNodesJson, context);
        }

        await context.Publish(new ReportContentGenerationSubmitted(context.Message.CorrelationId)
        {
            NumberOfContentNodesToGenerate = _titleCount,
            AuthorOid = message.AuthorOid!
        });
    }

    private async Task ProcessContentNodeRecursive(ContentNode node, string documentOutlineJson, ConsumeContext<GenerateReportContent> context)
    {
        _titleCount++; // Increment count for each node processed

        var contentNodeJson = JsonSerializer.Serialize(node);

        await context.Publish(new GenerateReportTitleSection(context.Message.CorrelationId)
        {
            ContentNodeJson = contentNodeJson,
            AuthorOid = context.Message.AuthorOid!,
            DocumentOutlineJson = documentOutlineJson,
            MetadataId = context.Message.MetadataId
        });

        // Recursively process each child node
        foreach (var child in node.Children)
        {
            await ProcessContentNodeRecursive(child, documentOutlineJson, context);
        }
    }
}