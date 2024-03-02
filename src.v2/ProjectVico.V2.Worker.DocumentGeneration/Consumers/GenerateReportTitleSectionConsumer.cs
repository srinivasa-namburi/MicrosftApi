using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Models.Enums;
using ProjectVico.V2.Worker.DocumentGeneration.Services;

namespace ProjectVico.V2.Worker.DocumentGeneration.Consumers;

public class GenerateReportTitleSectionConsumer : IConsumer<GenerateReportTitleSection>
{
    private readonly Kernel _kernel;
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
        _kernel = kernel;
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

                var bodyContentNodes = await GenerateBodyText(contentNodeType, sectionNumber, sectionTitle);

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

    private async Task<List<ContentNode>> GenerateBodyText(string contentNodeType, string sectionNumber, string sectionTitle)
    {
        var result = await _bodyTextGenerator.GenerateBodyText(contentNodeType, sectionNumber, sectionTitle);
        return result;
    }
}