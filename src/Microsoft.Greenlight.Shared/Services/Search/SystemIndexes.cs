namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Centralized, system-defined vector store index names.
/// All indexes are prefixed with "system-".
/// </summary>
public static class SystemIndexes
{
    // Content Reference indexes (cr = content references)

    /// <summary>
    /// Vector index for GeneratedDocument content references.
    /// </summary>
    public const string GeneratedDocumentContentReferenceIndex = "system-cr-generated-documents";

    /// <summary>
    /// Vector index for GeneratedSection content references.
    /// </summary>
    public const string GeneratedSectionContentReferenceIndex = "system-cr-generated-sections";

    /// <summary>
    /// Vector index for ExternalFile content references.
    /// </summary>
    public const string ExternalFileContentReferenceIndex = "system-cr-external-files";

    /// <summary>
    /// Vector index for ReviewItem content references.
    /// </summary>
    public const string ReviewItemContentReferenceIndex = "system-cr-review-items";

    /// <summary>
    /// Vector index for ExternalLinkAsset content references.
    /// </summary>
    public const string ExternalLinkAssetContentReferenceIndex = "system-cr-external-link-assets";

    /// <summary>
    /// Vector index for Flow document process metadata intent detection.
    /// </summary>
    public const string DocumentProcessMetadataIntentIndex = "system-index-documentprocessmetadata";
}

