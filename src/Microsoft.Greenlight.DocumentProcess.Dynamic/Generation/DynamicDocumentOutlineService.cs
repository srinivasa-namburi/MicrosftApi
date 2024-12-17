using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.DocumentProcess.Dynamic.Generation;

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
                .ThenInclude(p => p.Children)
                    .ThenInclude(q => q.Children)
                        .ThenInclude(r => r.Children)
                            .ThenInclude(s => s.Children)
                                .ThenInclude(t=>t.Children)
                                    .ThenInclude(u => u.Children)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync();

        var orderedSectionListExample = documentProcess.DocumentOutline.FullText;

        var documentOutlineLines = orderedSectionListExample.Split("\n").ToList();
        
        var sectionDictionary = new Dictionary<string, string>();

        // Foreach line in the lines List, remove quotes as well as leading and trailing whitespace
        documentOutlineLines = documentOutlineLines.Select(x => x.Trim([' ', '"', '-'])
                .Replace("[", "")
                .Replace("]", ""))
            .ToList();

        // Remove any empty lines
        documentOutlineLines = documentOutlineLines.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        //TODO: This assumes numbered sections and doesn't handle asterisk or unnumbered sections well
        // Create a dictionary that contains the section number (1, 1.1, 1.1.1, etc) as the key and the rest of the line as the value.
        foreach (var line in documentOutlineLines)
        {
            var sectionNumber = line.Split(' ')[0];
            var sectionTitle = line.Substring(sectionNumber.Length).Trim();
            sectionDictionary.Add(sectionNumber, sectionTitle);
        }

        // Remove any trailing periods from the section numbers
        sectionDictionary = sectionDictionary.ToDictionary(x => x.Key.TrimEnd('.'), x => x.Value);

        // Use the structure of the sections to determine a hierarchy - 1.1 is a child of 1, 1.1.1 is a child of 1.1, etc. Use this to create a tree of ContentNodes.
        // The ContentNodes will have a Text element that should contain the whole title - "1.1.1 Title" for example.
        // The Type should be Title for the top level, and Heading for the rest.
        // The Children should be a list of ContentNodes that are children of the current node.
        var contentNodeList = new List<ContentNode>();
        Dictionary<string, ContentNode> lastNodeAtLevel = new Dictionary<string, ContentNode>();

        foreach (var section in sectionDictionary)
        {
            var levels = section.Key.Split('.');
            var depth = levels.Length;
            var parentNodeKey = string.Join(".", levels.Take(depth - 1)); // Get parent node key by joining all but the last level

            ContentNode parentNode;
            if (depth == 1)
            {
                // This is a top-level node
                parentNode = null; // No parent
            }
            else if (!lastNodeAtLevel.TryGetValue(parentNodeKey, out parentNode))
            {
                // Parent node does not exist, which should not happen if input is correctly structured
                continue; // Or handle error
            }

            var currentNode = new ContentNode
            {
                Id = Guid.NewGuid(),
                Text = $"{section.Key} {section.Value}",
                Type = depth == 1 ? ContentNodeType.Title : ContentNodeType.Heading,
                GenerationState = ContentNodeGenerationState.OutlineOnly,
                Children = new List<ContentNode>()
            };

            var documentOutlineItem = documentProcess.DocumentOutline.OutlineItems.FirstOrDefault(x => x.SectionNumber == section.Key);

            if (documentOutlineItem != null)
            {
                currentNode.RenderTitleOnly = documentOutlineItem.RenderTitleOnly;
                currentNode.PromptInstructions = documentOutlineItem.PromptInstructions;
            }

            if (parentNode != null)
            {
                parentNode.Children.Add(currentNode);
                currentNode.ParentId = parentNode.Id;
            }
            else
            {
                currentNode.ParentId = null;
                contentNodeList.Add(currentNode);
            }

            lastNodeAtLevel[section.Key] = currentNode; // Update the last node at this level
            _dbContext.ContentNodes.Add(currentNode);
        }

        await _dbContext.SaveChangesAsync();

        _dbContext.Attach(generatedDocument);
        generatedDocument.ContentNodes = contentNodeList;

        // Update the generated document in  the database
        await _dbContext.SaveChangesAsync();

        return contentNodeList;
    }
}
