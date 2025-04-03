using Microsoft.Greenlight.Shared.Contracts.Components;
using Microsoft.Greenlight.Shared.Enums;
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

        /// <summary>
        /// Creates a version of the current state of a content node.
        /// Only works for BodyText type nodes.
        /// </summary>
        /// <param name="contentNodeId">The ID of the content node to version.</param>
        /// <param name="reason">The reason for creating this version.</param>
        /// <param name="comment">Optional comment about this version.</param>
        /// <returns>The new version number, or null if versioning failed.</returns>
        Task<int?> CreateContentNodeVersionAsync(
            Guid contentNodeId,
            ContentNodeVersioningReason reason = ContentNodeVersioningReason.System,
            string? comment = null);

        /// <summary>
        /// Replaces a content node text with new content while preserving its ID and versioning the current state.
        /// Only works for BodyText type nodes.
        /// </summary>
        /// <param name="existingContentNodeId">The ID of the existing content node.</param>
        /// <param name="newText">The new text for the content node.</param>
        /// <param name="reason">The reason for replacing the content node text.</param>
        /// <param name="comment">Optional comment about this change.</param>
        /// <returns>The updated content node, or null if replacement failed.</returns>
        Task<ContentNode?> ReplaceContentNodeTextAsync(
            Guid existingContentNodeId,
            string newText,
            ContentNodeVersioningReason reason = ContentNodeVersioningReason.ManualEdit,
            string? comment = null);

        /// <summary>
        /// Retrieves all versions of a content node.
        /// Only works for BodyText type nodes.
        /// </summary>
        /// <param name="contentNodeId">The ID of the content node.</param>
        /// <returns>A list of content node versions, or empty list if node cannot be versioned.</returns>
        Task<List<ContentNodeVersion>> GetContentNodeVersionsAsync(Guid contentNodeId);

        /// <summary>
        /// Promotes a previous version of a content node to be the current version.
        /// Only works for BodyText type nodes.
        /// </summary>
        /// <param name="contentNodeId">The ID of the content node.</param>
        /// <param name="versionId">The ID of the version to promote.</param>
        /// <param name="comment">Optional comment about this promotion.</param>
        /// <returns>The updated content node, or null if promotion failed.</returns>
        Task<ContentNode?> PromotePreviousVersionAsync(
            Guid contentNodeId,
            Guid versionId,
            string? comment = null);
    }
}