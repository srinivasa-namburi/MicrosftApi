namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Represents different types of document libraries to help determine type of document
/// library. Also sets up the appropriate factory for creating source reference items.
/// </summary>
public enum DocumentLibraryType
{
    /// <summary>
    /// Document Library Type for Reviews.
    /// </summary>
    Reviews = 800,

    /// <summary>
    ///  Document Library Type for Primary Document Process Library.
    /// </summary>
    PrimaryDocumentProcessLibrary = 100,

    /// <summary>
    ///  Document Library Type for Additional Document Process Library.
    /// </summary>
    AdditionalDocumentLibrary = 200
}
