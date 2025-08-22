// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.SourceReferences;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;

/// <summary>
/// Contract for AI completion services that generate document body content using RAG-style inputs.
/// Implementations must accept heterogeneous source reference items and produce content nodes.
/// </summary>
public interface IAiCompletionService
{
    /// <summary>
    /// Generates body content nodes using a heterogeneous list of source reference items (Kernel Memory, Vector Store, Plugins, etc.).
    /// </summary>
    /// <param name="sourceReferences">Heterogeneous source references (Kernel Memory, Vector Store, Plugins, etc.).</param>
    /// <param name="sectionOrTitleNumber">Section number identifier.</param>
    /// <param name="sectionOrTitleText">Section title text.</param>
    /// <param name="contentNodeType">Content node type.</param>
    /// <param name="tableOfContentsString">TOC context for the section.</param>
    /// <param name="metadataId">Optional metadata id for additional context.</param>
    /// <param name="sectionContentNode">Optional existing section node for instructions.</param>
    Task<List<ContentNode>> GetBodyContentNodes(List<SourceReferenceItem> sourceReferences,
        string sectionOrTitleNumber,
        string sectionOrTitleText, ContentNodeType contentNodeType, string tableOfContentsString, Guid? metadataId,
        ContentNode? sectionContentNode);

    /// <summary>
    /// Backwards-compatible shim for legacy callers still providing only document process repository (Kernel Memory) items.
    /// </summary>
    /// <param name="sourceDocuments">Legacy Kernel Memory-only source items.</param>
    /// <param name="sectionOrTitleNumber">Section number identifier.</param>
    /// <param name="sectionOrTitleText">Section title text.</param>
    /// <param name="contentNodeType">Content node type.</param>
    /// <param name="tableOfContentsString">TOC context for the section.</param>
    /// <param name="metadataId">Optional metadata id for additional context.</param>
    /// <param name="sectionContentNode">Optional existing section node for instructions.</param>
    async Task<List<ContentNode>> GetBodyContentNodes(List<DocumentProcessRepositorySourceReferenceItem> sourceDocuments,
        string sectionOrTitleNumber,
        string sectionOrTitleText, ContentNodeType contentNodeType, string tableOfContentsString, Guid? metadataId,
        ContentNode? sectionContentNode)
        => await GetBodyContentNodes(sourceDocuments.Cast<SourceReferenceItem>().ToList(), sectionOrTitleNumber, sectionOrTitleText,
            contentNodeType, tableOfContentsString, metadataId, sectionContentNode);
}
