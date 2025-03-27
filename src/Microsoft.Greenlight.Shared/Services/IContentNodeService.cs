using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Service for content node operations, including retrieving hierarchical content nodes and sorting them.
    /// </summary>
    public interface IContentNodeService
    {
        /// <summary>
        /// Sort a hierarchical list of content nodes.
        /// </summary>
        /// <param name="nodes"></param>
        void SortContentNodes(List<ContentNode> nodes);

        /// <summary>
        /// Returns a hierarchical list of content nodes for a generated document, sorted by section numbers if present
        /// </summary>
        /// <param name="generatedDocumentId">The ID of a generated document</param>
        /// <param name="enableTracking">Whether to enable EF Core change tracking. Default is false.</param>
        /// <param name="addParentNodes">Whether to add parent nodes as objects to the content nodes. Default is false. ParentId is always added.</param>
        /// <returns></returns>
        Task<List<ContentNode>?> GetContentNodesHierarchicalAsyncForDocumentId(Guid generatedDocumentId, bool enableTracking = false, bool addParentNodes = false);

        /// <summary>
        /// Renders the text with chapter headings for a set of content nodes (preserving hierarchy and order).
        /// </summary>
        /// <param name="rootContentNodes"></param>
        /// <returns></returns>
        Task<string?> GetRenderedTextForContentNodeHierarchiesAsync(List<ContentNode> rootContentNodes);
    }
}