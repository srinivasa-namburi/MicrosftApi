namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Represents the type of a file document. Helps map Azure blob container
/// name to corresponding document type.
/// </summary>
public enum FileDocumentType
{
    /// <summary>
    /// A document that has been exported.
    /// </summary>
    ExportedDocument,

    /// <summary>
    /// A document that is an asset.
    /// </summary>
    DocumentAsset,

    /// <summary>
    /// A document that is under review.
    /// </summary>
    Review,

    /// <summary>
    /// Represents a temporary file used for reference purposes. It is typically utilized to process chat and ad-hoc attachments
    /// </summary>
    TemporaryReferenceFile
}
