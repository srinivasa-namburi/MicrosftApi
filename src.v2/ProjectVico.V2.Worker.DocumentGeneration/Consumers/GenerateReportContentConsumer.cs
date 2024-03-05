using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Worker.DocumentGeneration.Consumers;

public class GenerateReportContentConsumer : IConsumer<GenerateReportContent>
{
    private readonly Kernel _sk;
    private readonly ILogger<GenerateReportContentConsumer> _logger;
    private int _titleCount;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public GenerateReportContentConsumer(Kernel sk, 
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        ILogger<GenerateReportContentConsumer> logger)
    {
        _sk = sk;
        _logger = logger;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
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
            DocumentOutlineJson = documentOutlineJson
        });

        // Recursively process each child node
        foreach (var child in node.Children)
        {
            await ProcessContentNodeRecursive(child, documentOutlineJson, context);
        }
    }
}