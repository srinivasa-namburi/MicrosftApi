namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Enum representing different types of source references.
/// </summary>
public enum SourceReferenceType
{
    /// <summary>
    /// Represents a document process repository.
    /// </summary>
    DocumentProcessRepository = 100,

    /// <summary>
    /// Represents a plugin.
    /// </summary>
    Plugin = 200,

    /// <summary>
    /// Represents an additional document library.
    /// </summary>
    AdditionalDocumentLibrary = 300,

    /// <summary>
    /// Represents general knowledge.
    /// </summary>
    GeneralKnowledge = 999
}
