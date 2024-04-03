using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Worker.DocumentGeneration.Consumers;

public class GenerateReportTitleSectionConsumer : IConsumer<GenerateReportTitleSection>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<GenerateReportTitleSectionConsumer> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IBodyTextGenerator _bodyTextGenerator;

    public GenerateReportTitleSectionConsumer(
        Kernel kernel,
        DocGenerationDbContext dbContext,
        ILogger<GenerateReportTitleSectionConsumer> logger,
        IPublishEndpoint publishEndpoint,
        IBodyTextGenerator bodyTextGenerator)
    {
        _dbContext = dbContext;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
        _bodyTextGenerator = bodyTextGenerator;
    }

    public async Task Consume(ConsumeContext<GenerateReportTitleSection> context)
    {
        var message = context.Message;
        var contentNodeGeneratedEvent = new ContentNodeGenerated(message.CorrelationId)
        {
            AuthorOid = message.AuthorOid
        };

        var tableOfContentsString = GeneratePlainTextTableOfContentsFromOutlineJson(message.DocumentOutlineJson);

        try
        {
            var trackedDocument =
                await _dbContext.GeneratedDocuments
                .FirstOrDefaultAsync(x => x.Id == message.CorrelationId);

            if (trackedDocument == null)
            {
                // This is an orphaned generation request - ignored.
                _logger.LogInformation("Encountered orphaned GenerateReportTitleSection - probably in development with restarting db instance?. Abandoned.");
                return;
            }
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(message.ContentNodeJson));
            var contentNode = await JsonSerializer.DeserializeAsync<ContentNode>(stream);

            // Find the content node with the same id as the one we are generating content for
            // var existingContentNode = trackedDocument.ContentNodes.FirstOrDefault(cn => cn.Id == contentNode.Id);
            var existingContentNode = await _dbContext.ContentNodes
                .Include(x => x.Children)
                .FirstOrDefaultAsync(cn => cn.Id == contentNode.Id)!;

            if (existingContentNode != null)
            {
                existingContentNode.GenerationState = ContentNodeGenerationState.InProgress;
                await _dbContext.SaveChangesAsync();

                await _publishEndpoint.Publish<ContentNodeGenerationStateChanged>(
                    new ContentNodeGenerationStateChanged(message.CorrelationId)
                    {
                        ContentNodeId = existingContentNode.Id,
                        GenerationState = ContentNodeGenerationState.InProgress
                    });

                string contentNodeType;

                if (contentNode.Type == ContentNodeType.Title)
                {
                    contentNodeType = "Title";
                }
                else
                {
                    contentNodeType = "Heading";
                }

                var sectionNumber = contentNode.Text.Split(' ')[0];
                var sectionTitle = contentNode.Text.Substring(sectionNumber.Length).Trim();

                var bodyContentNodes = await GenerateBodyText(contentNodeType, sectionNumber, sectionTitle, tableOfContentsString, message.MetadataId);

                // Set the Parent of all bodyContentNodes to be the existingContentNode
                foreach (var bodyContentNode in bodyContentNodes)
                {
                    bodyContentNode.ParentId = existingContentNode?.Id;
                    _dbContext.ContentNodes.Add(bodyContentNode);
                }

                if (bodyContentNodes.Count == 1)
                {
                    contentNodeGeneratedEvent.ContentNodeId = bodyContentNodes[0].Id;
                }


            }

            existingContentNode.GenerationState = ContentNodeGenerationState.Completed;
            await _dbContext.SaveChangesAsync();

            await _publishEndpoint.Publish<ContentNodeGenerationStateChanged>(
                   new ContentNodeGenerationStateChanged(message.CorrelationId)
                   {
                       ContentNodeId = existingContentNode.Id,
                       GenerationState = ContentNodeGenerationState.Completed
                   });

        }
        catch (Exception e)
        {
            contentNodeGeneratedEvent.IsSuccessful = false;

        }
        finally
        {

            // Publish the ContentNodeGenerated event defined at the top of this file
            await context.Publish(contentNodeGeneratedEvent);
        }
    }

    private async Task<List<ContentNode>> GenerateBodyText(string contentNodeType, string sectionNumber,
        string sectionTitle, string tableOfContentsString, Guid? metadataId = null)
    {
        // This method is a wrapper around the streaming output of the AI Completion Service.
        var result = await _bodyTextGenerator.GenerateBodyText(contentNodeType, sectionNumber, sectionTitle, tableOfContentsString, metadataId);
        return result;
    }

    private string GeneratePlainTextTableOfContentsFromOutlineJson(string outlineJson)
    {
        var contentNodes = JsonSerializer.Deserialize<List<ContentNode>>(outlineJson);
        return GenerateTableOfContents(contentNodes);
    }

    private string GenerateTableOfContents(IEnumerable<ContentNode> contentNodes)
    {
        var sb = new StringBuilder();
        foreach (var contentNode in contentNodes)
        {
            // Recursively append the content nodes to the string builder - separate method call to handle recursive calling
            AppendContentNode(sb, contentNode, 0);
        }
        return sb.ToString();
    }

    private void AppendContentNode(StringBuilder sb, ContentNode contentNode, int depth)
    {
        // Indent based on the depth in the hierarchy
        sb.AppendLine(new string(' ', depth * 2) + contentNode.Text);
        foreach (var child in contentNode.Children)
        {
            AppendContentNode(sb, child, depth + 1);
        }
    }
}