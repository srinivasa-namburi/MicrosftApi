using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Greenlight.Shared.Services
{
    /// <inheritdoc />
    public class ContentNodeService : IContentNodeService
    {
        private readonly DocGenerationDbContext _dbContext;

        public ContentNodeService(
            DocGenerationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <inheritdoc />
        public void SortContentNodes(List<ContentNode> nodes)
        {
            // Prioritize BodyText nodes and sort the nodes list itself
            nodes.Sort(CompareContentNodes);

            // Recursively sort the children of each node
            foreach (var node in nodes)
            {
                if (node.Children.Any())
                {
                    SortContentNodes(node.Children);
                }
            }
        }

        /// <inheritdoc />
        public async Task<string?> GetRenderedTextForContentNodeHierarchiesAsync(List<ContentNode> rootContentNodes)
        {
           var fullText = GenerateTextFromContentNodes(rootContentNodes, applySort: true);
           return fullText;
        }

        /// <inheritdoc />
        public async Task<List<ContentNode>?> GetContentNodesHierarchicalAsyncForDocumentId(Guid generatedDocumentId, bool enableTracking = false, bool addParentNodes = false)
        {
            // Create the query - tracking is controlled by the parameter
            var documentsQuery = enableTracking 
                ? _dbContext.GeneratedDocuments 
                : _dbContext.GeneratedDocuments.AsNoTracking();
            
            var document = await documentsQuery
                .FirstOrDefaultAsync(d => d.Id == generatedDocumentId);

            if (document == null)
            {
                return null;
            }

            // Step 1: Load top-level ContentNodes
            var contentNodesQuery = enableTracking
                ? _dbContext.ContentNodes
                : _dbContext.ContentNodes.AsNoTracking();
                
            var topLevelNodes = await contentNodesQuery
                .Include(cn => cn.ContentNodeSystemItem)
                .Where(cn => cn.GeneratedDocumentId == generatedDocumentId)
                .ToListAsync();

            // Step 2: Load all descendants
            var descendantNodes = await GetAllDescendantContentNodesAsync(topLevelNodes.Select(cn => cn.Id).ToList(), enableTracking);

            // Combine all ContentNodes
            var allContentNodes = topLevelNodes.Concat(descendantNodes).ToList();

            // Build the hierarchy
            var contentNodeDict = allContentNodes.ToDictionary(cn => cn.Id);

            // Initialize Children collections
            foreach (var node in allContentNodes)
            {
                node.Children = [];
            }

            // Link parents and children
            foreach (var node in allContentNodes)
            {
                if (node.ParentId.HasValue && contentNodeDict.TryGetValue(node.ParentId.Value, out var parentNode))
                {
                    parentNode.Children.Add(node);
                    if (addParentNodes)
                    {
                        node.Parent = parentNode;
                    }
                }
            }

            // Sort the top-level nodes
            SortContentNodes(topLevelNodes);

            return topLevelNodes;
        }

        /// <summary>
        /// Gets all descendant content nodes for the given parent IDs.
        /// </summary>
        /// <param name="parentIds">The list of parent IDs to get descendants for.</param>
        /// <param name="enableTracking">Whether to enable EF Core change tracking. Default is false.</param>
        /// <returns>The list of all descendant content nodes.</returns>
        private async Task<List<ContentNode>> GetAllDescendantContentNodesAsync(List<Guid> parentIds, bool enableTracking = false)
        {
            var allDescendants = new List<ContentNode>();
            var currentLevelIds = parentIds;

            while (currentLevelIds.Count != 0)
            {
                // Apply tracking settings once at the start of the query
                var contentNodesQuery = enableTracking
                    ? _dbContext.ContentNodes
                    : _dbContext.ContentNodes.AsNoTracking();
                
                var childNodes = await contentNodesQuery
                    .Include(cn => cn.ContentNodeSystemItem)
                    .Where(cn => cn.ParentId.HasValue && currentLevelIds.Contains(cn.ParentId.Value))
                    .ToListAsync();

                if (childNodes.Count == 0)
                {
                    break;
                }

                allDescendants.AddRange(childNodes);

                // Prepare for the next level
                currentLevelIds = childNodes.Select(cn => cn.Id).ToList();
            }

            return allDescendants;
        }

        private int CompareContentNodes(ContentNode x, ContentNode y)
        {
            // Priority to BodyText nodes to bubble them up
            if (x.Type == ContentNodeType.BodyText && y.Type != ContentNodeType.BodyText) return -1;
            if (y.Type == ContentNodeType.BodyText && x.Type != ContentNodeType.BodyText) return 1;

            // Extract and compare hierarchical numbers (e.g., 2.1.1)
            var xParts = Regex.Matches(x.Text, @"\d+").Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();
            var yParts = Regex.Matches(y.Text, @"\d+").Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();

            int minLength = Math.Min(xParts.Length, yParts.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (xParts[i] != yParts[i])
                    return xParts[i].CompareTo(yParts[i]);
            }

            // If one title is a subsection of the other, the shorter (parent) comes first
            if (xParts.Length != yParts.Length)
                return xParts.Length.CompareTo(yParts.Length);

            // If numeric comparison is inconclusive or not applicable, fall back to string comparison
            return string.Compare(x.Text, y.Text, StringComparison.Ordinal);
        }

        /// <summary>
        /// Generate a flat full text from a list of content nodes.
        /// Assumes the nodes are already sorted.
        /// </summary>
        /// <param name="allContentNodes">A set of root content nodes - children will also be included</param>
        /// <param name="applySort">Apply sorting - default false</param>
        /// <returns></returns>
        private string GenerateTextFromContentNodes(List<ContentNode> allContentNodes, bool applySort = false)
        {
            if (applySort)
            {
                SortContentNodes(allContentNodes);
            }
        
            var sb = new StringBuilder();
            foreach (var node in allContentNodes)
            {
                sb.Append(GenerateFullDocumentTextRecursive(node));
            }
            return sb.ToString();
        }

        private string GenerateFullDocumentTextRecursive(ContentNode node)
        {
            var sb = new StringBuilder();
            sb.Append(node.Text);
            // If the node is a title or heading, add a dashed line, 50 characters long
            if (node.Type == ContentNodeType.Title || node.Type == ContentNodeType.Heading)
            {
                sb.Append("\n");
                sb.Append(new string('-', 50));

            }
            sb.Append("\n");
            foreach (var child in node.Children)
            {
                sb.Append(GenerateFullDocumentTextRecursive(child));
            }
            return sb.ToString();

        }
    }
}
