using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using System.Text;
using System.Text.Json;

public class ReportContentGeneratorGrain : Grain, IReportContentGeneratorGrain
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly ILogger<ReportContentGeneratorGrain> _logger;
    private readonly IOptionsMonitor<ServiceConfigurationOptions> _optionsMonitor;
    private readonly IConfiguration _config;
    private SemaphoreSlim _throttleSemaphore;

    public ReportContentGeneratorGrain(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        ILogger<ReportContentGeneratorGrain> logger,
        IOptionsMonitor<ServiceConfigurationOptions> optionsMonitor,
        IConfiguration config)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _config = config;
    }

    public async Task GenerateContentAsync(Guid documentId, string? authorOid, string generatedDocumentJson,
        string documentProcessName, Guid? metadataId)
    {
        var maxParallelActivations = _config.GetValue<int>(
            "ServiceConfiguration:GreenlightServices:Scalability:NumberOfGenerationWorkers");

        if (maxParallelActivations <= 0)
        {
            maxParallelActivations = 4;
        }

        _throttleSemaphore = new SemaphoreSlim(maxParallelActivations, maxParallelActivations);

        var dbContext = await _dbContextFactory.CreateDbContextAsync();

        try
        {
            _logger.LogInformation("Generating content for document {DocumentId}", documentId);

            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(generatedDocumentJson));
            var reportContent = await JsonSerializer.DeserializeAsync<GeneratedDocument>(memoryStream);

            var generationMessages = new List<(ContentNode Node, string OutlineJson)>();
            var noContentNodes = new List<ContentNode>();
            var titleCount = 0;

            // Document outline JSON needs to be serialized for passing to title section generators
            var documentContentNodesJson = JsonSerializer.Serialize(reportContent!.ContentNodes);

            // Process each top-level content node recursively
            foreach (var title in reportContent.ContentNodes)
            {
                // Create a ContentNodeProcessor to track state during recursion
                var processor = new ContentNodeProcessor(dbContext);
                await processor.ProcessContentNodeTreeAsync(
                    title, documentContentNodesJson, documentId, authorOid);

                // Collect results after processing
                titleCount += processor.TitleCount;
                noContentNodes.AddRange(processor.NoContentNodes);
                generationMessages.AddRange(processor.GenerationMessages);
            }

            // Notify the orchestration grain about how many content nodes will be generated
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentGenerationOrchestrationGrain>(documentId);
            await orchestrationGrain.OnReportContentGenerationSubmittedAsync(titleCount);

            // Save changes to the database for nodes that have RenderTitleOnly set to true
            await dbContext.SaveChangesAsync();

            // Send completion notifications for all sections that have RenderTitleOnly set to true
            foreach (var node in noContentNodes)
            {
                await orchestrationGrain.OnContentNodeGeneratedAsync(node.Id, true);
            }
            
            foreach ((ContentNode node, string outlineJson) in generationMessages)
            {
                bool semaphoreAcquired = false;
                try
                {
                    await _throttleSemaphore.WaitAsync(15.Minutes());
                    semaphoreAcquired = true;

                    var sectionGeneratorGrain = GrainFactory.GetGrain<IReportTitleSectionGeneratorGrain>(Guid.NewGuid());
                    var contentNodeJson = JsonSerializer.Serialize(node);

                    // Introduce a half second delay to stagger execution to resolve all dependencies
                    await Task.Delay(500);

                    _ = sectionGeneratorGrain.GenerateSectionAsync(
                            documentId, authorOid, contentNodeJson, outlineJson, metadataId)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                _logger.LogError(t.Exception, "Error generating section for document {DocumentId}", documentId);
                            }
                            if (semaphoreAcquired)
                            {
                                _throttleSemaphore.Release();
                            }
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error waiting for semaphore for document {DocumentId}", documentId);
                    if (semaphoreAcquired)
                    {
                        _throttleSemaphore.Release();
                    }
                    throw;
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating content for document {DocumentId}", documentId);
            throw;
        }
    }

    /// <summary>
    /// Helper class to track state during recursive processing of content nodes
    /// </summary>
    private class ContentNodeProcessor
    {
        private readonly DocGenerationDbContext _dbContext;

        public int TitleCount { get; private set; }
        public List<ContentNode> NoContentNodes { get; } = new();
        public List<(ContentNode Node, string OutlineJson)> GenerationMessages { get; } = new();

        public ContentNodeProcessor(DocGenerationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task ProcessContentNodeTreeAsync(
            ContentNode node, string documentOutlineJson, Guid documentId, string? authorOid)
        {
            // Process this node
            await ProcessSingleNodeAsync(node, documentOutlineJson);

            // Then recursively process all children
            foreach (var child in node.Children)
            {
                await ProcessContentNodeTreeAsync(child, documentOutlineJson, documentId, authorOid);
            }
        }

        private async Task ProcessSingleNodeAsync(ContentNode node, string documentOutlineJson)
        {
            // Increment count for each node with body text
            TitleCount++;

            // Don't generate content for ContentNodes that have the ReportTitleOnly property set to true
            if (node.RenderTitleOnly)
            {
                var dbNode = await _dbContext.ContentNodes.FindAsync(node.Id);
                if (dbNode != null)
                {
                    dbNode.GenerationState = ContentNodeGenerationState.Completed;
                    NoContentNodes.Add(dbNode);
                }
            }
            else
            {
                GenerationMessages.Add((node, documentOutlineJson));
            }
        }
    }
}
