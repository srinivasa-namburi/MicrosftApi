using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Greenlight.DocumentProcess.Dynamic.Generation
{
    public class DynamicDocumentOutlineService : IDocumentOutlineService
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;

        public DynamicDocumentOutlineService(
            DocGenerationDbContext dbContext,
            IDocumentProcessInfoService documentProcessInfoService
        )
        {
            _dbContext = dbContext;
            _documentProcessInfoService = documentProcessInfoService;
        }

        public async Task<List<ContentNode>> GenerateDocumentOutlineForDocument(GeneratedDocument generatedDocument)
        {
            var documentProcess = await _dbContext.DynamicDocumentProcessDefinitions
                .Where(x => x.ShortName == generatedDocument.DocumentProcess)
                .Include(o => o.DocumentOutline)
                    .ThenInclude(o => o.OutlineItems)
                        .ThenInclude(o => o.Children)
                            .ThenInclude(o => o.Children)
                                .ThenInclude(o => o.Children)
                                    .ThenInclude(o => o.Children)
                                        .ThenInclude(o => o.Children)
                                            .ThenInclude(o => o.Children)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync();

            if (documentProcess?.DocumentOutline?.OutlineItems == null)
            {
                return new List<ContentNode>();
            }

            // Sort top-level items by Level and OrderIndex
            var topLevelItems = documentProcess.DocumentOutline.OutlineItems
                .OrderBy(i => i.Level)
                .ThenBy(i => i.OrderIndex)
                .ToList();

            var topLevelNodes = new List<ContentNode>();

            // Build hierarchy for each top-level item
            foreach (var topOutlineItem in topLevelItems)
            {
                var topNode = BuildContentNodeHierarchy(topOutlineItem, null);
                topLevelNodes.Add(topNode);
            }

            // Add only the top-level nodes; EF will recursively add children
            _dbContext.ContentNodes.AddRange(topLevelNodes);
            await _dbContext.SaveChangesAsync();

            // Attach and update the GeneratedDocument with the hierarchical nodes
            _dbContext.Attach(generatedDocument);
            generatedDocument.ContentNodes = topLevelNodes;
            foreach (var topNode in generatedDocument.ContentNodes)
            {
               topNode.GeneratedDocumentId = generatedDocument.Id;
            }

            await _dbContext.SaveChangesAsync();

            return topLevelNodes;
        }

        private ContentNode BuildContentNodeHierarchy(DocumentOutlineItem outlineItem, ContentNode? parentNode)
        {
            var nodeType = outlineItem.Level == 0 ? ContentNodeType.Title : ContentNodeType.Heading;

            var textValue = string.IsNullOrWhiteSpace(outlineItem.SectionNumber)
                ? outlineItem.SectionTitle
                : $"{outlineItem.SectionNumber} {outlineItem.SectionTitle}";

            var currentNode = new ContentNode
            {
                Id = Guid.NewGuid(),
                ParentId = parentNode?.Id,
                Text = textValue,
                Type = nodeType,
                GenerationState = ContentNodeGenerationState.OutlineOnly,
                Children = new List<ContentNode>(),
                PromptInstructions = outlineItem.PromptInstructions,
                RenderTitleOnly = outlineItem.RenderTitleOnly
            };

            parentNode?.Children.Add(currentNode);

            var children = outlineItem.Children
                .OrderBy(child => child.Level)
                .ThenBy(child => child.OrderIndex)
                .ToList();

            foreach (var childItem in children)
            {
                BuildContentNodeHierarchy(childItem, currentNode);
            }

            return currentNode;
        }
    }
}
