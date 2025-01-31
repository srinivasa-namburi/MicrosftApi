// Copyright (c) Microsoft. All rights reserved.
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Interfaces;

/// <summary>
/// Interface for processing content trees.
/// </summary>
public interface IContentTreeProcessor
{
    /// <summary>
    /// Finds section headings within a content node.
    /// </summary>
    /// <param name="contentNode">The content node to search within.</param>
    /// <param name="sectionHeadings">The list to populate with found section headings.</param>
    void FindSectionHeadings(ContentNode contentNode, List<ContentNode> sectionHeadings);

    /// <summary>
    /// Counts the number of content nodes within a content node.
    /// </summary>
    /// <param name="contentNode">The content node to count.</param>
    /// <returns>The number of content nodes.</returns>
    int CountContentNodes(ContentNode contentNode);

    /// <summary>
    /// Finds the last title or heading in a content tree.
    /// </summary>
    /// <param name="contentTree">The content tree to search within.</param>
    /// <returns>The last title or heading content node, or null if none found.</returns>
    ContentNode? FindLastTitleOrHeading(List<ContentNode> contentTree);
}
